using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

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
