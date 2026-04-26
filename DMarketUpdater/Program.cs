using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace DMarketUpdater;

internal static class Program
{
    private const int MaxRetryCount = 60;
    private const int RetryDelayMilliseconds = 500;

    private static int Main(string[] args)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "DMarketUpdater.log");

        try
        {
            Log(logPath, "=== DMarket Updater Start ===");
            Log(logPath, "Args: " + string.Join(" | ", args));

            if (args.Length < 2)
            {
                Log(logPath, "ERROR: arguments are insufficient.");
                return 1;
            }

            var sourceExePath = args[0];
            var targetExePath = args[1];
            var oldExePath = args.Length >= 3 ? args[2] : string.Empty;

            if (string.IsNullOrWhiteSpace(sourceExePath) || !File.Exists(sourceExePath))
            {
                Log(logPath, "ERROR: source exe does not exist: " + sourceExePath);
                return 2;
            }

            if (string.IsNullOrWhiteSpace(targetExePath))
            {
                Log(logPath, "ERROR: target exe path is empty.");
                return 3;
            }

            var targetDir = Path.GetDirectoryName(targetExePath);
            if (string.IsNullOrWhiteSpace(targetDir))
            {
                Log(logPath, "ERROR: target directory is empty.");
                return 4;
            }

            Directory.CreateDirectory(targetDir);
            WaitForApplicationExit(oldExePath, targetExePath, logPath);
            CopyExeWithRetry(sourceExePath, targetExePath, logPath);

            DeleteOldExeIfRenamed(oldExePath, targetExePath, logPath);

            Log(logPath, "Starting: " + targetExePath);
            Process.Start(new ProcessStartInfo
            {
                FileName = targetExePath,
                WorkingDirectory = targetDir,
                UseShellExecute = true
            });

            Log(logPath, "=== DMarket Updater Success ===");
            return 0;
        }
        catch (Exception ex)
        {
            Log(logPath, "ERROR: " + ex);
            return 99;
        }
    }

    private static void WaitForApplicationExit(string oldExePath, string targetExePath, string logPath)
    {
        var names = new[]
        {
            Path.GetFileNameWithoutExtension(oldExePath),
            Path.GetFileNameWithoutExtension(targetExePath),
            "Windows",
            "DMarketHelper",
            "DMarket"
        };

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "Updater", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            Log(logPath, "Waiting process: " + process.ProcessName + " PID=" + process.Id);
                            process.WaitForExit(10000);
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        Thread.Sleep(800);
    }

    private static void CopyExeWithRetry(string sourceExePath, string targetExePath, string logPath)
    {
        Exception? lastException = null;

        for (var i = 1; i <= MaxRetryCount; i++)
        {
            try
            {
                File.Copy(sourceExePath, targetExePath, true);
                Log(logPath, $"Copied: {sourceExePath} -> {targetExePath}");
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                Log(logPath, $"Copy retry {i}/{MaxRetryCount}: {ex.Message}");
                Thread.Sleep(RetryDelayMilliseconds);
            }
        }

        throw new IOException("Failed to copy new exe.", lastException);
    }

    private static void DeleteOldExeIfRenamed(string oldExePath, string targetExePath, string logPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(oldExePath) || !File.Exists(oldExePath))
            {
                return;
            }

            if (string.Equals(Path.GetFullPath(oldExePath), Path.GetFullPath(targetExePath), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var oldName = Path.GetFileName(oldExePath);
            if (!oldName.Equals("Windows.exe", StringComparison.OrdinalIgnoreCase) &&
                !oldName.Equals("DMarketHelper.exe", StringComparison.OrdinalIgnoreCase) &&
                !oldName.Equals("DMarket.exe", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            for (var i = 1; i <= 10; i++)
            {
                try
                {
                    File.Delete(oldExePath);
                    Log(logPath, "Deleted legacy exe: " + oldExePath);
                    return;
                }
                catch (Exception ex)
                {
                    Log(logPath, $"Delete legacy retry {i}/10: {ex.Message}");
                    Thread.Sleep(RetryDelayMilliseconds);
                }
            }
        }
        catch (Exception ex)
        {
            Log(logPath, "DeleteOldExeIfRenamed warning: " + ex.Message);
        }
    }

    private static void Log(string path, string message)
    {
        try
        {
            File.AppendAllText(path, $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
            // ignore
        }
    }
}
