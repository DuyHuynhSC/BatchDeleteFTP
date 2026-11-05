using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace WorkManagement.Services;

public enum TaskSchedulerTriggerType
{
    AtLogonWithDelay,
    DailyTime
}

public class TaskSchedulerService
{
    public const string DefaultTaskName = "WorkManagement_FTP_AutoCleanup";
    public const string DefaultMorningTaskName = "WorkManagement_MorningPreview";

    public static string GetExecutablePath()
    {
        string? exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
        {
            return exe;
        }

        return Path.Combine(AppContext.BaseDirectory, "WorkManagement.exe");
    }

    public static string BuildCliArguments(string mode = "--preview", bool allProfiles = true, bool sendEmail = true)
    {
        string args = mode;
        if (allProfiles) args += " --all";
        if (sendEmail) args += " --send-email";
        return args;
    }

    public static string BuildCliCommand(string mode = "--preview", bool allProfiles = true, bool sendEmail = true)
    {
        string exePath = GetExecutablePath();
        string args = BuildCliArguments(mode, allProfiles, sendEmail);
        return $"\"{exePath}\" {args}";
    }

    public static string BuildSchtasksCreateCommand(string taskName, string runTime, string mode = "--preview", bool allProfiles = true, bool sendEmail = true)
    {
        string cliCmd = BuildCliCommand(mode, allProfiles, sendEmail);
        return $"schtasks /create /tn \"{taskName}\" /tr \"\\\"{cliCmd}\\\"\" /sc daily /st {runTime} /f";
    }

    public static string BuildPowerShellLogonCommand(string taskName, string mode = "--preview", bool allProfiles = true, bool sendEmail = true, int delayMinutes = 1)
    {
        string exePath = GetExecutablePath();
        string args = BuildCliArguments(mode, allProfiles, sendEmail);
        string workingDir = Path.GetDirectoryName(exePath) ?? string.Empty;

        return $"powershell -NoProfile -Command \"$a = New-ScheduledTaskAction -Execute '{exePath}' -Argument '{args}' -WorkingDirectory '{workingDir}'; $t = New-ScheduledTaskTrigger -AtLogOn; $t.Delay = 'PT{delayMinutes}M'; $s = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable; Register-ScheduledTask -TaskName '{taskName}' -Action $a -Trigger $t -Settings $s -Description 'WorkManagement tu dong quet va kiem tra FTP moi sang' -RunLevel Highest -Force\"";
    }

    public static string BuildCommandPreview(TaskSchedulerTriggerType triggerType, string taskName, string runTime, string mode = "--preview", bool allProfiles = true, bool sendEmail = true, int delayMinutes = 1)
    {
        return triggerType == TaskSchedulerTriggerType.AtLogonWithDelay
            ? BuildPowerShellLogonCommand(taskName, mode, allProfiles, sendEmail, delayMinutes)
            : BuildSchtasksCreateCommand(taskName, runTime, mode, allProfiles, sendEmail);
    }

    public async Task<(bool success, string output)> RegisterTaskAsync(
        TaskSchedulerTriggerType triggerType,
        string taskName,
        string runTime,
        string mode = "--preview",
        bool allProfiles = true,
        bool sendEmail = true,
        int delayMinutes = 1)
    {
        if (triggerType == TaskSchedulerTriggerType.AtLogonWithDelay)
        {
            return await RegisterAtLogonTaskAsync(taskName, mode, allProfiles, sendEmail, delayMinutes);
        }

        return await RegisterDailyTaskAsync(taskName, runTime, mode, allProfiles, sendEmail);
    }

    public async Task<(bool success, string output)> RegisterAtLogonTaskAsync(
        string taskName,
        string mode = "--preview",
        bool allProfiles = true,
        bool sendEmail = true,
        int delayMinutes = 1)
    {
        try
        {
            string exePath = GetExecutablePath();
            string args = BuildCliArguments(mode, allProfiles, sendEmail);
            string workingDir = Path.GetDirectoryName(exePath) ?? string.Empty;

            string script = $"$a = New-ScheduledTaskAction -Execute '{exePath}' -Argument '{args}' -WorkingDirectory '{workingDir}'; " +
                            $"$t = New-ScheduledTaskTrigger -AtLogOn; $t.Delay = 'PT{delayMinutes}M'; " +
                            $"$s = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable; " +
                            $"Register-ScheduledTask -TaskName '{taskName}' -Action $a -Trigger $t -Settings $s -Description 'WorkManagement tu dong quet va kiem tra FTP moi sang' -RunLevel Highest -Force";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{script}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return (false, "Không thể khởi động powershell.exe");

            string stdout = await proc.StandardOutput.ReadToEndAsync();
            string stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode == 0)
            {
                return (true, string.IsNullOrWhiteSpace(stdout) ? "Tạo Task khi khởi động máy (delay 1 phút) thành công." : stdout.Trim());
            }

            return (false, string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string output)> RegisterDailyTaskAsync(string taskName, string runTime, string mode = "--auto", bool allProfiles = true, bool sendEmail = true)
    {
        try
        {
            string cliCmd = BuildCliCommand(mode, allProfiles, sendEmail);
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
