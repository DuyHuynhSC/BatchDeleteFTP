# Tài liệu Đặc tả Thiết kế: BatchDeleteFTP (C# .NET 8 WPF + Console Hybrid)

**Ngày tạo:** 2026-10-28  
**Trạng thái:** Đã duyệt thiết kế  

---

## 1. Tổng quan & Mục tiêu
Xây dựng ứng dụng desktop C# WPF (.NET 8) có khả năng chạy ở 2 chế độ:
1. **Chế độ Giao diện (WPF GUI):** Cấu hình kết nối FTP, duyệt cây thư mục trên FTP để chọn một hoặc nhiều `Remote Path` cần quét, **Xem trước (Preview)** danh sách các file thỏa điều kiện xóa, chọn/bỏ chọn từng file, **Backup** (tải file về thư mục local giữ nguyên cấu trúc thư mục) trước khi xóa, và thực thi xóa kèm theo dõi log trực tiếp.
2. **Chế độ Dòng lệnh (Console / CMD):** Chạy tự động qua tham số `--auto` (hoặc `--console` / `--preview`) để tích hợp vào Windows Task Scheduler hoặc script, đọc cấu hình từ `config.json`, thực hiện quét, backup và xóa file thỏa điều kiện rồi tự thoát.

---

## 2. Thông số Kết nối FTP Mặc định & Nghiệp vụ
- **Protocol:** FTP - File Transfer Protocol (Sử dụng thư viện `FluentFTP`).
- **Host:** `ftp.fujinet.net`
- **Port:** `6021`
- **Encryption:** `Require implicit FTP over TLS` (`FtpEncryptionMode.Implicit`, chấp nhận chứng chỉ SSL tự ký `ValidateAnyCertificate = true`).
- **Logon Type:** Normal (`Username`: `UCD-MOROZOFF`, `Password`: Lưu mã hóa DPAPI/Base64 hoặc plain-text tùy chỉnh trong `config.json`).
- **Điều kiện xóa file:**
  - Duyệt đệ quy tất cả các file trong danh sách `RemotePaths` đã chọn và toàn bộ các `sub-folder` bên trong.
  - Công thức: `CurrentSystemDate.Date > FileModifiedDate.Date.AddDays(RetentionDays)` (Mặc định `RetentionDays = 1`).
  - Ví dụ: Nếu ngày hệ thống hiện tại là `2026-10-28`, thì các file có ngày upload/modified từ `2026-10-26` trở về trước (`2026-10-26 + 1 ngày = 2026-10-27 < 2026-10-28`) sẽ được đưa vào danh sách xóa.
  - Chỉ xóa file, giữ nguyên các thư mục trên FTP.

---

## 3. Kiến trúc Module & Thành phần

```
BatchDeleteFTP.sln
├── src/BatchDeleteFTP/                  # Project chính (.NET 8 WPF, OutputType: WinExe)
│   ├── Models/
│   │   ├── FtpSettings.cs               # Cấu hình FTP, RemotePaths, Backup, RetentionDays
│   │   ├── FtpFileCandidate.cs          # Thông tin file quét được (IsSelected, FullPath, Size, ModifiedDate, Status)
│   │   └── FtpFolderNode.cs             # Node cho TreeView chọn thư mục từ xa trên FTP
│   ├── Services/
│   │   ├── IConfigService.cs / ConfigService.cs             # Đọc/ghi config.json + mã hóa mật khẩu
│   │   ├── IFtpCleanupService.cs / FtpCleanupService.cs     # Kết nối FluentFTP, lấy thư mục, Preview, Backup & Delete
│   │   └── IAppLogger.cs / AppLogger.cs                     # Ghi log ra File (logs/BatchDeleteFTP_yyyyMMdd.log), Console, và UI
│   ├── Views/
│   │   ├── MainWindow.xaml / .cs        # Giao diện chính (Cấu hình, Preview DataGrid, Log, Thực thi)
│   │   └── RemoteFolderPickerWindow.xaml / .cs # Cửa sổ TreeView duyệt & chọn nhiều thư mục trên FTP
│   ├── Helpers/
│   │   ├── NativeConsole.cs             # P/Invoke AttachConsole(-1) và FreeConsole() cho chế độ CMD
│   │   └── DateFilterRule.cs            # Logic thuần túy kiểm tra điều kiện ngày xóa (dễ dàng Unit Test)
│   └── App.xaml / App.xaml.cs           # Điểm khởi chạy phân luồng GUI vs Console (--auto, --preview)
└── tests/BatchDeleteFTP.Tests/          # Project Unit Test (xUnit)
    ├── DateFilterRuleTests.cs           # Kiểm thử công thức SystemDate.Date > UploadDate.Date.AddDays(1)
    ├── ConfigServiceTests.cs            # Kiểm thử đọc/ghi cấu hình và danh sách RemotePaths
    └── BackupPathBuilderTests.cs        # Kiểm thử tạo đường dẫn backup bảo toàn cấu trúc sub-folder
```

