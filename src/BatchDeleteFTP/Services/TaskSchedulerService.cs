using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace BatchDeleteFTP.Services;

public class TaskSchedulerService
{
    public const string DefaultTaskName = "WorkManagement_FTP_AutoCleanup";

    public static string GetExecutablePath()
    {
        string? exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
        {
            return exe;
        }

        return Path.Combine(AppContext.BaseDirectory, "WorkManagement.exe");
    }

    public static string BuildCliCommand(string mode = "--auto", bool allProfiles = true, bool sendEmail = true)
    {
        string exePath = GetExecutablePath();
        string args = mode;
        if (allProfiles) args += " --all";
        if (sendEmail) args += " --send-email";

        return $"\"{exePath}\" {args}";
    }

    public static string BuildSchtasksCreateCommand(string taskName, string runTime, string mode = "--auto", bool allProfiles = true, bool sendEmail = true)
    {
        string cliCmd = BuildCliCommand(mode, allProfiles, sendEmail);
        return $"schtasks /create /tn \"{taskName}\" /tr \"\\\"{cliCmd}\\\"\" /sc daily /st {runTime} /f";
    }

    public async Task<(bool success, string output)> RegisterDailyTaskAsync(string taskName, string runTime, string mode = "--auto", bool allProfiles = true, bool sendEmail = true)
    {
        try
        {
            string cliCmd = BuildCliCommand(mode, allProfiles, sendEmail);
            // Example: schtasks /create /tn "WorkManagement_FTP_AutoCleanup" /tr "\"C:\path\app.exe\" --auto --all" /sc daily /st 23:00 /f
            string arguments = $"/create /tn \"{taskName}\" /tr \"{cliCmd}\" /sc daily /st {runTime} /f";

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return (false, "Không thể khởi động schtasks.exe");
            }

            string stdout = await proc.StandardOutput.ReadToEndAsync();
            string stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode == 0)
            {
                return (true, string.IsNullOrWhiteSpace(stdout) ? "Tạo task thành công trong Windows Task Scheduler." : stdout.Trim());
            }

            return (false, string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string output)> DeleteTaskAsync(string taskName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/delete /tn \"{taskName}\" /f",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return (false, "Không thể chạy schtasks.exe");

            string stdout = await proc.StandardOutput.ReadToEndAsync();
            string stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            return (proc.ExitCode == 0, proc.ExitCode == 0 ? "Đã xoá task thành công." : (stderr + " " + stdout).Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
