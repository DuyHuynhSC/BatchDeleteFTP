# BatchDeleteFTP

Ứng dụng C# .NET WPF kết hợp chế độ dòng lệnh (Console / CMD Hybrid) hỗ trợ kết nối tới **FTP over TLS (Implicit / Explicit)**, quản lý nhiều cấu hình kết nối (**Site / Profile Manager**), quét đồng thời nhiều Profile (**Multi-Profile Batch Scan & Delete**), duyệt và chọn các thư mục từ xa (`Remote Path`), **Xem trước (Preview)** danh sách các file thỏa điều kiện xóa, **Gửi email báo cáo kết quả xem trước** qua giao thức SMTP, **Backup** file về máy tính giữ nguyên cấu trúc thư mục con trước khi xóa, tích hợp đầy đủ trình quản lý file FTP (**FTP File Manager**) và hỗ trợ giao diện **Sáng / Tối (Dark / Light Theme)** theo chuẩn Material Design.

Công thức dọn dẹp file tự động:
```text
SystemDate.Date > UploadDate.Date + RetentionDays (Mặc định RetentionDays = 1)
```

---

## 1. Tính năng Chính

### 1.1. Tự Động Quét & Xóa File (Batch Clean & Backup)
- **Quét đồng thời nhiều Profile (`Multi-Profile Scan`):**
  - Cho phép người dùng linh hoạt chọn quét trên **Profile hiện tại**, **Tất cả các Profile**, hoặc **Tùy chọn danh sách Profile cụ thể**.
  - Kết quả quét tổng hợp hiển thị rõ cột **Site Profile**, đường dẫn remote, ngày upload, kích thước và trạng thái.
  - Quá trình quét được cách ly độc lập theo từng profile: nếu một máy chủ gặp sự cố kết nối hoặc quá thời gian chờ, hệ thống vẫn tiếp tục quét các profile còn lại và cảnh báo chi tiết.
- **Thực thi Backup & Xóa đa máy chủ FTP (`Fail-safe Multi-Server Execution`):**
  - Tự động gom nhóm các file được chọn theo từng Site Profile tương ứng.
  - Kết nối tuần tự tới từng máy chủ FTP với các cấu hình riêng biệt (Proxy, cổng, TLS, thông tin xác thực).
  - **Cơ chế bảo vệ dữ liệu:** Nếu bật Backup, ứng dụng sẽ tải file về máy tính trước theo cấu trúc:
    `<LocalBackupPath>\<yyyyMMdd_HHmmss>\<RelativeSubFolder>\<FileName>`
    Chỉ khi file được sao lưu thành công và tồn tại trọn vẹn trên ổ cứng cục bộ thì ứng dụng mới gửi lệnh xóa file đó trên FTP Server.
  - Chỉ xóa file, giữ nguyên toàn bộ cấu trúc thư mục trên FTP Server.

### 1.2. Quản Lý Nhiều Cấu Hình Kết Nối (Site / Profile Manager)
- Cho phép lưu trữ và chuyển đổi nhanh giữa nhiều máy chủ / tài khoản FTP khác nhau (tương tự *Site Manager* của FileZilla).
- Hỗ trợ các thao tác **➕ Tạo Mới**, **💾 Lưu Profile**, **🗑 Xóa Profile** trực quan ngay trên thanh tiêu đề ứng dụng.
- Cửa sổ **Cấu hình FTP & Backup** chi tiết hỗ trợ kiểm tra kết nối trực tiếp, chọn thư mục backup cục bộ và thiết lập proxy riêng.

### 1.3. Hỗ Trợ Chuẩn FTPS (TLS 1.2 / 1.3) & Proxy Doanh Nghiệp
- Hỗ trợ cả 2 chế độ mã hóa: `Implicit FTP over TLS` (Port `6021` / `990`) và `Explicit FTP over TLS` (Port `21`).
- Hỗ trợ kết nối qua HTTP/HTTPS Proxy (ví dụ `proxy.xxx.vn:8080`) kèm xác thực `Proxy User / Password` thông qua đường hầm HTTP 1.1 CONNECT của `FluentFTP`.
- Tối ưu hóa bắt tay TLS 1.2 / TLS 1.3 với chế độ truyền dữ liệu thụ động `PASV` và ghi log chi tiết thời gian thực.

