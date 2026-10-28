# Kế hoạch Triển khai: BatchDeleteFTP (.NET 8 WPF + Console Hybrid)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Xây dựng ứng dụng C# .NET 8 WPF (kết hợp chạy dòng lệnh CMD `--auto` / `--preview`) để kết nối FTPS Implicit (port 6021), cho phép chọn Remote Path trên FTP, Xem trước (Preview), Backup file về local trước khi xóa, và xóa tất cả các file trong folder/sub-folder có `SystemDate.Date > UploadDate.Date.AddDays(1)`.

**Architecture:** Ứng dụng Hybrid Single-Executable (.NET 8 `net8.0-windows`, `UseWPF=true`), phân luồng trong `App.xaml.cs` (`OnStartup`): nếu không có tham số thì mở `MainWindow` (WPF), nếu có tham số `--auto` / `--console` / `--preview` thì gọi Win32 `AttachConsole(-1)` để chạy chế độ CLI. Logic cốt lõi (lọc ngày, đường dẫn backup, đọc/ghi cấu hình, kết nối `FluentFTP`) được tách thành các service độc lập và kiểm thử tự động bằng `xUnit`.

**Tech Stack:** C# 12, .NET 8 (`net8.0-windows`), WPF, `FluentFTP` (NuGet), `System.Text.Json`, `xUnit` (Unit Test).

