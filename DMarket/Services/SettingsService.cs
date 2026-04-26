using DMarket.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DMarket.Services
{
    public static class SettingsService
    {
        private static readonly string SettingsDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DMarket");

        private static readonly string LegacySettingsDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Windows");

        private static readonly string SettingsPath =
            Path.Combine(SettingsDirectory, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static AppSettings Load()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);

                var defaultSettings = new AppSettings();
                var loadPath = ResolveLoadPath();

                if (string.IsNullOrWhiteSpace(loadPath) || !File.Exists(loadPath))
                {
                    Save(defaultSettings);
                    return defaultSettings;
                }

                var existingJson = File.ReadAllText(loadPath);
                if (string.IsNullOrWhiteSpace(existingJson))
                {
                    Save(defaultSettings);
                    return defaultSettings;
                }

                var mergedSettings = BuildMergedSettings(existingJson, defaultSettings);
                Save(mergedSettings);
                return mergedSettings;
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            Directory.CreateDirectory(SettingsDirectory);

            settings.SettingsVersion = AppSettings.CurrentSettingsVersion;

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }

        private static string ResolveLoadPath()
        {
            return SettingsPath;
        }

        private static AppSettings BuildMergedSettings(string existingJson, AppSettings defaultSettings)
        {
            JsonNode? defaultNode = JsonSerializer.SerializeToNode(defaultSettings, JsonOptions);
            JsonNode? existingNode = JsonNode.Parse(existingJson);

            if (defaultNode is not JsonObject defaultObject)
            {
                return defaultSettings;
            }

            if (existingNode is not JsonObject existingObject)
            {
                return defaultSettings;
            }

            var existingVersion = ReadSettingsVersion(existingObject);
            var migratedExistingObject = MigrateExistingSettings(existingObject, existingVersion, AppSettings.CurrentSettingsVersion);

            MergeIntoDefault(defaultObject, migratedExistingObject);
            defaultObject[nameof(AppSettings.SettingsVersion)] = AppSettings.CurrentSettingsVersion;

            var merged = defaultObject.Deserialize<AppSettings>(JsonOptions);
            return merged ?? defaultSettings;
        }

        private static int ReadSettingsVersion(JsonObject settingsObject)
        {
            if (settingsObject.TryGetPropertyValue(nameof(AppSettings.SettingsVersion), out var versionNode) &&
                versionNode is JsonValue valueNode)
            {
                try
                {
                    if (valueNode.TryGetValue<int>(out var version))
                    {
                        return version;
                    }
                }
                catch
                {
                    // 古い settings.json にバージョンがない、または数値でない場合は 0 扱い
                }
            }

            return 0;
        }

        private static JsonObject MigrateExistingSettings(JsonObject existingObject, int fromVersion, int toVersion)
        {
            var working = (JsonObject)existingObject.DeepClone();

            if (fromVersion >= toVersion)
            {
                return working;
            }

            for (var version = fromVersion + 1; version <= toVersion; version++)
            {
                switch (version)
                {
                    case 1:
                        break;
                    case 2:
                        if (!working.ContainsKey(nameof(AppSettings.DebugMode)))
                        {
                            working[nameof(AppSettings.DebugMode)] = false;
                        }
                        break;
                }
            }

            return working;
        }

        private static void MergeIntoDefault(JsonObject defaultObject, JsonObject existingObject)
        {
            foreach (var existingProperty in existingObject)
            {
                if (existingProperty.Key is null)
                {
                    continue;
                }

                if (!defaultObject.TryGetPropertyValue(existingProperty.Key, out var defaultValue))
                {
                    continue;
                }

                var existingValue = existingProperty.Value;
                if (existingValue is null)
                {
                    continue;
                }

                if (defaultValue is JsonObject defaultChildObject && existingValue is JsonObject existingChildObject)
                {
                    MergeIntoDefault(defaultChildObject, existingChildObject);
                    continue;
                }

                defaultObject[existingProperty.Key] = existingValue.DeepClone();
            }
        }
    }
}
