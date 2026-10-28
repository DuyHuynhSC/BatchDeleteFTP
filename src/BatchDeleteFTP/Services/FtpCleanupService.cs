using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BatchDeleteFTP.Helpers;
using BatchDeleteFTP.Models;
using FluentFTP;
using FluentFTP.Proxy.AsyncProxy;

namespace BatchDeleteFTP.Services;

public class FtpCleanupService
{
    private readonly AppLogger _logger;

    private sealed class FtpConnectionLogger : IFtpLogger
    {
        private readonly AppLogger _appLogger;
        public bool IsEnabled { get; set; } = true;

        public FtpConnectionLogger(AppLogger appLogger)
        {
            _appLogger = appLogger;
        }

        public void Log(FtpLogEntry entry)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(entry.Message)) return;
            string msg = entry.Message.Trim();
            if (msg.StartsWith(">") ||
                msg.StartsWith("Command:") ||
                msg.StartsWith("Response:") ||
                msg.StartsWith("Status:") ||
                msg.Contains("CONNECT") ||
                msg.Contains("TLS") ||
                msg.Contains("SSL") ||
                entry.Severity == FtpTraceLevel.Warn ||
                entry.Severity == FtpTraceLevel.Error)
            {
                _appLogger.Info($"[FTP] {msg}");
            }
        }
    }

    public FtpCleanupService(AppLogger logger)
    {
        _logger = logger;
    }

    public static IReadOnlyList<string> NormalizeRemotePaths(IEnumerable<string> paths)
    {
        var cleaned = paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p =>
            {
                string s = p.Trim().Replace('\\', '/');
                if (!s.StartsWith('/')) s = "/" + s;
                if (s.Length > 1) s = s.TrimEnd('/');
                return s;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p.Length)
            .ToList();

        if (cleaned.Contains("/"))
        {
            return new List<string> { "/" };
        }

        var result = new List<string>();
        foreach (string path in cleaned)
        {
            bool isSubFolderOfExisting = result.Any(parent =>
                path.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase));
            if (!isSubFolderOfExisting)
            {
                result.Add(path);
            }
        }

        return result.Count > 0 ? result : new List<string> { "/" };
    }

    public AsyncFtpClient CreateClient(FtpSettings settings)
    {
        AsyncFtpClient client;

        if (settings.UseProxy && !string.IsNullOrWhiteSpace(settings.ProxyHost) && settings.ProxyPort > 0)
        {
            var profile = new FtpProxyProfile
            {
                ProxyHost = settings.ProxyHost.Trim(),
                ProxyPort = settings.ProxyPort,
                FtpHost = settings.Host.Trim(),
                FtpPort = settings.Port,
                FtpCredentials = new NetworkCredential(settings.Username, settings.Password)
            };

            if (!string.IsNullOrWhiteSpace(settings.ProxyUser))
            {
                profile.ProxyCredentials = new NetworkCredential(settings.ProxyUser, settings.ProxyPassword);
            }

            client = new AsyncFtpClientHttp11Proxy(profile);
        }
        else
        {
            client = new AsyncFtpClient(settings.Host, settings.Username, settings.Password, settings.Port);
        }

        client.Config.EncryptionMode = string.Equals(settings.EncryptionMode, "Explicit", StringComparison.OrdinalIgnoreCase)
            ? FtpEncryptionMode.Explicit
            : FtpEncryptionMode.Implicit;
        client.Config.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        client.Config.ValidateAnyCertificate = true;
        client.Config.DataConnectionType = FtpDataConnectionType.PASV;
        client.Config.RetryAttempts = 1;
        client.Config.ConnectTimeout = 15000;
        client.Config.ReadTimeout = 20000;
        client.Encoding = Encoding.UTF8;
        return client;
    }

    private async Task ConnectOptimizedAsync(AsyncFtpClient client, FtpSettings settings, CancellationToken ct)
    {
        var connLogger = new FtpConnectionLogger(_logger);
        client.Logger = connLogger;
        try
        {
            await client.Connect(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Info($"[FTP] Thử lại kết nối với cấu hình TLS 1.2 tiêu chuẩn ({ex.Message})...");
            client.Config.SslProtocols = SslProtocols.Tls12;
            await client.Connect(ct);
        }
        finally
        {
            connLogger.IsEnabled = false;
        }
    }

    public async Task TestConnectionAsync(FtpSettings settings, CancellationToken ct = default)
    {
        await using var client = CreateClient(settings);
        string proxyInfo = (settings.UseProxy && !string.IsNullOrWhiteSpace(settings.ProxyHost))
            ? $" qua Proxy {settings.ProxyHost}:{settings.ProxyPort}"
            : string.Empty;

        _logger.Info($"Đang kết nối{proxyInfo} tới {settings.Host}:{settings.Port} ({settings.EncryptionMode} TLS)...");
        await ConnectOptimizedAsync(client, settings, ct);
        _logger.Info("Kết nối FTP thành công!");
    }

    public async Task<List<FtpFolderNode>> GetSubDirectoriesAsync(FtpSettings settings, string remotePath, CancellationToken ct = default)
    {
        await using var client = CreateClient(settings);
        await ConnectOptimizedAsync(client, settings, ct);

        var items = await client.GetListing(remotePath, FtpListOption.Auto, ct);
        var folders = new List<FtpFolderNode>();

        foreach (var item in items.Where(i => i.Type == FtpObjectType.Directory))
        {
            if (item.Name is "." or "..") continue;
            var node = new FtpFolderNode
            {
                Name = item.Name,
                FullPath = item.FullName
            };
            node.Children.Add(new FtpFolderNode { Name = "Đang tải...", IsPlaceholder = true });
            folders.Add(node);
        }

        return folders.OrderBy(f => f.Name).ToList();
    }

    public async Task<List<FtpFileCandidate>> ScanCandidatesAsync(
        FtpSettings settings,
        DateTime currentSystemDate,
        CancellationToken ct = default)
    {
        var candidates = new List<FtpFileCandidate>();
        var visitedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targetPaths = NormalizeRemotePaths(settings.RemotePaths);

        await using var client = CreateClient(settings);
        _logger.Info($"Đang kết nối tới {settings.Host}:{settings.Port} để quét file...");
        await ConnectOptimizedAsync(client, settings, ct);

        foreach (string remoteRoot in targetPaths)
        {
            _logger.Info($"Đang quét đệ quy thư mục: {remoteRoot}");
            await ScanDirectoryRecursiveAsync(client, remoteRoot, currentSystemDate, settings.RetentionDays, candidates, visitedFiles, ct);
        }

        _logger.Info($"Quét hoàn tất. Tìm thấy {candidates.Count} file thỏa điều kiện xóa (SystemDate > UploadDate + {settings.RetentionDays}).");
        return candidates;
    }

    private async Task ScanDirectoryRecursiveAsync(
        AsyncFtpClient client,
        string currentPath,
        DateTime currentSystemDate,
        int retentionDays,
        List<FtpFileCandidate> candidates,
        HashSet<string> visitedFiles,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        FtpListItem[] items;
        try
        {
            items = await client.GetListing(currentPath, FtpListOption.Modify, ct);
        }
        catch (Exception ex)
        {
            _logger.Error($"Không thể đọc thư mục '{currentPath}': {ex.Message}");
            return;
        }

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            if (item.Type == FtpObjectType.Directory)
            {
                if (item.Name is "." or "..") continue;
                await ScanDirectoryRecursiveAsync(client, item.FullName, currentSystemDate, retentionDays, candidates, visitedFiles, ct);
            }
            else if (item.Type == FtpObjectType.File)
            {
                DateTime modified = item.Modified;
                if (modified == DateTime.MinValue)
                {
                    try
                    {
                        modified = await client.GetModifiedTime(item.FullName, ct);
                    }
                    catch
                    {
                    }
                }

                if (modified != DateTime.MinValue &&
                    DateFilterRule.ShouldDelete(modified, currentSystemDate, retentionDays) &&
                    visitedFiles.Add(item.FullName))
                {
                    candidates.Add(new FtpFileCandidate
                    {
                        IsSelected = true,
                        FullPath = item.FullName,
                        FileName = item.Name,
                        Size = item.Size,
                        ModifiedDate = modified,
                        Status = "Sẵn sàng xóa"
                    });
                }
            }
        }
    }

    public async Task<(int DeletedCount, int FailedCount)> ExecuteBackupAndDeleteAsync(
        FtpSettings settings,
        IEnumerable<FtpFileCandidate> candidates,
        CancellationToken ct = default)
    {
        var selectedFiles = candidates.Where(c => c.IsSelected).ToList();
        if (selectedFiles.Count == 0)
        {
            _logger.Info("Không có file nào được chọn để xóa.");
            return (0, 0);
        }

        string sessionTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        int deletedCount = 0;
        int failedCount = 0;

        await using var client = CreateClient(settings);
        _logger.Info($"Đang kết nối tới {settings.Host}:{settings.Port} để thực thi {(settings.EnableBackup ? "Backup & Xóa" : "Xóa")} {selectedFiles.Count} file...");
        await ConnectOptimizedAsync(client, settings, ct);

        foreach (var file in selectedFiles)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (settings.EnableBackup)
                {
                    file.Status = "Đang Backup...";
                    string localFilePath = BackupPathBuilder.BuildLocalBackupFilePath(
                        settings.LocalBackupPath,
                        sessionTimestamp,
                        file.FullPath);

                    string? localDir = Path.GetDirectoryName(localFilePath);
                    if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
                    {
                        Directory.CreateDirectory(localDir);
                    }

                    FtpStatus dlStatus = await client.DownloadFile(localFilePath, file.FullPath, FtpLocalExists.Overwrite, FtpVerify.None, progress: null, token: ct);
                    if (dlStatus != FtpStatus.Success || !File.Exists(localFilePath))
                    {
                        file.Status = "Lỗi Backup (Đã hủy xóa)";
                        _logger.Error($"Backup thất bại cho file '{file.FullPath}'. Bỏ qua không xóa file này.");
                        failedCount++;
                        continue;
                    }

                    _logger.Info($"Đã Backup: '{file.FullPath}' -> '{localFilePath}'");
                }

                file.Status = "Đang xóa trên FTP...";
                await client.DeleteFile(file.FullPath, ct);
                file.Status = settings.EnableBackup ? "Đã Backup & Xóa" : "Đã Xóa";
                _logger.Info($"Đã xóa thành công trên FTP: '{file.FullPath}' (Ngày upload: {file.ModifiedDate:yyyy-MM-dd HH:mm:ss})");
                deletedCount++;
            }
            catch (Exception ex)
            {
                file.Status = $"Lỗi: {ex.Message}";
                _logger.Error($"Lỗi khi xử lý file '{file.FullPath}': {ex.Message}");
                failedCount++;
            }
        }

        _logger.Info($"Hoàn tất! Đã xóa: {deletedCount}, Lỗi: {failedCount}.");
        return (deletedCount, failedCount);
    }
}
