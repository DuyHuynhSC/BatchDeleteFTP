using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BatchDeleteFTP.Models;

namespace BatchDeleteFTP.Services;

public class EmailNotificationService
{
    private readonly AppLogger _logger;

    public EmailNotificationService(AppLogger logger)
    {
        _logger = logger;
    }

    public static List<string> ParseRecipientEmails(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return new List<string>();
        }

        var tokens = rawInput.Split(new[] { ';', ',', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in tokens)
        {
            if (MailAddress.TryCreate(token, out var mailAddr) && !string.IsNullOrWhiteSpace(mailAddr.Address))
            {
                if (seen.Add(mailAddr.Address))
                {
                    result.Add(mailAddr.Address);
                }
            }
        }

        return result;
    }

    public string BuildPreviewSubject(FtpSettings settings, int candidateCount, DateTime scanTime)
    {
        string profile = string.IsNullOrWhiteSpace(settings.SelectedProfileName) ? "Default" : settings.SelectedProfileName;
        return $"[BatchDeleteFTP] Kết quả Xem trước ({candidateCount} file) - Profile: {profile} ({scanTime:yyyy-MM-dd HH:mm})";
    }

    public string BuildPreviewHtmlBody(FtpSettings settings, IReadOnlyList<FtpFileCandidate> candidates, DateTime scanTime)
    {
        var activeList = candidates?.Where(c => c.IsSelected).ToList() ?? new List<FtpFileCandidate>();
        long totalBytes = activeList.Sum(c => c.Size);
        double totalMb = totalBytes / (1024.0 * 1024.0);
        string remotePaths = settings.RemotePaths != null && settings.RemotePaths.Count > 0
            ? string.Join("; ", settings.RemotePaths)
            : "/";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset='utf-8'/></head>");
        sb.AppendLine("<body style='font-family: Segoe UI, Roboto, Arial, sans-serif; color: #1E293B; line-height: 1.5; margin: 0; padding: 16px; background-color: #F8FAFC;'>");
        sb.AppendLine("<div style='max-width: 960px; margin: 0 auto; background: #FFFFFF; border-radius: 8px; border: 1px solid #E2E8F0; overflow: hidden;'>");

        // Header
        sb.AppendLine("<div style='background-color: #283593; color: #FFFFFF; padding: 16px 20px;'>");
        sb.AppendLine("<h2 style='margin: 0; font-size: 18px;'>BatchDeleteFTP - Báo Cáo Xem Trước File Cần Xóa</h2>");
        sb.AppendLine($"<p style='margin: 4px 0 0 0; font-size: 13px; opacity: 0.9;'>Thời gian tạo báo cáo: {scanTime:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine("</div>");

        // Summary Info
        sb.AppendLine("<div style='padding: 16px 20px;'>");
        sb.AppendLine("<table style='width: 100%; border-collapse: collapse; margin-bottom: 16px; font-size: 13px; background-color: #F1F5F9; border-radius: 6px;'>");
        sb.AppendLine($"<tr><td style='padding: 8px 12px; font-weight: bold; width: 210px;'>Site Profile:</td><td style='padding: 8px 12px;'>{WebUtility.HtmlEncode(settings.SelectedProfileName)}</td></tr>");
        sb.AppendLine($"<tr><td style='padding: 8px 12px; font-weight: bold;'>FTP Server:</td><td style='padding: 8px 12px;'>{WebUtility.HtmlEncode(settings.Host)}:{settings.Port} ({WebUtility.HtmlEncode(settings.EncryptionMode)} TLS)</td></tr>");
        sb.AppendLine($"<tr><td style='padding: 8px 12px; font-weight: bold;'>Thư mục quét (Remote Paths):</td><td style='padding: 8px 12px;'>{WebUtility.HtmlEncode(remotePaths)}</td></tr>");
        sb.AppendLine($"<tr><td style='padding: 8px 12px; font-weight: bold;'>Điều kiện lọc ngày:</td><td style='padding: 8px 12px;'>SystemDate.Date &gt; UploadDate.Date + {settings.RetentionDays} ngày</td></tr>");
        sb.AppendLine($"<tr><td style='padding: 8px 12px; font-weight: bold;'>Chế độ Backup trước xóa:</td><td style='padding: 8px 12px;'>{(settings.EnableBackup ? $"Bật ({WebUtility.HtmlEncode(settings.LocalBackupPath)})" : "Tắt (Xóa trực tiếp)")}</td></tr>");
        sb.AppendLine($"<tr><td style='padding: 8px 12px; font-weight: bold;'>Tổng số file dự kiến xóa:</td><td style='padding: 8px 12px; font-weight: bold; color: #D32F2F;'>{activeList.Count:N0} file ({totalBytes:N0} bytes ~ {totalMb:N2} MB)</td></tr>");
        sb.AppendLine("</table>");

        // Table of files
        if (activeList.Count == 0)
        {
            sb.AppendLine("<div style='padding: 16px; background-color: #E8F5E9; color: #2E7D32; border-radius: 6px; font-weight: 600;'>");
            sb.AppendLine("Không có file nào thỏa điều kiện xóa tại thời điểm kiểm tra.");
            sb.AppendLine("</div>");
        }
        else
        {
            sb.AppendLine("<h3 style='margin: 16px 0 8px 0; font-size: 15px; color: #1E293B;'>Danh sách chi tiết các file thỏa điều kiện xóa:</h3>");
            sb.AppendLine("<table style='width: 100%; border-collapse: collapse; font-size: 12.5px; border: 1px solid #CBD5E1;'>");
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr style='background-color: #1E293B; color: #FFFFFF; text-align: left;'>");
            sb.AppendLine("<th style='padding: 8px 10px; border: 1px solid #334155; width: 45px; text-align: center;'>STT</th>");
            sb.AppendLine("<th style='padding: 8px 10px; border: 1px solid #334155;'>Đường dẫn FTP (Remote Path)</th>");
            sb.AppendLine("<th style='padding: 8px 10px; border: 1px solid #334155; width: 150px;'>Ngày Upload</th>");
            sb.AppendLine("<th style='padding: 8px 10px; border: 1px solid #334155; width: 130px; text-align: right;'>Dung lượng (Bytes)</th>");
            sb.AppendLine("<th style='padding: 8px 10px; border: 1px solid #334155; width: 140px;'>Trạng thái</th>");
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");

            for (int i = 0; i < activeList.Count; i++)
            {
                var item = activeList[i];
                string rowBg = i % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
                sb.AppendLine($"<tr style='background-color: {rowBg};'>");
                sb.AppendLine($"<td style='padding: 6px 10px; border: 1px solid #E2E8F0; text-align: center;'>{i + 1}</td>");
                sb.AppendLine($"<td style='padding: 6px 10px; border: 1px solid #E2E8F0; font-family: Consolas, monospace;'>{WebUtility.HtmlEncode(item.FullPath)}</td>");
                sb.AppendLine($"<td style='padding: 6px 10px; border: 1px solid #E2E8F0;'>{item.ModifiedDate:yyyy-MM-dd HH:mm:ss}</td>");
                sb.AppendLine($"<td style='padding: 6px 10px; border: 1px solid #E2E8F0; text-align: right;'>{item.Size:N0}</td>");
                sb.AppendLine($"<td style='padding: 6px 10px; border: 1px solid #E2E8F0;'>{WebUtility.HtmlEncode(item.Status)}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
        }

        sb.AppendLine("<p style='margin-top: 16px; font-size: 11.5px; color: #64748B;'>Email này được gửi tự động từ công cụ <strong>BatchDeleteFTP</strong>.</p>");
        sb.AppendLine("</div></div></body></html>");
        return sb.ToString();
    }

    public async Task SendPreviewReportAsync(FtpSettings settings, IReadOnlyList<FtpFileCandidate> candidates, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            throw new InvalidOperationException("Chưa cấu hình địa chỉ máy chủ SMTP (SmtpHost). Vui lòng mở 'Cấu hình Email' để thiết lập.");
        }

        var recipients = settings.RecipientEmails?
            .SelectMany(ParseRecipientEmails)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (recipients.Count == 0)
        {
            throw new InvalidOperationException("Danh sách người nhận (Recipient Emails) đang trống hoặc không hợp lệ. Vui lòng nhập ít nhất 1 địa chỉ email.");
        }

        string fromAddress = !string.IsNullOrWhiteSpace(settings.SenderEmail)
            ? settings.SenderEmail.Trim()
            : settings.SmtpUser.Trim();

        if (string.IsNullOrWhiteSpace(fromAddress) || !MailAddress.TryCreate(fromAddress, out _))
        {
            throw new InvalidOperationException("Địa chỉ Email người gửi (Sender Email / SMTP User) chưa hợp lệ.");
        }

        DateTime now = DateTime.Now;
        int selectedCount = candidates?.Count(c => c.IsSelected) ?? 0;
        string subject = BuildPreviewSubject(settings, selectedCount, now);
        string htmlBody = BuildPreviewHtmlBody(settings, candidates ?? Array.Empty<FtpFileCandidate>(), now);

        _logger.Info($"Đang gửi email kết quả Xem trước ({selectedCount} file) tới {recipients.Count} thành viên ({string.Join(", ", recipients)}) qua {settings.SmtpHost}:{settings.SmtpPort}...");

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, "BatchDeleteFTP Notification", Encoding.UTF8),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = htmlBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };

        foreach (var email in recipients)
        {
            message.To.Add(new MailAddress(email));
        }

        using var client = new SmtpClient(settings.SmtpHost.Trim(), settings.SmtpPort > 0 ? settings.SmtpPort : 587)
        {
            EnableSsl = settings.SmtpEnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30000
        };

        if (!string.IsNullOrWhiteSpace(settings.SmtpUser))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(settings.SmtpUser.Trim(), settings.SmtpPassword);
        }
        else
        {
            client.UseDefaultCredentials = true;
        }

        await client.SendMailAsync(message, ct);
        _logger.Info($"Đã gửi email báo cáo Xem trước thành công tới: {string.Join(", ", recipients)}.");
    }
}