### 1.4. Chọn Thư Mục Từ Xa Trên FTP (Remote Folder Picker)
- Hỗ trợ nhập thủ công danh sách đường dẫn cách nhau bởi dấu `;` (ví dụ: `/FolderA;/FolderB/Sub1`).
- Nút **"Chọn thư mục FTP..."** mở cửa sổ cây thư mục (`TreeView` lazy-load) trực tiếp từ FTP Server, hỗ trợ mở rộng từng cấp và tích chọn nhiều thư mục cần dọn dẹp.
- Tự động chuẩn hóa và khử trùng lặp đường dẫn (nếu chọn cả thư mục cha và thư mục con, ứng dụng tự động gộp để tránh quét trùng lặp).

### 1.5. Gửi Email Báo Cáo Xem Trước (Email Notification Service)
- Thiết lập thông số máy chủ gửi thư Outgoing Mail Server (SMTP - Port 25 / 587) có hoặc không có SSL/TLS.
- Tự động phân tích và suy luận địa chỉ người gửi theo cấu trúc `username@domain` dựa trên tên máy chủ SMTP (ví dụ: `duy-hd` + `mail.fujinet.net` -> `duy-hd@fujinet.net`).
- Hỗ trợ nhập danh sách nhiều email nhận (ngăn cách bởi dấu chấm phẩy `;`, dấu phẩy `,` hoặc xuống dòng), tự động khử trùng lặp và xác thực cú pháp email.
- Tạo báo cáo định dạng HTML chuyên nghiệp: tóm tắt thông số cấu hình, phạm vi các profile đã quét, tổng số file, dung lượng ước tính và bảng chi tiết từng file.
- Tùy chọn **Tự động gửi email sau khi Preview** (`AutoSendEmailOnPreview`).

### 1.6. Quản Lý File FTP Trực Quan (Tab File Manager)
- **Thanh điều hướng Remote Path:** Gõ trực tiếp đường dẫn thư mục FTP và nhấn Enter hoặc bấm **Đến**. Hỗ trợ các nút tắt: **Lên 1 Cấp**, **Về Gốc (/)**, và **Làm Mới**.
- **Xem file (`Xem File`):** Tích hợp trình xem trước hỗ trợ xem trực tiếp các định dạng text, log, code (`.txt`, `.log`, `.csv`, `.json`, `.xml`, `.cs`, `.py`...), hiển thị hình ảnh (`.png`, `.jpg`, `.jpeg`, `.gif`, `.bmp`, `.ico`) hoặc mở bằng ứng dụng mặc định của Windows.
- **Tải lên (`Upload`):** Hỗ trợ **Tải Lên File** (chọn một hoặc nhiều file) và **Tải Lên Thư Mục** (giữ nguyên cấu trúc thư mục con).
- **Tải về (`Download`):** Tải các file hoặc thư mục được chọn từ FTP về thư mục lưu trữ trên máy tính.
- **Tạo thư mục mới:** Tạo nhanh folder mới ngay tại đường dẫn remote hiện tại.
- **Xóa mục trên FTP:** Hỗ trợ chọn nhiều file / thư mục và xóa trực tiếp với hộp thoại xác nhận an toàn.
- **Lọc nhanh file:** Ô tìm kiếm tức thì trên thanh công cụ hỗ trợ lọc nhanh theo tên file trong danh sách hiển thị.

### 1.7. Giao Diện Material Design & Chuyển Đổi Dark/Light Theme
- Giao diện người dùng hiện đại, đồng bộ màu sắc thương hiệu và icon theo chuẩn Google Material Design.
- Nút gạt chuyển đổi giao diện **Sáng / Tối (Light / Dark Theme)** góc trên bên phải, tự động lưu và áp dụng cho toàn bộ các cửa sổ của chương trình.

### 1.8. Chế Độ Kép (GUI & Chạy Tự Động Qua Command Line)
- Chạy trực tiếp `BatchDeleteFTP.exe` để mở giao diện đồ họa WPF.
- Hỗ trợ các tham số dòng lệnh `--auto`, `--console`, `--preview` phục vụ tích hợp với **Windows Task Scheduler** hoặc batch script để tự động hóa hàng ngày.

---

## 2. Cấu Trúc File Cấu Hình (`config.json`)

File `config.json` tự động được khởi tạo và lưu trữ cùng thư mục với file thực thi `BatchDeleteFTP.exe`:

