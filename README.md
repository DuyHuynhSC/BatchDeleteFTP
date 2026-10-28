# BatchDeleteFTP

Ứng dụng C# WPF kết hợp chế độ dòng lệnh (Console / CMD Hybrid) hỗ trợ kết nối tới **FTP over TLS (Implicit / Explicit)**, duyệt và chọn các thư mục từ xa (`Remote Path`), **Xem trước (Preview)** danh sách các file thỏa điều kiện xóa, **Backup** file về máy tính giữ nguyên cấu trúc thư mục con trước khi xóa, và tự động dọn dẹp các file cũ theo công thức:

```text
SystemDate.Date > UploadDate.Date + RetentionDays (Mặc định RetentionDays = 1)
```

---

## 1. Tính năng Chính

- **Hỗ trợ chuẩn FTPS & Proxy nội bộ công ty:** Mặc định cấu hình sẵn cho `ftp.fujinet.net` (Port `6021`, `Implicit FTP over TLS`, User `UCD-MOROZOFF`), tích hợp hỗ trợ kết nối qua HTTP/HTTPS Proxy công ty (`prox2.fujinet.vn:8080`) thông qua đường hầm HTTP 1.1 CONNECT của `FluentFTP`.
- **Chọn Thư mục Từ xa trên FTP (`Remote Folder Picker`):**
  - Cho phép nhập một hoặc nhiều đường dẫn ngăn cách bởi dấu `;` (ví dụ: `/FolderA;/FolderB/Sub1`) hoặc bấm **"Chọn thư mục FTP..."** để mở cây thư mục (`TreeView` lazy-load) trực tiếp từ FTP Server và tích chọn nhiều thư mục cần quét.
  - Tự động chuẩn hóa và khử trùng lặp đường dẫn (nếu chọn cả thư mục cha và thư mục con, ứng dụng tự động gộp để không quét lặp).
- **Xem Trước (`Preview`):**
  - Quét đệ quy toàn bộ thư mục và sub-folder được chọn, lọc các file có `CurrentSystemDate.Date > FileModifiedDate.Date.AddDays(RetentionDays)` và hiển thị lên bảng `DataGrid` mà không xóa file.
  - Hỗ trợ tích chọn / bỏ chọn từng file cụ thể hoặc **Chọn tất cả / Bỏ chọn tất cả** trước khi thực thi.
- **Backup An toàn Trước Khi Xóa (`Fail-safe Backup`):**
  - Khi bật tùy chọn **Backup trước xóa**, ứng dụng tải từng file về thư mục Local theo cấu trúc:
    `<LocalBackupPath>\<yyyyMMdd_HHmmss>\<RelativeSubFolder>\<FileName>`
  - **Cơ chế bảo vệ dữ liệu:** Chỉ khi file được tải về máy thành công và tồn tại đầy đủ trên ổ cứng thì ứng dụng mới gửi lệnh xóa file đó trên FTP Server. Nếu tải backup lỗi, file trên FTP được giữ nguyên.
  - Chỉ xóa file, giữ nguyên toàn bộ cấu trúc thư mục trên FTP Server.
- **Chế độ Kép (WPF GUI & Console CMD):**
  - Chạy trực tiếp file `.exe` để mở giao diện đồ họa WPF.
  - Truyền tham số `--auto`, `--console` hoặc `--preview` để chạy tự động trong CMD / Windows Task Scheduler.

---

## 2. Cấu hình (`config.json`)

File `config.json` được tự động khởi tạo và lưu cùng thư mục với file thực thi `BatchDeleteFTP.exe`:

```json
{
  "Host": "ftp.fujinet.net",
  "Port": 6021,
  "EncryptionMode": "Implicit",
  "Username": "UCD-MOROZOFF",
  "Password": "",
  "UseProxy": true,
  "ProxyHost": "prox2.fujinet.vn",
  "ProxyPort": 8080,
  "ProxyUser": "",
  "ProxyPassword": "",
  "RemotePaths": [
    "/"
  ],
  "RetentionDays": 1,
  "EnableBackup": true,
  "LocalBackupPath": "C:\\SourceCode\\BatchDeleteFTP\\Backups"
}
```

---

## 3. Hướng dẫn Sử dụng

### 3.1. Chế độ Giao diện (WPF GUI)
Chạy trực tiếp `BatchDeleteFTP.exe` (không truyền tham số):
1. Nhập `Password` và kiểm tra các thông số kết nối (`Host`, `Port`, `Encryption`, `User`).
2. Bấm **🔌 Kiểm Tra Kết Nối** để xác nhận kết nối FTPS thành công.
3. Bấm **Chọn thư mục FTP...** để duyệt cây thư mục trên FTP và tích chọn các thư mục cần dọn dẹp.
4. Cấu hình thư mục **Backup Local** và bấm **💾 Lưu Cấu Hình**.
5. Bấm **🔍 Xem Trước (Preview)** để kiểm tra danh sách các file sẽ bị xóa.
6. Bấm **🗑 Thực Hiện Backup & Xóa** để tiến hành sao lưu và xóa file.

### 3.2. Chế độ Dòng lệnh (Console / CMD / Windows Task Scheduler)

- **Chạy Xem Trước trên CMD (chỉ liệt kê file sẽ xóa, không xóa thật):**
  ```cmd
  BatchDeleteFTP.exe --preview
  ```
- **Chạy Tự động Backup & Xóa (dùng cho lịch tự động Windows Task Scheduler):**
  ```cmd
  BatchDeleteFTP.exe --auto
  ```
  *(Hoặc `BatchDeleteFTP.exe --console`)*

> **Mã thoát (Exit Code):**
> - `0`: Quá trình chạy hoàn tất thành công, không có lỗi.
> - `1`: Có lỗi kết nối hoặc có ít nhất 1 file gặp lỗi khi Backup/Xóa.

### 3.3. Nhật ký Hoạt động (Logs)
Toàn bộ lịch sử kết nối, danh sách file quét được, đường dẫn file đã Backup và kết quả xóa đều được ghi tự động vào thư mục:
```text
logs/BatchDeleteFTP_yyyyMMdd.log
```

---

## 4. Hướng dẫn Build & Kiểm thử (Development)

- **Biên dịch dự án (Release):**
  ```powershell
  dotnet build -c Release
  ```
- **Chạy toàn bộ Unit Tests (xUnit):**
  ```powershell
  dotnet test
  ```
