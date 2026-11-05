# WorkManagement - DevOps Workspace & Automation Hub

**WorkManagement** là ứng dụng C# .NET WPF hiện đại kết hợp chế độ dòng lệnh (Console/CMD Hybrid), đóng vai trò là trung tâm làm việc (Workspace Hub) dành cho lập trình viên, kỹ sư DevOps và quản trị hệ thống:
1. **Quản lý máy ảo & Remote Desktop (VMs & RDP Hub):** Quản lý tập trung toàn bộ danh sách máy ảo, server cơ sở dữ liệu, tự động đăng nhập Remote Desktop (RDP) chỉ với 1 click, import/export dữ liệu Excel thông minh.
2. **Tự động hóa dọn dẹp & quản lý file FTP (FTP Cleaner & Manager):** Kết nối FTPS (TLS 1.2/1.3), quét đa máy chủ, sao lưu (backup) trước khi xóa, duyệt file trực quan và gửi email báo cáo.
3. **Lập lịch chạy tự động ngầm (Windows Task Scheduler):** Tự động khởi chạy ngầm mỗi sáng khi đăng nhập Windows (tự động delay 1 phút chờ mạng/VPN sẵn sàng), mặc định chạy ở chế độ **Xem trước (Preview)** an toàn và gửi email thông báo.

---

## 1. Các Tính Năng Nổi Bật

### 1.1. Quản Lý Máy Ảo & Kết Nối Remote Desktop (VMs & RDP Hub)
- **Danh mục máy ảo chi tiết:** Quản lý đầy đủ các trường thông tin: *STT, Địa chỉ IP, Tên máy, Domain/Workgroup, Hệ điều hành (OS), User, Password, TNS, Database Name, Oracle Version, Người kiểm tra, Ghi chú, Ngày hết hạn*.
- **Kết nối RDP 1-click (Nút Play):**
  - Tự động nạp thông tin đăng nhập vào Windows Credential (`cmdkey`) theo chuẩn định dạng `[Tên máy]\[User]` (ví dụ: `D2-SVR15-6016\Administrator`) và khởi chạy `mstsc.exe` kết nối tới IP đích mà không cần nhập lại mật khẩu.
- **Bảo mật & Thao tác nhanh:**
  - Hỗ trợ nút ẩn/hiện mật khẩu dạng `••••••••` trên từng dòng.
  - Nút sao chép (Copy) nhanh IP và Password vào Clipboard chỉ với một chạm.
- **Import Excel Đa Sheet Thông Minh:**
  - Hỗ trợ cả file `.xlsx` và `.xls`.
  - Tự động nhận diện dòng tiêu đề (kể cả khi tiêu đề nằm ở dòng 2 hoặc dòng 3 của bảng tính).
  - Tự động chấm điểm chất lượng (Score) và đề xuất sheet chứa danh sách máy ảo remote.
  - Cho phép người dùng chuyển đổi xem trước dữ liệu giữa các sheet qua ComboBox chọn Sheet.
  - Hỗ trợ 3 chế độ xử lý khi trùng IP/Tên máy: *Ghi đè (Overwrite)*, *Bỏ qua (Skip)*, *Thêm tất cả (Append)*. Khi chọn Ghi đè, số thứ tự (STT) từ file Excel sẽ luôn được cập nhật đè vào hệ thống.
  - Tự động bù STT tăng dần nếu file nguồn để trống cột No.
- **Nút "Auto-Fit Cột":** Tự động căn chỉnh độ rộng tất cả các cột trong bảng vừa khít nội dung văn bản.
- **Nút "Xóa Tất Cả" (Delete All):** Dọn sạch danh sách máy ảo hiện tại với hộp thoại xác nhận an toàn trước khi import dữ liệu mới.
- **Xuất file mẫu Excel (Template):** Tải file mẫu `.xlsx` chuẩn để người dùng nhập liệu nhanh chóng.

