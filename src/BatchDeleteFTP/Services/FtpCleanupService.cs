using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BatchDeleteFTP.Helpers;
using BatchDeleteFTP.Models;
using FluentFTP;

namespace BatchDeleteFTP.Services;

public class FtpCleanupService
{
    private readonly AppLogger _logger;

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

    private AsyncFtpClient CreateClient(FtpSettings settings)
    {
        var client = new AsyncFtpClient(settings.Host, settings.Username, settings.Password, settings.Port);
        client.Config.EncryptionMode = string.Equals(settings.EncryptionMode, "Explicit", StringComparison.OrdinalIgnoreCase)
            ? FtpEncryptionMode.Explicit
            : FtpEncryptionMode.Implicit;
        client.Config.ValidateAnyCertificate = true;
        client.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;
        client.Config.ConnectTimeout = 15000;
        client.Config.ReadTimeout = 30000;
        return client;
    }

    public async Task TestConnectionAsync(FtpSettings settings, CancellationToken ct = default)
    {
        await using var client = CreateClient(settings);
        _logger.Info($"Đang kết nối tới {settings.Host}:{settings.Port} ({settings.EncryptionMode} TLS)...");
        await client.AutoConnect(ct);
        _logger.Info("Kết nối FTP thành công!");
    }

    public async Task<List<FtpFolderNode>> GetSubDirectoriesAsync(FtpSettings settings, string remotePath, CancellationToken ct = default)
    {
        await using var client = CreateClient(settings);
        await client.AutoConnect(ct);

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
        await client.AutoConnect(ct);

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
        await client.AutoConnect(ct);

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
