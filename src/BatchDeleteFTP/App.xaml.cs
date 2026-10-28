using System;
using System.Windows;
using BatchDeleteFTP.Helpers;
using BatchDeleteFTP.Services;

namespace BatchDeleteFTP;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configService = new ConfigService();
        var logger = new AppLogger();
        var ftpService = new FtpCleanupService(logger);
        var cliOptions = CliOptions.Parse(e.Args);

        if (cliOptions.IsConsoleMode)
        {
            NativeConsole.AttachOrCreateConsole();
            int exitCode = 0;
            try
            {
                var settings = configService.Load();
                logger.Info($"Chạy chế độ Console ({(cliOptions.IsPreviewOnly ? "PREVIEW ONLY" : "AUTO BACKUP & DELETE")}).");
                var candidates = await ftpService.ScanCandidatesAsync(settings, DateTime.Now);

                if (cliOptions.IsPreviewOnly)
                {
                    foreach (var c in candidates)
                    {
                        logger.Info($"[PREVIEW] Sẽ xóa: {c.FullPath} | Upload: {c.ModifiedDate:yyyy-MM-dd HH:mm:ss} | Size: {c.Size} bytes");
                    }
                }
                else
                {
                    var (_, failedCount) = await ftpService.ExecuteBackupAndDeleteAsync(settings, candidates);
                    if (failedCount > 0) exitCode = 1;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Lỗi nghiêm trọng trong chế độ Console: {ex.Message}");
                exitCode = 1;
            }
            finally
            {
                NativeConsole.FreeConsole();
                Shutdown(exitCode);
            }
            return;
        }

        var mainWindow = new MainWindow(configService, logger, ftpService);
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