**Spec:** [2026-10-28-batch-delete-ftp-design.md](file:///c:/SourceCode/BatchDeleteFTP/docs/superpowers/specs/2026-10-28-batch-delete-ftp-design.md)

## Global Constraints

- Target Framework: `net8.0-windows` cho cả project chính và project test.
- Mặc định thông tin FTP theo hình đính kèm: Host `ftp.fujinet.net`, Port `6021`, Encryption `Implicit` (`FtpEncryptionMode.Implicit`), User `UCD-MOROZOFF`.
- Điều kiện xóa file: `CurrentSystemDate.Date > FileModifiedDate.Date.AddDays(RetentionDays)` với `RetentionDays = 1` mặc định.
- Chỉ xóa file trên FTP sau khi đã tải Backup thành công về máy local (khi `EnableBackup == true`). Giữ nguyên các thư mục rỗng trên FTP.

---

### Task 1: Khởi tạo Solution, Project Structure và Logic Lọc Ngày + Đường Dẫn Backup (TDD)

**Files:**
- Create: `BatchDeleteFTP.sln`
- Create: `src/BatchDeleteFTP/BatchDeleteFTP.csproj`
- Create: `src/BatchDeleteFTP/Helpers/DateFilterRule.cs`
- Create: `src/BatchDeleteFTP/Helpers/BackupPathBuilder.cs`
- Create: `tests/BatchDeleteFTP.Tests/BatchDeleteFTP.Tests.csproj`
- Create: `tests/BatchDeleteFTP.Tests/DateFilterRuleTests.cs`
- Create: `tests/BatchDeleteFTP.Tests/BackupPathBuilderTests.cs`

**Interfaces:**
- Produces:
  - `BatchDeleteFTP.Helpers.DateFilterRule.ShouldDelete(DateTime fileModifiedDate, DateTime currentSystemDate, int retentionDays = 1) -> bool`
  - `BatchDeleteFTP.Helpers.BackupPathBuilder.BuildLocalBackupFilePath(string localBackupRoot, string sessionTimestamp, string remoteFilePath) -> string`

- [ ] **Step 1: Khởi tạo Solution, Project WPF và Project xUnit**

Chạy lệnh tạo solution và các project:
```powershell
dotnet new sln -n BatchDeleteFTP
dotnet new wpf -n BatchDeleteFTP -o src/BatchDeleteFTP -f net8.0-windows
dotnet new xunit -n BatchDeleteFTP.Tests -o tests/BatchDeleteFTP.Tests -f net8.0-windows
dotnet sln add src/BatchDeleteFTP/BatchDeleteFTP.csproj
dotnet sln add tests/BatchDeleteFTP.Tests/BatchDeleteFTP.Tests.csproj
dotnet add tests/BatchDeleteFTP.Tests/BatchDeleteFTP.Tests.csproj reference src/BatchDeleteFTP/BatchDeleteFTP.csproj
dotnet add src/BatchDeleteFTP/BatchDeleteFTP.csproj package FluentFTP --version 51.1.0
```

- [ ] **Step 2: Viết Unit Test cho `DateFilterRule` và `BackupPathBuilder` (Failing Tests)**

Tạo file `tests/BatchDeleteFTP.Tests/DateFilterRuleTests.cs`:
```csharp
using System;
using BatchDeleteFTP.Helpers;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class DateFilterRuleTests
{
    [Fact]
    public void ShouldDelete_WhenSystemDateIsGreaterThanUploadDatePlusOne_ReturnsTrue()
    {
        var uploadDate = new DateTime(2026, 10, 26, 23, 50, 0);
        var systemDate = new DateTime(2026, 10, 28, 08, 15, 0);

        bool result = DateFilterRule.ShouldDelete(uploadDate, systemDate, retentionDays: 1);

        Assert.True(result);
    }

    [Fact]
    public void ShouldDelete_WhenSystemDateEqualsUploadDatePlusOne_ReturnsFalse()
    {
        var uploadDate = new DateTime(2026, 10, 27, 01, 00, 0);
        var systemDate = new DateTime(2026, 10, 28, 22, 00, 0);

        bool result = DateFilterRule.ShouldDelete(uploadDate, systemDate, retentionDays: 1);

        Assert.False(result);
    }

    [Fact]
    public void ShouldDelete_WhenUploadedToday_ReturnsFalse()
    {
        var uploadDate = new DateTime(2026, 10, 28, 10, 00, 0);
        var systemDate = new DateTime(2026, 10, 28, 15, 00, 0);

        bool result = DateFilterRule.ShouldDelete(uploadDate, systemDate, retentionDays: 1);

        Assert.False(result);
    }
}
```

Tạo file `tests/BatchDeleteFTP.Tests/BackupPathBuilderTests.cs`:
```csharp
using System.IO;
using BatchDeleteFTP.Helpers;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class BackupPathBuilderTests
{
    [Fact]
    public void BuildLocalBackupFilePath_PreservesRemoteDirectoryStructure()
    {
        string root = @"C:\Backups";
        string session = "20261028_153000";
        string remotePath = "/MOROZOFF/Outbound/2026/order_01.csv";

        string localPath = BackupPathBuilder.BuildLocalBackupFilePath(root, session, remotePath);

        string expected = Path.Combine(@"C:\Backups", "20261028_153000", "MOROZOFF", "Outbound", "2026", "order_01.csv");
        Assert.Equal(expected, localPath);
    }
}
```

- [ ] **Step 3: Chạy `dotnet test` để xác nhận lỗi biên dịch (Red)**

Run: `dotnet test`
Expected: FAIL (chưa định nghĩa `DateFilterRule` và `BackupPathBuilder`).

- [ ] **Step 4: Cài đặt `DateFilterRule` và `BackupPathBuilder` (Green)**

Tạo file `src/BatchDeleteFTP/Helpers/DateFilterRule.cs`:
```csharp
using System;

namespace BatchDeleteFTP.Helpers;

public static class DateFilterRule
{
    public static bool ShouldDelete(DateTime fileModifiedDate, DateTime currentSystemDate, int retentionDays = 1)
    {
        return currentSystemDate.Date > fileModifiedDate.Date.AddDays(retentionDays);
    }
}
```

Tạo file `src/BatchDeleteFTP/Helpers/BackupPathBuilder.cs`:
```csharp
using System;
using System.IO;

namespace BatchDeleteFTP.Helpers;

public static class BackupPathBuilder
{
    public static string BuildLocalBackupFilePath(string localBackupRoot, string sessionTimestamp, string remoteFilePath)
    {
        string normalizedRemote = (remoteFilePath ?? string.Empty)
            .Replace('\\', '/')
            .TrimStart('/');

        string[] segments = normalizedRemote.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string baseDir = Path.Combine(localBackupRoot, sessionTimestamp);

        foreach (string segment in segments)
        {
            baseDir = Path.Combine(baseDir, segment);
        }

        return baseDir;
    }
}
```

- [ ] **Step 5: Chạy lại `dotnet test` để xác minh tất cả test PASS**

Run: `dotnet test`
Expected: PASS (4/4 tests passed).

---

### Task 2: Xây dựng Models, Cấu hình (`ConfigService`) và Ghi Log (`AppLogger`)

**Files:**
- Create: `src/BatchDeleteFTP/Models/FtpSettings.cs`
- Create: `src/BatchDeleteFTP/Models/FtpFileCandidate.cs`
- Create: `src/BatchDeleteFTP/Models/FtpFolderNode.cs`
- Create: `src/BatchDeleteFTP/Services/ConfigService.cs`
- Create: `src/BatchDeleteFTP/Services/AppLogger.cs`
- Create: `tests/BatchDeleteFTP.Tests/ConfigServiceTests.cs`

**Interfaces:**
- Produces:
  - `BatchDeleteFTP.Models.FtpSettings`
  - `BatchDeleteFTP.Models.FtpFileCandidate`
  - `BatchDeleteFTP.Models.FtpFolderNode`
  - `BatchDeleteFTP.Services.ConfigService.Load(string filePath) -> FtpSettings`
  - `BatchDeleteFTP.Services.ConfigService.Save(FtpSettings settings, string filePath)`
  - `BatchDeleteFTP.Services.AppLogger`

- [ ] **Step 1: Viết Unit Test cho `ConfigService` (Failing Test)**

Tạo file `tests/BatchDeleteFTP.Tests/ConfigServiceTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class ConfigServiceTests
{
    [Fact]
    public void SaveAndLoad_PreservesSettingsAndRemotePaths()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ftp_config_{Guid.NewGuid()}.json");
        try
        {
            var service = new ConfigService();
            var original = new FtpSettings
            {
                Host = "ftp.fujinet.net",
                Port = 6021,
                Username = "UCD-MOROZOFF",
                Password = "SecretPassword123",
                RemotePaths = new List<string> { "/FolderA", "/FolderB/Sub" },
                RetentionDays = 1,
                EnableBackup = true,
                LocalBackupPath = @"C:\Temp\FTPBackup"
            };

            service.Save(original, tempFile);
            FtpSettings loaded = service.Load(tempFile);

            Assert.Equal("ftp.fujinet.net", loaded.Host);
            Assert.Equal(6021, loaded.Port);
            Assert.Equal("UCD-MOROZOFF", loaded.Username);
            Assert.Equal("SecretPassword123", loaded.Password);
            Assert.Equal(new[] { "/FolderA", "/FolderB/Sub" }, loaded.RemotePaths);
            Assert.True(loaded.EnableBackup);
            Assert.Equal(@"C:\Temp\FTPBackup", loaded.LocalBackupPath);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
```

- [ ] **Step 2: Chạy `dotnet test` để xác nhận lỗi biên dịch (Red)**

Run: `dotnet test`
Expected: FAIL.

- [ ] **Step 3: Cài đặt `FtpSettings`, `FtpFileCandidate`, `FtpFolderNode`, `ConfigService` và `AppLogger` (Green)**

Tạo file `src/BatchDeleteFTP/Models/FtpSettings.cs`:
```csharp
using System.Collections.Generic;
using System.IO;

namespace BatchDeleteFTP.Models;

public class FtpSettings
{
    public string Host { get; set; } = "ftp.fujinet.net";
    public int Port { get; set; } = 6021;
    public string EncryptionMode { get; set; } = "Implicit";
    public string Username { get; set; } = "UCD-MOROZOFF";
    public string Password { get; set; } = string.Empty;
    public List<string> RemotePaths { get; set; } = new() { "/" };
    public int RetentionDays { get; set; } = 1;
    public bool EnableBackup { get; set; } = true;
    public string LocalBackupPath { get; set; } = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Backups");
}
```

Tạo file `src/BatchDeleteFTP/Models/FtpFileCandidate.cs`:
```csharp
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BatchDeleteFTP.Models;

public class FtpFileCandidate : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private string _status = "Chờ xử lý";

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime ModifiedDate { get; set; }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

Tạo file `src/BatchDeleteFTP/Models/FtpFolderNode.cs`:
```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BatchDeleteFTP.Models;

public class FtpFolderNode : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isExpanded;

    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = "/";
    public bool IsPlaceholder { get; set; }
    public bool ChildrenLoaded { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public ObservableCollection<FtpFolderNode> Children { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

Tạo file `src/BatchDeleteFTP/Services/ConfigService.cs`:
```csharp
using System;
using System.IO;
using System.Text.Json;
using BatchDeleteFTP.Models;

namespace BatchDeleteFTP.Services;

public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string DefaultConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    public FtpSettings Load(string? filePath = null)
    {
        string targetPath = filePath ?? DefaultConfigPath;
        if (!File.Exists(targetPath))
        {
            var defaultSettings = new FtpSettings();
            Save(defaultSettings, targetPath);
            return defaultSettings;
        }

        string json = File.ReadAllText(targetPath);
        var settings = JsonSerializer.Deserialize<FtpSettings>(json, JsonOptions) ?? new FtpSettings();
        if (settings.RemotePaths == null || settings.RemotePaths.Count == 0)
        {
            settings.RemotePaths = new() { "/" };
        }
        return settings;
    }

    public void Save(FtpSettings settings, string? filePath = null)
    {
        string targetPath = filePath ?? DefaultConfigPath;
        string? dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(targetPath, json);
    }
}
```

Tạo file `src/BatchDeleteFTP/Services/AppLogger.cs`:
```csharp
using System;
using System.IO;

namespace BatchDeleteFTP.Services;

public class AppLogger
{
    private readonly string _logDir;
    public event Action<string>? OnLogMessage;

    public AppLogger(string? logDir = null)
    {
        _logDir = logDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(_logDir))
        {
            Directory.CreateDirectory(_logDir);
        }
    }

    public void Info(string message) => Write("INFO", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        Console.WriteLine(line);
        OnLogMessage?.Invoke(line);

        try
        {
            string logFile = Path.Combine(_logDir, $"BatchDeleteFTP_{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(logFile, line + Environment.NewLine);
        }
        catch
        {
            // Tránh crash ứng dụng nếu file log bị khóa
        }
    }
}
```

- [ ] **Step 4: Chạy `dotnet test` để xác minh PASS**

Run: `dotnet test`
Expected: PASS (5/5 tests passed).

---

### Task 3: Xây dựng Dịch vụ FTP (`FtpCleanupService`) hỗ trợ Duyệt Thư mục, Xem Trước (Preview), Backup & Xóa

**Files:**
- Create: `src/BatchDeleteFTP/Services/FtpCleanupService.cs`
- Create: `tests/BatchDeleteFTP.Tests/RemotePathNormalizerTests.cs`

**Interfaces:**
- Consumes: `FtpSettings`, `FtpFileCandidate`, `DateFilterRule`, `BackupPathBuilder`, `AppLogger`
- Produces:
  - `FtpCleanupService.NormalizeRemotePaths(IEnumerable<string> paths) -> IReadOnlyList<string>`
  - `FtpCleanupService.TestConnectionAsync(FtpSettings settings, CancellationToken ct) -> Task`
  - `FtpCleanupService.GetSubDirectoriesAsync(FtpSettings settings, string remotePath, CancellationToken ct) -> Task<List<FtpFolderNode>>`
  - `FtpCleanupService.ScanCandidatesAsync(FtpSettings settings, DateTime currentSystemDate, CancellationToken ct) -> Task<List<FtpFileCandidate>>`
  - `FtpCleanupService.ExecuteBackupAndDeleteAsync(FtpSettings settings, IEnumerable<FtpFileCandidate> candidates, CancellationToken ct) -> Task<(int DeletedCount, int FailedCount)>`

- [ ] **Step 1: Viết Unit Test cho hàm chuẩn hóa và khử trùng lặp `RemotePaths` (Failing Test)**

Tạo file `tests/BatchDeleteFTP.Tests/RemotePathNormalizerTests.cs`:
```csharp
using BatchDeleteFTP.Services;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class RemotePathNormalizerTests
{
    [Fact]
    public void NormalizeRemotePaths_RemovesDuplicatesAndChildRedundancy()
    {
        var raw = new[] { "FolderA", "/FolderA/", "/FolderA/Sub1", "/FolderB" };

        var normalized = FtpCleanupService.NormalizeRemotePaths(raw);

        Assert.Equal(new[] { "/FolderA", "/FolderB" }, normalized);
    }
}
```

- [ ] **Step 2: Chạy `dotnet test` để xác nhận lỗi (Red)**

Run: `dotnet test`
Expected: FAIL.

- [ ] **Step 3: Cài đặt `FtpCleanupService` sử dụng `FluentFTP` (Green)**

Tạo file `src/BatchDeleteFTP/Services/FtpCleanupService.cs`:
```csharp
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
        _logger.Info($"Đang kết nối tới {settings.Host}:{settings.Port} (Implicit TLS)...");
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
            // Thêm node giả để hiển thị mũi tên mở rộng trên TreeView
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
                        // Giữ nguyên nếu server không hỗ trợ MDTM cho file này
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
```

- [ ] **Step 4: Chạy `dotnet test` để xác minh tất cả test PASS**

Run: `dotnet test`
Expected: PASS (6/6 tests passed).

---

### Task 4: Xây dựng Cửa sổ Chọn Thư mục Từ xa trên FTP (`RemoteFolderPickerWindow`) và Giao diện Chính (`MainWindow`)

**Files:**
- Create: `src/BatchDeleteFTP/Views/RemoteFolderPickerWindow.xaml`
- Create: `src/BatchDeleteFTP/Views/RemoteFolderPickerWindow.xaml.cs`
- Modify: `src/BatchDeleteFTP/MainWindow.xaml`
- Modify: `src/BatchDeleteFTP/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `FtpSettings`, `FtpFolderNode`, `FtpFileCandidate`, `ConfigService`, `FtpCleanupService`, `AppLogger`
- Produces:
  - `BatchDeleteFTP.Views.RemoteFolderPickerWindow` (TreeView lazy-load thư mục FTP + Checkbox chọn nhiều đường dẫn)
  - `BatchDeleteFTP.MainWindow` (Giao diện cấu hình, chọn Remote Paths, Xem trước, chọn/bỏ chọn file, Backup & Xóa)

- [ ] **Step 1: Tạo cửa sổ `RemoteFolderPickerWindow.xaml` và `.xaml.cs` để duyệt và tích chọn Remote Path trên FTP**

Tạo file `src/BatchDeleteFTP/Views/RemoteFolderPickerWindow.xaml`:
```xml
<Window x:Class="BatchDeleteFTP.Views.RemoteFolderPickerWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Chọn Thư Mục Trên FTP Server" Height="480" Width="550"
        WindowStartupLocation="CenterOwner">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Margin="0,0,0,8" TextWrapping="Wrap"
                   Text="Mở rộng (▶) để tải các thư mục con và tích chọn [x] một hoặc nhiều thư mục cần quét xóa:"/>

        <TreeView x:Name="FolderTreeView" Grid.Row="1" BorderBrush="#CCCCCC" BorderThickness="1"
                  TreeViewItem.Expanded="FolderTreeViewItem_Expanded">
            <TreeView.ItemTemplate>
                <HierarchicalDataTemplate ItemsSource="{Binding Children}">
                    <StackPanel Orientation="Horizontal" Margin="2">
                        <CheckBox IsChecked="{Binding IsSelected}" VerticalAlignment="Center" Margin="0,0,6,0"/>
                        <TextBlock Text="📁 " VerticalAlignment="Center"/>
                        <TextBlock Text="{Binding Name}" VerticalAlignment="Center" FontWeight="SemiBold"/>
                        <TextBlock Text="{Binding FullPath, StringFormat=' ({0})'}" Foreground="Gray" VerticalAlignment="Center"/>
                    </StackPanel>
                </HierarchicalDataTemplate>
            </TreeView.ItemTemplate>
        </TreeView>

        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,10,0,0">
            <TextBlock x:Name="StatusText" VerticalAlignment="Center" Margin="0,0,16,0" Foreground="DimGray"/>
            <Button Content="Xác nhận chọn" Width="120" Height="30" Margin="0,0,8,0" Click="BtnConfirm_Click" IsDefault="True"/>
            <Button Content="Hủy" Width="80" Height="30" IsCancel="True"/>
        </StackPanel>
    </Grid>
</Window>
```

Tạo file `src/BatchDeleteFTP/Views/RemoteFolderPickerWindow.xaml.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;

namespace BatchDeleteFTP.Views;

public partial class RemoteFolderPickerWindow : Window
{
    private readonly FtpSettings _settings;
    private readonly FtpCleanupService _ftpService;
    public ObservableCollection<FtpFolderNode> RootNodes { get; } = new();
    public List<string> SelectedPaths { get; private set; } = new();

    public RemoteFolderPickerWindow(FtpSettings settings, FtpCleanupService ftpService, IEnumerable<string> currentPaths)
    {
        InitializeComponent();
        _settings = settings;
        _ftpService = ftpService;

        var preselected = new HashSet<string>(
            FtpCleanupService.NormalizeRemotePaths(currentPaths),
            StringComparer.OrdinalIgnoreCase);

        var rootNode = new FtpFolderNode
        {
            Name = "Thư mục gốc",
            FullPath = "/",
            IsSelected = preselected.Contains("/")
        };
        rootNode.Children.Add(new FtpFolderNode { Name = "Đang tải...", IsPlaceholder = true });

        RootNodes.Add(rootNode);
        FolderTreeView.ItemsSource = RootNodes;

        Loaded += async (_, _) =>
        {
            await LoadChildrenAsync(rootNode, preselected);
            rootNode.IsExpanded = true;
        };
    }

    private async void FolderTreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem item && item.DataContext is FtpFolderNode node)
        {
            await LoadChildrenAsync(node, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private async System.Threading.Tasks.Task LoadChildrenAsync(FtpFolderNode parentNode, HashSet<string> preselected)
    {
        if (parentNode.ChildrenLoaded || parentNode.IsPlaceholder) return;

        try
        {
            StatusText.Text = $"Đang tải {parentNode.FullPath}...";
            var children = await _ftpService.GetSubDirectoriesAsync(_settings, parentNode.FullPath);
            parentNode.Children.Clear();
            foreach (var child in children)
            {
                if (preselected.Contains(child.FullPath))
                {
                    child.IsSelected = true;
                }
                parentNode.Children.Add(child);
            }
            parentNode.ChildrenLoaded = true;
            StatusText.Text = "Đã tải xong.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Lỗi tải thư mục.";
            MessageBox.Show($"Không thể tải thư mục '{parentNode.FullPath}':\n{ex.Message}", "Lỗi FTP", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        var collected = new List<string>();
        foreach (var node in RootNodes)
        {
            CollectSelectedNodes(node, collected);
        }

        if (collected.Count == 0)
        {
            MessageBox.Show("Vui lòng tích chọn ít nhất 1 thư mục!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedPaths = FtpCleanupService.NormalizeRemotePaths(collected).ToList();
        DialogResult = true;
        Close();
    }

    private static void CollectSelectedNodes(FtpFolderNode node, List<string> result)
    {
        if (node.IsPlaceholder) return;
        if (node.IsSelected)
        {
            result.Add(node.FullPath);
        }
        foreach (var child in node.Children)
        {
            CollectSelectedNodes(child, result);
        }
    }
}
```

- [ ] **Step 2: Thiết kế giao diện `MainWindow.xaml` và `MainWindow.xaml.cs`**

Cập nhật `src/BatchDeleteFTP/MainWindow.xaml`:
```xml
<Window x:Class="BatchDeleteFTP.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="BatchDeleteFTP - Quản lý Xóa &amp; Backup File FTP (Implicit TLS)" Height="720" Width="1020"
        WindowStartupLocation="CenterScreen">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="160"/>
        </Grid.RowDefinitions>

        <!-- Khung 1: Cấu hình FTP & Backup -->
        <GroupBox Header="1. Cấu hình Kết nối FTP &amp; Backup" Grid.Row="0" Padding="10" Margin="0,0,0,8">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="130"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="120"/>
                    <ColumnDefinition Width="220"/>
                </Grid.ColumnDefinitions>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <TextBlock Grid.Row="0" Grid.Column="0" Text="Host / Encryption:" VerticalAlignment="Center" Margin="0,4"/>
                <StackPanel Grid.Row="0" Grid.Column="1" Orientation="Horizontal" Margin="0,4">
                    <TextBox x:Name="TxtHost" Width="220" VerticalContentAlignment="Center" Margin="0,0,8,0"/>
                    <ComboBox x:Name="CboEncryption" Width="190" SelectedIndex="0" VerticalContentAlignment="Center">
                        <ComboBoxItem Content="Implicit (FTP over TLS)" Tag="Implicit"/>
                        <ComboBoxItem Content="Explicit (FTP over TLS)" Tag="Explicit"/>
                    </ComboBox>
                </StackPanel>

                <TextBlock Grid.Row="0" Grid.Column="2" Text="Port / Số ngày (+N):" VerticalAlignment="Center" Margin="8,4,0,4"/>
                <StackPanel Grid.Row="0" Grid.Column="3" Orientation="Horizontal" Margin="0,4">
                    <TextBox x:Name="TxtPort" Width="70" VerticalContentAlignment="Center" Margin="0,0,10,0"/>
                    <TextBlock Text="Upload +" VerticalAlignment="Center" Margin="0,0,6,0"/>
                    <TextBox x:Name="TxtRetentionDays" Width="50" VerticalContentAlignment="Center"/>
                </StackPanel>

                <TextBlock Grid.Row="1" Grid.Column="0" Text="User:" VerticalAlignment="Center" Margin="0,4"/>
                <TextBox x:Name="TxtUser" Grid.Row="1" Grid.Column="1" VerticalContentAlignment="Center" Margin="0,4,12,4"/>

                <TextBlock Grid.Row="1" Grid.Column="2" Text="Password:" VerticalAlignment="Center" Margin="8,4,0,4"/>
                <PasswordBox x:Name="TxtPassword" Grid.Row="1" Grid.Column="3" VerticalContentAlignment="Center" Margin="0,4"/>

                <TextBlock Grid.Row="2" Grid.Column="0" Text="Remote Paths (;):" VerticalAlignment="Center" Margin="0,4"/>
                <DockPanel Grid.Row="2" Grid.Column="1" Grid.ColumnSpan="3" Margin="0,4">
                    <Button x:Name="BtnBrowseRemote" DockPanel.Dock="Right" Content="Chọn thư mục FTP..." Width="150" Margin="8,0,0,0" Click="BtnBrowseRemote_Click"/>
                    <TextBox x:Name="TxtRemotePaths" VerticalContentAlignment="Center" ToolTip="Nhiều đường dẫn ngăn cách bởi dấu chấm phẩy (;), ví dụ: /FolderA;/FolderB"/>
                </DockPanel>

                <CheckBox x:Name="ChkEnableBackup" Grid.Row="3" Grid.Column="0" Content="Backup trước xóa" VerticalAlignment="Center" Margin="0,4"/>
                <DockPanel Grid.Row="3" Grid.Column="1" Grid.ColumnSpan="3" Margin="0,4">
                    <Button x:Name="BtnBrowseLocalBackup" DockPanel.Dock="Right" Content="Chọn thư mục Local..." Width="150" Margin="8,0,0,0" Click="BtnBrowseLocalBackup_Click"/>
                    <TextBox x:Name="TxtLocalBackupPath" VerticalContentAlignment="Center"/>
                </DockPanel>
            </Grid>
        </GroupBox>

        <!-- Khung 2: Thanh công cụ điều khiển -->
        <DockPanel Grid.Row="1" Margin="0,0,0,8">
            <StackPanel Orientation="Horizontal" DockPanel.Dock="Left">
                <Button x:Name="BtnSaveConfig" Content="💾 Lưu Cấu Hình" Width="130" Height="32" Margin="0,0,8,0" Click="BtnSaveConfig_Click"/>
                <Button x:Name="BtnTestConnection" Content="🔌 Kiểm Tra Kết Nối" Width="145" Height="32" Margin="0,0,8,0" Click="BtnTestConnection_Click"/>
                <Button x:Name="BtnPreview" Content="🔍 Xem Trước (Preview)" Width="165" Height="32" Margin="0,0,8,0" FontWeight="SemiBold" Click="BtnPreview_Click"/>
                <Button x:Name="BtnExecuteDelete" Content="🗑 Thực Hiện Backup &amp; Xóa" Width="195" Height="32" FontWeight="Bold" Foreground="DarkRed" Click="BtnExecuteDelete_Click"/>
            </StackPanel>

            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                <Button Content="Chọn tất cả" Width="90" Height="28" Margin="0,0,6,0" Click="BtnSelectAll_Click"/>
                <Button Content="Bỏ chọn tất cả" Width="100" Height="28" Click="BtnUnselectAll_Click"/>
            </StackPanel>
        </DockPanel>

        <!-- Khung 3: Danh sách file Xem Trước (DataGrid) -->
        <GroupBox Header="2. Danh sách File Thỏa Điều Kiện Xóa (SystemDate.Date &gt; UploadDate.Date + N)" Grid.Row="2" Margin="0,0,0,8">
            <DataGrid x:Name="GridCandidates" AutoGenerateColumns="False" CanUserAddRows="False" IsReadOnly="False">
                <DataGrid.Columns>
                    <DataGridCheckBoxColumn Header="Chọn xóa" Binding="{Binding IsSelected, UpdateSourceTrigger=PropertyChanged}" Width="75"/>
                    <DataGridTextColumn Header="Đường dẫn FTP (Remote Path)" Binding="{Binding FullPath}" Width="*" IsReadOnly="True"/>
                    <DataGridTextColumn Header="Ngày Upload (Modified)" Binding="{Binding ModifiedDate, StringFormat=yyyy-MM-dd HH:mm:ss}" Width="155" IsReadOnly="True"/>
                    <DataGridTextColumn Header="Dung lượng (Bytes)" Binding="{Binding Size, StringFormat={}{0:N0}}" Width="125" IsReadOnly="True"/>
                    <DataGridTextColumn Header="Trạng thái" Binding="{Binding Status}" Width="180" IsReadOnly="True"/>
                </DataGrid.Columns>
            </DataGrid>
        </GroupBox>

        <!-- Khung 4: Nhật ký hoạt động (Log) -->
        <GroupBox Header="3. Nhật ký hoạt động (Log)" Grid.Row="3">
            <TextBox x:Name="TxtLogs" IsReadOnly="True" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto"
                     FontFamily="Consolas" FontSize="12" Background="#1E1E1E" Foreground="#D4D4D4"/>
        </GroupBox>
    </Grid>
</Window>
```

Cập nhật `src/BatchDeleteFTP/MainWindow.xaml.cs`:
```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BatchDeleteFTP.Models;
using BatchDeleteFTP.Services;
using BatchDeleteFTP.Views;
using Microsoft.Win32;

namespace BatchDeleteFTP;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService;
    private readonly AppLogger _logger;
    private readonly FtpCleanupService _ftpService;
    private readonly ObservableCollection<FtpFileCandidate> _candidates = new();

    public MainWindow(ConfigService configService, AppLogger logger, FtpCleanupService ftpService)
    {
        InitializeComponent();
        _configService = configService;
        _logger = logger;
        _ftpService = ftpService;

        GridCandidates.ItemsSource = _candidates;

        _logger.OnLogMessage += line =>
        {
            Dispatcher.Invoke(() =>
            {
                TxtLogs.AppendText(line + Environment.NewLine);
                TxtLogs.ScrollToEnd();
            });
        };

        LoadSettingsToUi(_configService.Load());
    }

    private void LoadSettingsToUi(FtpSettings s)
    {
        TxtHost.Text = s.Host;
        TxtPort.Text = s.Port.ToString();
        CboEncryption.SelectedIndex = string.Equals(s.EncryptionMode, "Explicit", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        TxtUser.Text = s.Username;
        TxtPassword.Password = s.Password;
        TxtRemotePaths.Text = string.Join(";", s.RemotePaths);
        TxtRetentionDays.Text = s.RetentionDays.ToString();
        ChkEnableBackup.IsChecked = s.EnableBackup;
        TxtLocalBackupPath.Text = s.LocalBackupPath;
    }

    private FtpSettings ReadSettingsFromUi()
    {
        int.TryParse(TxtPort.Text, out int port);
        if (port <= 0) port = 6021;

        int.TryParse(TxtRetentionDays.Text, out int retentionDays);
        if (retentionDays < 0) retentionDays = 1;

        string encTag = (CboEncryption.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Implicit";

        var paths = (TxtRemotePaths.Text ?? "/")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return new FtpSettings
        {
            Host = TxtHost.Text.Trim(),
            Port = port,
            EncryptionMode = encTag,
            Username = TxtUser.Text.Trim(),
            Password = TxtPassword.Password,
            RemotePaths = FtpCleanupService.NormalizeRemotePaths(paths).ToList(),
            RetentionDays = retentionDays,
            EnableBackup = ChkEnableBackup.IsChecked == true,
            LocalBackupPath = TxtLocalBackupPath.Text.Trim()
        };
    }

    private void SetUiBusy(bool isBusy)
    {
        BtnSaveConfig.IsEnabled = !isBusy;
        BtnTestConnection.IsEnabled = !isBusy;
        BtnPreview.IsEnabled = !isBusy;
        BtnExecuteDelete.IsEnabled = !isBusy;
        BtnBrowseRemote.IsEnabled = !isBusy;
    }

    private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadSettingsFromUi();
        _configService.Save(settings);
        TxtRemotePaths.Text = string.Join(";", settings.RemotePaths);
        _logger.Info($"Đã lưu cấu hình vào '{_configService.DefaultConfigPath}'.");
        MessageBox.Show("Đã lưu cấu hình thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
    {
        SetUiBusy(true);
        try
        {
            var settings = ReadSettingsFromUi();
            await _ftpService.TestConnectionAsync(settings);
            MessageBox.Show("Kết nối tới FTP Server thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error($"Kết nối thất bại: {ex.Message}");
            MessageBox.Show($"Kết nối thất bại:\n{ex.Message}", "Lỗi Kết Nối", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetUiBusy(false);
        }
    }

    private void BtnBrowseRemote_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadSettingsFromUi();
        var picker = new RemoteFolderPickerWindow(settings, _ftpService, settings.RemotePaths)
        {
            Owner = this
        };

        if (picker.ShowDialog() == true)
        {
            TxtRemotePaths.Text = string.Join(";", picker.SelectedPaths);
            _logger.Info($"Đã chọn Remote Paths: {TxtRemotePaths.Text}");
        }
    }

    private void BtnBrowseLocalBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục lưu file Backup trên máy tính"
        };

        if (dialog.ShowDialog(this) == true)
        {
            TxtLocalBackupPath.Text = dialog.FolderName;
        }
    }

    private async void BtnPreview_Click(object sender, RoutedEventArgs e)
    {
        SetUiBusy(true);
        try
        {
            var settings = ReadSettingsFromUi();
            _candidates.Clear();
            var list = await _ftpService.ScanCandidatesAsync(settings, DateTime.Now);
            foreach (var item in list)
            {
                _candidates.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi Xem Trước: {ex.Message}");
            MessageBox.Show($"Lỗi khi quét file trên FTP:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetUiBusy(false);
        }
    }

    private async void BtnExecuteDelete_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadSettingsFromUi();
        SetUiBusy(true);
        try
        {
            if (_candidates.Count == 0)
            {
                var scanned = await _ftpService.ScanCandidatesAsync(settings, DateTime.Now);
                foreach (var item in scanned)
                {
                    _candidates.Add(item);
                }
            }

            int selectedCount = _candidates.Count(c => c.IsSelected);
            if (selectedCount == 0)
            {
                MessageBox.Show("Không có file nào thỏa điều kiện hoặc được chọn để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string actionDesc = settings.EnableBackup
                ? $"tải Backup về '{settings.LocalBackupPath}' và XÓA {selectedCount} file"
                : $"XÓA VĨNH VIỄN {selectedCount} file (KHÔNG Backup)";

            var confirm = MessageBox.Show(
                $"Bạn có chắc chắn muốn {actionDesc} trên FTP Server không?",
                "Xác nhận xóa file",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            var (deleted, failed) = await _ftpService.ExecuteBackupAndDeleteAsync(settings, _candidates);
            MessageBox.Show($"Hoàn tất xử lý!\n- Đã xóa: {deleted} file\n- Lỗi: {failed} file", "Kết quả", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi thực thi xóa: {ex.Message}");
            MessageBox.Show($"Lỗi khi xóa file:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetUiBusy(false);
        }
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _candidates) item.IsSelected = true;
    }

    private void BtnUnselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _candidates) item.IsSelected = false;
    }
}
```

- [ ] **Step 3: Chạy `dotnet build` để xác nhận giao diện WPF biên dịch không lỗi**

Run: `dotnet build`
Expected: Build succeeded (0 Errors).

---

### Task 5: Tích hợp Chế độ Hybrid Console (`--auto`, `--console`, `--preview`) trong `App.xaml.cs`

**Files:**
- Create: `src/BatchDeleteFTP/Helpers/NativeConsole.cs`
- Modify: `src/BatchDeleteFTP/App.xaml`
- Modify: `src/BatchDeleteFTP/App.xaml.cs`
- Create: `tests/BatchDeleteFTP.Tests/CliArgsParserTests.cs`

**Interfaces:**
- Consumes: `ConfigService`, `AppLogger`, `FtpCleanupService`, `MainWindow`
- Produces:
  - `BatchDeleteFTP.Helpers.CliOptions.Parse(string[] args) -> CliOptions`
  - Hỗ trợ chạy `BatchDeleteFTP.exe` (mở WPF), `BatchDeleteFTP.exe --auto` / `--console` (chạy tự động Backup & Xóa từ CMD), `BatchDeleteFTP.exe --preview` (chỉ quét và in danh sách ra CMD).

- [ ] **Step 1: Viết Unit Test cho bộ phân tích tham số dòng lệnh `CliOptions.Parse` (Failing Test)**

Tạo file `tests/BatchDeleteFTP.Tests/CliArgsParserTests.cs`:
```csharp
using BatchDeleteFTP.Helpers;
using Xunit;

namespace BatchDeleteFTP.Tests;

public class CliArgsParserTests
{
    [Fact]
    public void Parse_EmptyArgs_RunsGuiMode()
    {
        var opts = CliOptions.Parse(System.Array.Empty<string>());
        Assert.False(opts.IsConsoleMode);
        Assert.False(opts.IsPreviewOnly);
    }

    [Theory]
    [InlineData("--auto")]
    [InlineData("--console")]
    [InlineData("/auto")]
    public void Parse_AutoFlag_EnablesConsoleExecutionMode(string arg)
    {
        var opts = CliOptions.Parse(new[] { arg });
        Assert.True(opts.IsConsoleMode);
        Assert.False(opts.IsPreviewOnly);
    }

    [Fact]
    public void Parse_PreviewFlag_EnablesConsolePreviewMode()
    {
        var opts = CliOptions.Parse(new[] { "--preview" });
        Assert.True(opts.IsConsoleMode);
        Assert.True(opts.IsPreviewOnly);
    }
}
```

- [ ] **Step 2: Chạy `dotnet test` để xác nhận lỗi biên dịch (Red)**

Run: `dotnet test`
Expected: FAIL.

- [ ] **Step 3: Cài đặt `NativeConsole`, `CliOptions` và cập nhật `App.xaml` + `App.xaml.cs` (Green)**

Tạo file `src/BatchDeleteFTP/Helpers/NativeConsole.cs`:
```csharp
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace BatchDeleteFTP.Helpers;

public class CliOptions
{
    public bool IsConsoleMode { get; init; }
    public bool IsPreviewOnly { get; init; }

    public static CliOptions Parse(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return new CliOptions { IsConsoleMode = false, IsPreviewOnly = false };
        }

        bool isPreview = args.Any(a =>
            a.Equals("--preview", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/preview", StringComparison.OrdinalIgnoreCase));

        bool isAuto = args.Any(a =>
            a.Equals("--auto", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--console", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/auto", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/console", StringComparison.OrdinalIgnoreCase));

        return new CliOptions
        {
            IsConsoleMode = isAuto || isPreview,
            IsPreviewOnly = isPreview
        };
    }
}

public static class NativeConsole
{
    private const int ATTACH_PARENT_PROCESS = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeConsole();

    public static void AttachOrCreateConsole()
    {
        if (!AttachConsole(ATTACH_PARENT_PROCESS))
        {
            AllocConsole();
        }
    }
}
```

Cập nhật `src/BatchDeleteFTP/App.xaml` (bỏ `StartupUri` để điều khiển trong `OnStartup`):
```xml
<Application x:Class="BatchDeleteFTP.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
    </Application.Resources>
</Application>
```

Cập nhật `src/BatchDeleteFTP/App.xaml.cs`:
```csharp
using System;
using System.Windows;
using BatchDeleteFTP.Helpers;
using BatchDeleteFTP.Services;

namespace BatchDeleteFTP;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configService = new ConfigService();
        var logger = new AppLogger();
        var ftpService = new FtpCleanupService(logger);
        var cliOptions = CliOptions.Parse(e.Args);

        if (cliOptions.IsConsoleMode)
        {
            NativeConsole.AttachOrCreateConsole();
            int exitCode = 0;
            try
            {
                var settings = configService.Load();
                logger.Info($"Chạy chế độ Console ({(cliOptions.IsPreviewOnly ? "PREVIEW ONLY" : "AUTO BACKUP & DELETE")}).");
                var candidates = await ftpService.ScanCandidatesAsync(settings, DateTime.Now);

                if (cliOptions.IsPreviewOnly)
                {
                    foreach (var c in candidates)
                    {
                        logger.Info($"[PREVIEW] Sẽ xóa: {c.FullPath} | Upload: {c.ModifiedDate:yyyy-MM-dd HH:mm:ss} | Size: {c.Size} bytes");
                    }
                }
                else
                {
                    var (_, failedCount) = await ftpService.ExecuteBackupAndDeleteAsync(settings, candidates);
                    if (failedCount > 0) exitCode = 1;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Lỗi nghiêm trọng trong chế độ Console: {ex.Message}");
                exitCode = 1;
            }
            finally
            {
                NativeConsole.FreeConsole();
                Shutdown(exitCode);
            }
            return;
        }

        var mainWindow = new MainWindow(configService, logger, ftpService);
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
```

- [ ] **Step 4: Chạy `dotnet test` để xác minh toàn bộ Unit Test và Build thành công**

Run: `dotnet test`
Expected: PASS (9/9 tests passed, 0 errors).