```json
{
  "SelectedProfileName": "SITE_A",
  "SelectedScanProfileNames": [
    "SITE_A"
  ],
  "Host": "ftp.xxx.net",
  "Port": 6021,
  "EncryptionMode": "Implicit",
  "Username": "XXX-USER",
  "Password": "",
  "UseProxy": true,
  "ProxyHost": "proxy.xxx.vn",
  "ProxyPort": 8080,
  "ProxyUser": "",
  "ProxyPassword": "",
  "RemotePaths": [
    "/"
  ],
  "RetentionDays": 1,
  "EnableBackup": true,
  "LocalBackupPath": "C:\\SourceCode\\BatchDeleteFTP\\Backups",
  "SmtpHost": "mail.fujinet.net",
  "SmtpPort": 25,
  "SmtpEnableSsl": false,
  "SmtpUser": "duy-hd",
  "SmtpPassword": "",
  "SenderEmail": "duy-hd@fujinet.net",
  "RecipientEmails": [
    "admin@fujinet.net",
    "monitor@fujinet.net"
  ],
  "AutoSendEmailOnPreview": false,
  "IsDarkTheme": false,
  "Profiles": [
    {
      "Name": "SITE_A",
      "Host": "ftp.xxx.net",
      "Port": 6021,
      "EncryptionMode": "Implicit",
      "Username": "XXX-USER",
      "Password": "",
      "UseProxy": true,
      "ProxyHost": "proxy.xxx.vn",
      "ProxyPort": 8080,
      "ProxyUser": "",
      "ProxyPassword": "",
      "RemotePaths": [
        "/"
      ],
      "RetentionDays": 1,
      "EnableBackup": true,
      "LocalBackupPath": "C:\\SourceCode\\BatchDeleteFTP\\Backups",
      "SmtpHost": "mail.fujinet.net",
      "SmtpPort": 25,
      "SmtpEnableSsl": false,
      "SmtpUser": "duy-hd",
      "SmtpPassword": "",
      "SenderEmail": "duy-hd@fujinet.net",
      "RecipientEmails": [
        "admin@fujinet.net"
      ],
      "AutoSendEmailOnPreview": false
    }
  ]
}
```

---

## 3. Hướng Dẫn Sử Dụng

### 3.1. Chế Độ Giao Diện (WPF GUI)
Khởi chạy trực tiếp `BatchDeleteFTP.exe`:

1. **Thiết lập & Quản lý Profile:**
   - Chọn Profile có sẵn ở danh sách thả xuống trên thanh tiêu đề hoặc bấm **Tạo Mới** để thêm một cấu hình máy chủ mới.
   - Nhập thông số kết nối và bấm **Lưu Profile**.
   - Bấm **Cấu Hình FTP & Backup** để tùy chỉnh chi tiết: cổng, TLS, Proxy, thư mục Remote Path và đường dẫn lưu trữ Backup.
   - Bấm **Kiểm Tra Kết Nối** để xác nhận thông số FTPS hoạt động chính xác.

2. **Quét & Xóa File Đa Profile (Tab 1: Tự Động Quét & Xóa):**
   - Bấm **Profiles Quét: ...** để mở hộp thoại lựa chọn các Site Profile bạn muốn quét cùng lúc (mặc định là Profile đang active).
   - Bấm **Xem Trước (Preview)** để hệ thống quét các thư mục đã chọn trên tất cả các Profile đã đánh dấu.
   - Kiểm tra danh sách file thỏa điều kiện xóa hiển thị trên bảng `DataGrid` (phân loại rõ cột `Site Profile`). Bạn có thể tích chọn / bỏ chọn từng file hoặc dùng nút **Chọn tất cả** / **Bỏ chọn tất cả**.
   - Bấm **Thực Hiện Backup & Xóa** để tiến hành sao lưu về máy và dọn dẹp file trên các máy chủ FTP tương ứng.

3. **Gửi Email Báo Cáo:**
   - Bấm **Cấu Hình Email** để thiết lập máy chủ SMTP và danh sách địa chỉ nhận thông báo.
   - Bấm **Gửi Email Xem Trước** để gửi kết quả danh sách các file cần dọn dẹp tới email các thành viên liên quan.

