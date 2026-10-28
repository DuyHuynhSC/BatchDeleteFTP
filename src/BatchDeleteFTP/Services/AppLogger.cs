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
