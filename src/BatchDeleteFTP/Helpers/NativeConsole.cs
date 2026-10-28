using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace BatchDeleteFTP.Helpers;

public class CliOptions
{
    public bool IsConsoleMode { get; init; }
    public bool IsPreviewOnly { get; init; }

    public static CliOptions Parse(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return new CliOptions { IsConsoleMode = false, IsPreviewOnly = false };
        }

        bool isPreview = args.Any(a =>
            a.Equals("--preview", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/preview", StringComparison.OrdinalIgnoreCase));

        bool isAuto = args.Any(a =>
            a.Equals("--auto", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--console", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/auto", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/console", StringComparison.OrdinalIgnoreCase));

        return new CliOptions
        {
            IsConsoleMode = isAuto || isPreview,
            IsPreviewOnly = isPreview
        };
    }
}

public static class NativeConsole
{
    private const int ATTACH_PARENT_PROCESS = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeConsole();

    public static void AttachOrCreateConsole()
    {
        if (!AttachConsole(ATTACH_PARENT_PROCESS))
        {
            AllocConsole();
        }
    }
}