4. **Quản Lý File Trực Tiếp Trên FTP (Tab 2: File Manager):**
   - Chuyển sang Tab **2. Quản Lý File FTP**.
   - Nhập đường dẫn thư mục remote hoặc điều hướng trực tiếp bằng các nút trên thanh công cụ.
   - Thực hiện các thao tác: **Xem File**, **Tải Lên File / Thư Mục**, **Tải Về Máy**, **Tạo Thư Mục**, hoặc **Xóa**.

### 3.2. Chế Độ Dòng Lệnh (Console / CMD / Windows Task Scheduler)

Hỗ trợ chạy ngầm hoặc tự động hóa hoàn toàn với các tham số dòng lệnh linh hoạt:

| Tham số | Ý nghĩa |
| :--- | :--- |
| `--preview` hoặc `/preview` | Chạy chế độ **Xem trước (Preview)**: chỉ quét và liệt kê danh sách file thỏa điều kiện xóa ra console/log, **không xóa thật** trên FTP. |
| `--auto` hoặc `--console` | Chạy chế độ **Tự Động Thực Thi**: tự động sao lưu về máy và dọn dẹp file trên FTP. |
| `--all` hoặc `--all-profiles` | Áp dụng cho **TẤT CẢ các Site Profile** đã cấu hình trong `config.json` (thay vì chỉ quét Profile đang active). |
| `--send-email` hoặc `--email` | **Tự động gửi email báo cáo** nếu phát hiện có ít nhất 1 file thỏa điều kiện cần xóa (nếu không có file cần xóa sẽ tự động bỏ qua để tránh spam email). |

#### Các câu lệnh thường dùng:

1. **Quét kiểm tra tất cả Profile và gửi mail nếu có file cần xóa (Khuyên dùng cho Task Scheduler mỗi sáng):**
   ```cmd
   BatchDeleteFTP.exe --preview --all --send-email
   ```
2. **Quét xem trước tất cả Profile trên màn hình console (không gửi mail, không xóa):**
   ```cmd
   BatchDeleteFTP.exe --preview --all
   ```
3. **Quét xem trước chỉ trên Profile mặc định hiện tại:**
   ```cmd
   BatchDeleteFTP.exe --preview
   ```
4. **Tự động Backup & Xóa trên TẤT CẢ Profile:**
   ```cmd
   BatchDeleteFTP.exe --auto --all
   ```

> **Mã Thoát (Exit Code):**
> - `0`: Tiến trình kết thúc thành công hoàn toàn, không có lỗi.
> - `1`: Xảy ra lỗi kết nối mạng hoặc có ít nhất 1 file thất bại trong quá trình kết nối / Backup / Xóa.

---

### 3.3. Hướng Dẫn Cấu Hình Windows Task Scheduler Chạy Tự Động Mỗi Sáng

Để ứng dụng tự động chạy ở chế độ **Console, Preview, kiểm tra tất cả các Profile và gửi mail nếu có file cần xóa** mỗi khi bạn khởi động máy tính vào buổi sáng, bạn có thể thiết lập theo 1 trong 2 cách sau:

#### Cách 1: Thiết lập tự động bằng PowerShell (Nhanh nhất - 1 lệnh duy nhất)

Mở **PowerShell với quyền Administrator** (`Run as Administrator`) và chạy đoạn lệnh sau (thay đổi `$appPath` nếu file exe nằm ở thư mục khác):

```powershell
$exePath = "C:\SourceCode\BatchDeleteFTP\src\BatchDeleteFTP\bin\Release\net9.0-windows\BatchDeleteFTP.exe"
$workingDir = [System.IO.Path]::GetDirectoryName($exePath)

$action = New-ScheduledTaskAction -Execute $exePath -Argument "--preview --all --send-email" -WorkingDirectory $workingDir
$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable

# Đặt delay 1 phút sau khi đăng nhập để đảm bảo card mạng và kết nối Internet/VPN đã sẵn sàng
$trigger.Delay = "PT1M"

Register-ScheduledTask -TaskName "BatchDeleteFTP_MorningPreview" `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Description "Quét kiểm tra tất cả FTP Profile và gửi mail báo cáo nếu có file cần xóa mỗi sáng khi mở máy." `
    -RunLevel Highest -Force

Write-Host "Đã tạo Task thành công! Chạy thử nghiệm ngay:" -ForegroundColor Green
Start-ScheduledTask -TaskName "BatchDeleteFTP_MorningPreview"
```