### 1.2. Phân Hệ FTP Cleaner & Quản Lý File (FTP Automation)
- **Tự động quét & dọn dẹp file theo hạn lưu trữ:**
  ```text
  SystemDate.Date > UploadDate.Date + RetentionDays (Mặc định RetentionDays = 1)
  ```
- **Quét đồng thời nhiều Profile (`Multi-Profile Scan`):** Quét trên profile hiện tại hoặc quét hàng loạt trên tất cả các máy chủ FTP đã cấu hình.
- **Cơ chế Fail-safe Backup:** Tải file về máy tính cục bộ bảo toàn cấu trúc thư mục con trước khi thực hiện lệnh xóa thật trên FTP Server.
- **Hỗ trợ FTPS & Proxy:** Hỗ trợ Implicit TLS (Port 6021/990), Explicit TLS (Port 21) và HTTP/HTTPS Proxy doanh nghiệp.
- **Trình duyệt File FTP Trực Quan:** Xem file (text, code, hình ảnh), Upload file/thư mục, Download về máy, Tạo thư mục mới và Xóa trực tiếp.
- **Email Báo Cáo HTML:** Gửi báo cáo chi tiết danh sách file cần xóa qua SMTP server tới danh sách email cấu hình sẵn.

### 1.3. Giao Diện Người Dùng & Bộ Theme Phong Phú
- **Menu Dock Bên Trái (Sidebar):** Điều hướng linh hoạt giữa các chức năng, có nút thu gọn/mở rộng menu (Collapse/Expand) để mở rộng tối đa không gian làm việc.
- **Đa dạng Theme:** Hỗ trợ chuyển đổi mượt mà giữa các phong cách thiết kế:
  - *Material Light* (Sáng hiện đại, độ tương phản cao)
  - *Material Dark* (Tối dịu mắt)
  - *Skeuomorphism* (Mô phỏng chất liệu nổi)
  - *Neumorphism* (Giao diện dập nổi mượt mà)
  - *Frutiger Aero* (Kính trong suốt, xanh lục bảo)
  - *Claymorphism* (Đất sét 3D bo tròn)
  - *Glassmorphism* (Kính mờ acrylic)
  - *Y2K Digital* (Công nghệ số hoài cổ)

---

## 2. Chế Độ Dòng Lệnh & Lập Lịch Tự Động (Windows Task Scheduler)

Ứng dụng hỗ trợ chạy ở chế độ Console/CMD không cần mở giao diện đồ họa, rất thích hợp để chạy tự động qua Windows Task Scheduler.

### 2.1. Các Tham Số Dòng Lệnh (CLI Arguments)

| Tham số | Ý nghĩa |
| :--- | :--- |
| `--preview` hoặc `/preview` | **(Mặc định - Khuyên dùng):** Chỉ quét và liệt kê danh sách file cần xóa ra màn hình console và file log, **không xóa thật** trên FTP. |
| `--auto` hoặc `--console` | **Tự Động Thực Thi:** Tự động sao lưu file về máy và thực hiện lệnh xóa thật trên FTP. |
| `--all` hoặc `--all-profiles` | Áp dụng quét trên **TẤT CẢ các Site Profile** đã cấu hình trong `config.json`. |
| `--send-email` hoặc `--email` | **Tự động gửi email thông báo** nếu có ít nhất 1 file thỏa điều kiện xóa. |

### 2.2. Lệnh CLI Thường Dùng

1. **Quét kiểm tra tất cả Profile và gửi mail báo cáo (Chế độ khuyến nghị mỗi sáng):**
   ```cmd
   WorkManagement.exe --preview --all --send-email
   ```
2. **Xem trước danh sách file trên console (không gửi mail, không xóa):**
   ```cmd
   WorkManagement.exe --preview --all
   ```
3. **Thực thi Backup & Xóa tự động trên tất cả Profile:**
   ```cmd
   WorkManagement.exe --auto --all
   ```

---

