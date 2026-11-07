using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace WorkManagement.Helpers;

public class CliOptions
{
    public bool IsConsoleMode { get; init; }
    public bool IsPreviewOnly { get; init; }
    public bool IsAllProfiles { get; init; }
    public bool SendEmailIfCandidatesFound { get; init; }
    public bool StartMinimizedToTray { get; init; }

    public static CliOptions Parse(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return new CliOptions
            {
                IsConsoleMode = false,
                IsPreviewOnly = false,
                IsAllProfiles = false,
                SendEmailIfCandidatesFound = false,
                StartMinimizedToTray = false
            };
        }

        bool isPreview = args.Any(a =>
            a.Equals("--preview", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/preview", StringComparison.OrdinalIgnoreCase));

        bool isAuto = args.Any(a =>
            a.Equals("--auto", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--console", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/auto", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/console", StringComparison.OrdinalIgnoreCase));

        bool isAll = args.Any(a =>
            a.Equals("--all", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--all-profiles", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/all", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/all-profiles", StringComparison.OrdinalIgnoreCase));

        bool sendEmail = args.Any(a =>
            a.Equals("--send-email", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--email", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/send-email", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/email", StringComparison.OrdinalIgnoreCase));

        bool startInTray = args.Any(a =>
            a.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/tray", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/minimized", StringComparison.OrdinalIgnoreCase));

        return new CliOptions
        {
            IsConsoleMode = isAuto || isPreview,
            IsPreviewOnly = isPreview,
            IsAllProfiles = isAll,
            SendEmailIfCandidatesFound = sendEmail,
            StartMinimizedToTray = startInTray
        };
    }
}

public static class NativeConsole
{
    private const int ATTACH_PARENT_PROCESS = -1;
    private const uint CP_UTF8 = 65001;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleOutputCP(uint wCodePageID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCP(uint wCodePageID);

    public static void AttachOrCreateConsole()
    {
        if (!AttachConsole(ATTACH_PARENT_PROCESS))
        {
            AllocConsole();
        }

        try
        {
            SetConsoleOutputCP(CP_UTF8);
            SetConsoleCP(CP_UTF8);

            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            Console.OutputEncoding = utf8NoBom;
            Console.InputEncoding = utf8NoBom;

            var stdOut = new StreamWriter(Console.OpenStandardOutput(), utf8NoBom) { AutoFlush = true };
            var stdErr = new StreamWriter(Console.OpenStandardError(), utf8NoBom) { AutoFlush = true };
            Console.SetOut(stdOut);
            Console.SetError(stdErr);
        }
        catch
        {
            // Ignore if stdout is redirected without a console handle
        }
    }
}
