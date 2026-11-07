using System;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using WorkManagement.Models;

namespace WorkManagement.Helpers;

public static class DateFilterRule
{
    public static bool ShouldDelete(DateTime fileModifiedDate, DateTime currentSystemDate, int retentionDays = 1)
    {
        return currentSystemDate.Date > fileModifiedDate.Date.AddDays(retentionDays);
    }

    /// <summary>
    /// Đánh giá toàn diện xem một file trên FTP có thỏa điều kiện xóa hay không,
    /// kết hợp: Ngày lưu trữ (RetentionDays), Kích thước tối thiểu (MinFileSizeMb),
    /// Danh sách đuôi file cho phép (IncludeExtensions) và Danh sách loại trừ an toàn (ExcludePatterns).
    /// </summary>
    public static bool ShouldDeleteCandidate(
        string fullRemotePath,
        string fileName,
        long fileSizeBytes,
        DateTime fileModifiedDate,
        DateTime currentSystemDate,
        FtpSettings settings)
    {
        if (!ShouldDelete(fileModifiedDate, currentSystemDate, settings.RetentionDays))
        {
            return false;
        }

        // 1. Kiểm tra kích thước tối thiểu (MB)
        if (settings.MinFileSizeMb > 0)
        {
            long minBytes = (long)(settings.MinFileSizeMb * 1024.0 * 1024.0);
            if (fileSizeBytes < minBytes)
            {
                return false;
            }
        }

        // 2. Kiểm tra danh sách loại trừ an toàn (Whitelist / Exclude Patterns: wildcard *, ? hoặc substring đường dẫn)
        if (!string.IsNullOrWhiteSpace(settings.ExcludePatterns))
        {
            var patterns = settings.ExcludePatterns
                .Split(new[] { ';', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var rawPattern in patterns)
            {
                if (MatchesExcludePattern(fullRemotePath, fileName, rawPattern))
                {
                    return false;
                }
            }
        }

        // 3. Kiểm tra danh sách chỉ xóa các đuôi file cụ thể (IncludeExtensions, ví dụ: .zip;.bak;*.dmp)
        if (!string.IsNullOrWhiteSpace(settings.IncludeExtensions))
        {
            var exts = settings.IncludeExtensions
                .Split(new[] { ';', ',', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (exts.Length > 0)
            {
                string fileExt = Path.GetExtension(fileName);
                bool matchedExt = false;

                foreach (var rawExt in exts)
                {
                    string cleanExt = rawExt.TrimStart('*');
                    if (!cleanExt.StartsWith('.')) cleanExt = "." + cleanExt;

                    if (string.Equals(fileExt, cleanExt, StringComparison.OrdinalIgnoreCase) ||
                        FileSystemName.MatchesSimpleExpression(rawExt, fileName, ignoreCase: true))
                    {
                        matchedExt = true;
                        break;
                    }
                }

                if (!matchedExt)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static bool IsExcludedDirectory(string remoteDirectoryPath, string? excludePatterns)
    {
        if (string.IsNullOrWhiteSpace(excludePatterns)) return false;

        string dirName = Path.GetFileName(remoteDirectoryPath.TrimEnd('/'));
        var patterns = excludePatterns
            .Split(new[] { ';', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var pattern in patterns)
        {
            if (MatchesExcludePattern(remoteDirectoryPath, dirName, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesExcludePattern(string fullRemotePath, string name, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;

        string normalizedPath = fullRemotePath.Replace('\\', '/');
        string normalizedPattern = pattern.Replace('\\', '/');

        // Khớp tên file/thư mục theo Wildcard (* hoặc ?)
        if (FileSystemName.MatchesSimpleExpression(normalizedPattern, name, ignoreCase: true) ||
            FileSystemName.MatchesSimpleExpression(normalizedPattern, normalizedPath, ignoreCase: true))
        {
            return true;
        }

        // Nếu mẫu có chứa dấu '/' (ví dụ '/master/' hoặc 'keep_folder'), kiểm tra chứa trong đường dẫn
        if (normalizedPattern.Contains('/') &&
            normalizedPath.Contains(normalizedPattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