## 3. Cấu Hình Windows Task Scheduler

Bạn có thể thiết lập lịch chạy tự động theo 1 trong 2 cách sau:

### Cách 1: Thiết lập trực tiếp từ Giao diện Ứng dụng (Nhanh nhất - 1 Click)

1. Mở ứng dụng **WorkManagement**, ở menu bên trái chọn **Lịch Task Scheduler**.
2. Cửa sổ cấu hình hiển thị:
   - **Thời điểm kích hoạt Task:**
     - **Tùy chọn 1 (Mặc định - Khuyên dùng cho mỗi sáng):** *"Khi khởi động / đăng nhập Windows (Tự động delay 1 phút chờ mạng/VPN sẵn sàng)"*.
     - **Tùy chọn 2:** *"Chạy định kỳ vào giờ cố định hàng ngày (HH:mm)"*.
   - **Chế độ thực thi:** Mặc định chọn **`Chỉ Preview & Gửi Mail Báo Cáo (--preview)`** để đảm bảo an toàn tuyệt đối.
   - Tích chọn sẵn **Áp dụng cho tất cả Profiles FTP (--all)** và **Gửi email thông báo kết quả (--send-email)**.
3. Bấm nút **"Tạo Task Vào Windows"**. Ứng dụng sẽ tự động đăng ký Task vào Windows Task Scheduler.
   *(Nếu cần chạy thủ công, bạn có thể bấm **Copy Lệnh** để dán vào PowerShell Administrator).*

---

### Cách 2: Thiết lập bằng PowerShell Administrator

Mở PowerShell với quyền Quản trị (`Run as Administrator`) và chạy đoạn mã sau:

```powershell
$exePath = "C:\SourceCode\WorkManagement\src\WorkManagement\bin\Release\net9.0-windows\WorkManagement.exe"
$workingDir = [System.IO.Path]::GetDirectoryName($exePath)

$action = New-ScheduledTaskAction -Execute $exePath -Argument "--preview --all --send-email" -WorkingDirectory $workingDir
$trigger = New-ScheduledTaskTrigger -AtLogOn
# Delay 1 phút sau khi đăng nhập để đảm bảo card mạng/VPN/Proxy đã kết nối ổn định
$trigger.Delay = "PT1M"
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable

Register-ScheduledTask -TaskName "WorkManagement_MorningPreview" `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Description "WorkManagement tu dong quet va kiem tra FTP moi sang khi mo may." `
    -RunLevel Highest -Force

Write-Host "Da tao Task thanh cong! Chay thu nghiem ngay:" -ForegroundColor Green
Start-ScheduledTask -TaskName "WorkManagement_MorningPreview"
```

---

## 4. Cấu Trúc Lưu Trữ Dữ Liệu

1. **Cấu hình FTP & Email (`config.json`):**
   - Lưu trữ cùng thư mục với file thực thi `WorkManagement.exe`.
2. **Danh sách máy ảo (`virtual_machines.json`):**
   - Lưu trữ an toàn tại thư mục AppData của người dùng:
     `%APPDATA%\WorkManagement\virtual_machines.json`
3. **Nhật ký hoạt động (`logs`):**
   - Toàn bộ log quét, kết nối RDP, thao tác backup/xóa file được lưu tại:
     `logs/WorkManagement_yyyyMMdd.log`

---

## 5. Hướng Dẫn Biên Dịch & Kiểm Thử (Developer Guide)

- **Môi trường yêu cầu:** .NET 9.0 SDK trở lên trên Windows.
- **Biên dịch bản Release:**
  ```powershell
  dotnet build -c Release WorkManagement.sln
  ```
- **Chạy kiểm thử toàn bộ Unit Tests (xUnit):**
  ```powershell
  dotnet test WorkManagement.sln
  ```
- File thực thi sau khi biên dịch:
  `src\WorkManagement\bin\Release\net9.0-windows\WorkManagement.exe`
