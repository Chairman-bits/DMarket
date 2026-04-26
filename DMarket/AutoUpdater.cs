using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace DMarket;

public static class AutoUpdater
{
    private const string VERSION_URL = "https://raw.githubusercontent.com/Chairman-bits/DMarket/main/version.json";
    private const string RELEASE_NOTES_URL = "https://raw.githubusercontent.com/Chairman-bits/DMarket/main/release-notes.json";
    private const string CurrentAppExeName = "DMarket.exe";

    private static readonly string[] LegacyExeNames = Array.Empty<string>();

    private static VersionInfo? _versionInfo;
    private static List<ReleaseNote>? _releaseNotes;

    public static bool HasUpdate { get; private set; }
    public static string LatestVersionText { get; private set; } = "";
    public static bool LastCheckSucceeded { get; private set; }
    public static string CurrentVersionText { get; private set; } = "";
    public static string LastErrorMessage { get; private set; } = "";

    private static string NormalizeVersion(string? versionText)
    {
        var text = (versionText ?? string.Empty).Replace("\uFEFF", "").Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return "0.0.0";
        }

        if (Version.TryParse(text, out var version))
        {
            return version.Revision == 0
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : version.ToString();
        }

        return "0.0.0";
    }

    private static Version ParseVersionSafe(string? versionText)
    {
        return Version.TryParse(NormalizeVersion(versionText), out var version)
            ? version
            : new Version(0, 0, 0);
    }

    private static string GetCurrentVersion()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return "0.0.0";
        }

        var info = FileVersionInfo.GetVersionInfo(exePath);
        return NormalizeVersion(info.FileVersion);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(60);
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DMarket/1.0");
        return client;
    }

    public static void MigrateLegacyExeNameIfNeeded()
    {
        // 新規アプリのため旧 exe 名からの移行処理は不要です。
    }

    public static async Task CheckSilentlyAsync()
    {
        try
        {
            LastErrorMessage = "";

            using var client = CreateClient();
            var json = await client.GetStringAsync(VERSION_URL);

            _versionInfo = JsonSerializer.Deserialize<VersionInfo>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            CurrentVersionText = GetCurrentVersion();

            if (_versionInfo == null)
            {
                LastCheckSucceeded = false;
                HasUpdate = false;
                LatestVersionText = "";
                LastErrorMessage = "version.json の解析結果が null でした。";
                return;
            }

            _versionInfo.latest = NormalizeVersion(_versionInfo.latest);
            LatestVersionText = _versionInfo.latest;

            var currentVersion = ParseVersionSafe(CurrentVersionText);
            var latestVersion = ParseVersionSafe(LatestVersionText);

            if (latestVersion <= new Version(0, 0, 0))
            {
                LastCheckSucceeded = false;
                HasUpdate = false;
                LastErrorMessage = $"latest の値が不正です: {LatestVersionText}";
                return;
            }

            LastCheckSucceeded = true;
            HasUpdate = latestVersion > currentVersion;
        }
        catch (Exception ex)
        {
            LastCheckSucceeded = false;
            HasUpdate = false;
            CurrentVersionText = GetCurrentVersion();
            LatestVersionText = "";
            LastErrorMessage = ex.Message;
            AppDiagnostics.LogError("AutoUpdater.CheckSilentlyAsync", ex);
        }
    }

    public static async Task CheckAsync()
    {
        await CheckSilentlyAsync();
    }

    public static async Task ApplyUpdateAsync()
    {
        if (_versionInfo == null)
        {
            await CheckSilentlyAsync();
        }

        if (_versionInfo == null)
        {
            System.Windows.MessageBox.Show("更新情報の取得に失敗しました。", "アップデート", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_versionInfo.url) && (_versionInfo.urls == null || _versionInfo.urls.Count == 0))
        {
            System.Windows.MessageBox.Show("更新パッケージのURLが設定されていません。", "アップデート", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "DMarket_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            var extractDir = Path.Combine(tempRoot, "extract");
            Directory.CreateDirectory(extractDir);

            using (var client = CreateClient())
            {
                if (_versionInfo.urls != null && _versionInfo.urls.Count > 0)
                {
                    foreach (var packageUrl in _versionInfo.urls.Where(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        var fileName = GetSafeFileNameFromUrl(packageUrl);
                        var tempFile = Path.Combine(tempRoot, fileName);
                        var data = await client.GetByteArrayAsync(packageUrl);
                        await File.WriteAllBytesAsync(tempFile, data);

                        if (tempFile.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            ZipFile.ExtractToDirectory(tempFile, extractDir, true);
                        }
                    }
                }
                else
                {
                    var tempZip = Path.Combine(tempRoot, "app.zip");
                    var data = await client.GetByteArrayAsync(_versionInfo.url);
                    await File.WriteAllBytesAsync(tempZip, data);
                    ZipFile.ExtractToDirectory(tempZip, extractDir, true);
                }
            }

            var updaterPath = Path.Combine(extractDir, "Updater.exe");
            var newExePath = ResolveNewExePath(extractDir);
            var currentProcessPath = Environment.ProcessPath ?? string.Empty;
            var appDir = AppContext.BaseDirectory.TrimEnd('\\');
            var targetExePath = Path.Combine(appDir, CurrentAppExeName);

            if (!File.Exists(updaterPath))
            {
                System.Windows.MessageBox.Show("更新パッケージ内に Updater.exe が見つかりません。", "アップデートエラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(newExePath) || !File.Exists(newExePath))
            {
                System.Windows.MessageBox.Show("更新パッケージ内に新しい exe が見つかりません。", "アップデートエラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = $"\"{newExePath}\" \"{targetExePath}\" \"{currentProcessPath}\"",
                UseShellExecute = true,
                WorkingDirectory = extractDir
            });

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            AppDiagnostics.LogError("AutoUpdater.ApplyUpdateAsync", ex);
            System.Windows.MessageBox.Show("アップデートの開始に失敗しました。\n" + ex.Message, "アップデートエラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private static string GetSafeFileNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var fileName = Path.GetFileName(uri.LocalPath);
            return string.IsNullOrWhiteSpace(fileName) ? Guid.NewGuid().ToString("N") : fileName;
        }
        catch
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    private static string ResolveNewExePath(string extractDir)
    {
        var candidates = new List<string>
        {
            Path.Combine(extractDir, CurrentAppExeName)
        };

        var manifestExeName = _versionInfo?.appExeName;
        if (!string.IsNullOrWhiteSpace(manifestExeName))
        {
            candidates.Add(Path.Combine(extractDir, manifestExeName));
        }

        var currentExeName = Path.GetFileName(Environment.ProcessPath ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(currentExeName))
        {
            candidates.Add(Path.Combine(extractDir, currentExeName));
        }

        candidates.AddRange(LegacyExeNames.Select(name => Path.Combine(extractDir, name)));

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Directory.GetFiles(extractDir, "*.exe")
            .FirstOrDefault(x => !string.Equals(Path.GetFileName(x), "Updater.exe", StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
    }

    public static async Task ShowReleaseNotesAsync()
    {
        try
        {
            if (_releaseNotes == null)
            {
                using var client = CreateClient();
                var json = await client.GetStringAsync(RELEASE_NOTES_URL);

                _releaseNotes = JsonSerializer.Deserialize<List<ReleaseNote>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<ReleaseNote>();
            }

            if (_releaseNotes.Count == 0)
            {
                System.Windows.MessageBox.Show("更新履歴はありません。", "更新履歴", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var window = new DMarket.Windows.UpdateHistoryWindow(_releaseNotes);
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            AppDiagnostics.LogError("AutoUpdater.ShowReleaseNotesAsync", ex);
            System.Windows.MessageBox.Show("更新履歴の取得に失敗しました。", "更新履歴", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    public class VersionInfo
    {
        public string latest { get; set; } = "";
        public string url { get; set; } = "";
        public List<string> urls { get; set; } = new();
        public string appExeName { get; set; } = "DMarket.exe";
        public List<string> legacyExeNames { get; set; } = new();
    }

    public class ReleaseNote
    {
        public string version { get; set; } = "";
        public string publishedAt { get; set; } = "";
        public List<string> notes { get; set; } = new();
    }
}
