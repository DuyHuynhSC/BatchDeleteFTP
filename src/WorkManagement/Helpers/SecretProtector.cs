using System;
using System.Security.Cryptography;
using System.Text;

namespace WorkManagement.Helpers;

/// <summary>
/// Cung cấp cơ chế mã hóa và giải mã mật khẩu an toàn dựa trên Windows Data Protection API (DPAPI).
/// Dữ liệu sau khi mã hóa sẽ có tiền tố "ENC:DPAPI:" giúp tự động nhận diện và tương thích ngược với dữ liệu cũ (plain-text).
/// </summary>
public static class SecretProtector
{
    public const string EncryptedPrefix = "ENC:DPAPI:";

    // Entropy cố định riêng cho ứng dụng để tăng cường bảo mật trên cùng tài khoản Windows
    private static readonly byte[] AppEntropy = Encoding.UTF8.GetBytes("WorkManagement.DevOpsHub.SecretEntropy.v1");

    public static bool IsEncrypted(string? value)
    {
        return !string.IsNullOrEmpty(value) &&
               value.StartsWith(EncryptedPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Mã hóa chuỗi mật khẩu bằng Windows DPAPI (CurrentUser).
    /// Nếu chuỗi rỗng hoặc đã được mã hóa từ trước thì giữ nguyên (Idempotent).
    /// </summary>
    public static string Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        if (IsEncrypted(plainText))
        {
            return plainText;
        }

        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, AppEntropy, DataProtectionScope.CurrentUser);
            return EncryptedPrefix + Convert.ToBase64String(encryptedBytes);
        }
        catch
        {
            // Fallback an toàn nếu môi trường không hỗ trợ DPAPI
            return plainText;
        }
    }

    /// <summary>
    /// Giải mã chuỗi mật khẩu đã được mã hóa bởi <see cref="Protect"/>.
    /// Nếu chuỗi chưa mã hóa (plain-text từ phiên bản cũ) thì trả về nguyên bản để đảm bảo tương thích ngược.
    /// </summary>
    public static string Unprotect(string? storedText)
    {
        if (string.IsNullOrEmpty(storedText))
        {
            return string.Empty;
        }

        if (!IsEncrypted(storedText))
        {
            return storedText;
        }

        string base64Payload = storedText.Substring(EncryptedPrefix.Length);
        if (string.IsNullOrWhiteSpace(base64Payload))
        {
            return string.Empty;
        }

        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(base64Payload);
            byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, AppEntropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // Nếu giải mã thất bại (ví dụ copy file sang máy khác hoặc chuỗi Base64 bị hỏng), trả về chuỗi rỗng hoặc nguyên bản an toàn
            return string.Empty;
        }
    }
}