---

## 4. Chi tiết các Tính năng Trọng tâm

### 4.1. Chọn Remote Path cần xóa (Remote Folder Picker)
- Trong cấu hình và trên giao diện chính, người dùng có thể nhập danh sách đường dẫn (ngăn cách bởi dấu `;` hoặc danh sách) hoặc bấm nút **"Chọn thư mục FTP..."**.
- Cửa sổ `RemoteFolderPickerWindow` kết nối tới FTP, tải danh sách thư mục gốc `/` và cho phép mở rộng (lazy-load) các thư mục con.
- Mỗi thư mục có Checkbox để người dùng chọn một hoặc nhiều thư mục cần quét xóa.
- Tránh quét trùng lặp: Nếu cả thư mục cha `/FolderA` và thư mục con `/FolderA/Sub` đều được chọn, hệ thống tự động gộp hoặc khử trùng lặp theo `FullPath` của file.

### 4.2. Tính năng Xem Trước (Preview)
- Khi bấm **"Xem Trước (Preview)"** trên GUI (hoặc chạy `BatchDeleteFTP.exe --preview` trên CMD):
  - Kết nối tới FTP, duyệt đệ quy các `RemotePaths` đã chọn.
  - Áp dụng `DateFilterRule.ShouldDelete(file.Modified, DateTime.Now, settings.RetentionDays)`.
  - Hiển thị danh sách các file thỏa điều kiện lên bảng `DataGrid` với cột Checkbox `Chọn xóa` (mặc định tích chọn `true`), Đường dẫn FTP, Ngày Upload, Dung lượng, và Trạng thái.
  - Người dùng có thể bỏ tích những file muốn giữ lại trước khi bấm **"Thực hiện Xóa"**.

### 4.3. Tính năng Backup trước khi xóa
- Nếu tùy chọn `EnableBackup = true`:
  - Trước khi xóa một file trên FTP (ví dụ `/DATA/2026/report.csv`), ứng dụng sẽ tải file đó về máy local tại đường dẫn:
    `<LocalBackupPath>\<yyyyMMdd_HHmmss>\DATA\2026\report.csv`
  - **Cơ chế an toàn (Fail-safe):** Chỉ khi `DownloadFile` từ `FluentFTP` trả về `FtpStatus.Success` và file tồn tại trên ổ cứng local thì mới gọi `DeleteFile` trên FTP. Nếu tải backup thất bại, file trên FTP sẽ **không bị xóa**, trạng thái dòng đó báo lỗi và ghi vào file log.

### 4.4. Chế độ Console (CMD)
- Hỗ trợ các tham số dòng lệnh:
  - `BatchDeleteFTP.exe` (không tham số): Mở giao diện WPF.
  - `BatchDeleteFTP.exe --auto` hoặc `--console`: Chạy tự động theo cấu hình trong `config.json` (quét -> backup nếu bật -> xóa -> ghi log -> thoát với mã `0` hoặc `1`).
  - `BatchDeleteFTP.exe --preview`: Chạy ở chế độ Console nhưng chỉ liệt kê các file sẽ bị xóa ra màn hình CMD và file log mà không xóa thật.