---

#### Cách 2: Thiết lập thủ công qua giao diện Windows Task Scheduler (GUI)

1. Nhấn tổ hợp phím **Win + R**, gõ **`taskschd.msc`** và nhấn **Enter** để mở trình quản lý *Task Scheduler*.
2. Ở khung bên phải, chọn **Create Task...** (không chọn *Create Basic Task* để có đầy đủ cấu hình nâng cao).
3. **Tab General (Thông tin chung):**
   - **Name:** Điền `BatchDeleteFTP_MorningPreview`.
   - **Description:** `Tự động kiểm tra file quá hạn trên tất cả Profile FTP và gửi email báo cáo mỗi sáng.`
   - Chọn **Run only when user is logged on** (nếu muốn cửa sổ console hiện ra để theo dõi) hoặc **Run whether user is logged on or not** (chạy ngầm hoàn toàn).
   - Tích chọn **Run with highest privileges** (chạy với quyền quản trị để không bị UAC chặn).
4. **Tab Triggers (Thời điểm kích hoạt):**
   - Nhấn **New...**
   - **Begin the task:** Chọn **At log on** (Mỗi khi đăng nhập vào Windows) hoặc **At startup** (Mỗi khi khởi động máy).
   - Trong mục *Advanced settings*, tích chọn **Delay task for:** và chọn **1 minute** (hoặc **2 minutes**).
     > *Lưu ý quan trọng:* Bước Delay giúp máy tính có đủ thời gian kết nối mạng LAN / Wifi / Proxy doanh nghiệp trước khi chương trình kết nối tới FTP Server.
   - Nhấn **OK**.
5. **Tab Actions (Hành động thực thi):**
   - Nhấn **New...**
   - **Action:** Chọn `Start a program`.
   - **Program/script:** Nhấn *Browse...* và chọn tới file `BatchDeleteFTP.exe` (ví dụ: `C:\SourceCode\BatchDeleteFTP\src\BatchDeleteFTP\bin\Release\net9.0-windows\BatchDeleteFTP.exe`).
   - **Add arguments (optional):** Điền chính xác:
     ```text
     --preview --all --send-email
     ```
   - **Start in (optional):** Điền đường dẫn thư mục chứa file exe (ví dụ: `C:\SourceCode\BatchDeleteFTP\src\BatchDeleteFTP\bin\Release\net9.0-windows`).
     > *Rất quan trọng:* Không được để trống ô này, chương trình cần đường dẫn gốc để nạp file cấu hình `config.json`.
   - Nhấn **OK**.
6. **Tab Conditions (Điều kiện mạng):**
   - Tích chọn **Start only if the following network connection is available:** và chọn **Any connection** (đảm bảo chỉ chạy khi máy đã có mạng).
7. **Tab Settings:**
   - Tích chọn **Allow task to be run on demand** (để có thể bấm chuột phải chọn *Run* kiểm tra bất cứ lúc nào).
   - Tích chọn **If the running task does not end when requested, force it to stop**.
8. Nhấn **OK** để hoàn tất việc lưu Task.

#### Kiểm tra thử nghiệm Task vừa tạo:
- Trong danh sách *Task Scheduler Library*, tìm task `BatchDeleteFTP_MorningPreview`.
- Nhấn chuột phải -> chọn **Run**.
- Kiểm tra file log tại `logs/BatchDeleteFTP_yyyyMMdd.log` và kiểm tra hòm thư nhận email để xác nhận kết quả.

---

### 3.4. Nhật Ký Hoạt Động (Logs)
Toàn bộ lịch sử kết nối, chi tiết lệnh FTP, danh sách file quét được, đường dẫn file đã Backup và kết quả xóa được ghi tự động vào:
```text
logs/BatchDeleteFTP_yyyyMMdd.log
```

---

## 4. Hướng Dẫn Biên Dịch & Kiểm Thử (Developer Guide)

- **Yêu cầu môi trường:** .NET 9.0 SDK hoặc .NET 10.0 SDK trên hệ điều hành Windows.
- **Biên dịch mã nguồn bản Release:**
  ```powershell
  dotnet build -c Release
  ```
- **Chạy toàn bộ Unit Tests (xUnit):**
  ```powershell
  dotnet test
  ```
