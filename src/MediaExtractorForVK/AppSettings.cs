using System.Text.Json;

namespace MediaExtractorForVK;

internal sealed class ScanProfile
{
    public string Name { get; set; } = "";
    public int SimilarityDistance { get; set; } = 6;
    public bool RecognitionEnabled { get; set; } = true;
    public string RecognitionModelId { get; set; } = RecognitionModels.StandardId;

    public ScanProfile Clone() => new()
    {
        Name = Name,
        SimilarityDistance = SimilarityDistance,
        RecognitionEnabled = RecognitionEnabled,
        RecognitionModelId = RecognitionModelId
    };
}

internal sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public List<string> Folders { get; set; } = [];
    public string LastBrowseFolder { get; set; } = "";
    public string VkDestinationPath { get; set; } = "";
    public int SimilarityDistance { get; set; } = 6;
    public bool RecognitionEnabled { get; set; } = true;
    public string RecognitionModelId { get; set; } = RecognitionModels.StandardId;
    public string Theme { get; set; } = "Light";
    public string ActiveScanProfileName { get; set; } = "Сбалансированный";
    public List<ScanProfile> ScanProfiles { get; set; } = DefaultProfiles();

    public static AppSettings Load()
    {
        try
        {
            var path = SettingsPath();
            if (!File.Exists(path))
            {
                var legacyPath = LegacySettingsPath();
                if (!File.Exists(legacyPath))
                {
                    return new AppSettings();
                }

                var migrated = JsonSerializer.Deserialize<AppSettings>(
                                   File.ReadAllText(legacyPath)) ??
                               new AppSettings();
                migrated.Normalize();
                migrated.Save();
                return migrated;
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ??
                           new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var path = SettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            // Settings are optional and must not block the application.
        }
    }

    private static string SettingsPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MediaExtractorForVK",
            "settings.json");

    private static string LegacySettingsPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MediaParserForVK",
            "settings.json");

    private void Normalize()
    {
        ScanProfiles ??= [];
        if (ScanProfiles.Count == 0)
        {
            ScanProfiles = DefaultProfiles();
        }

        ScanProfiles = ScanProfiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Name))
            .GroupBy(profile => profile.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var profile = group.Last().Clone();
                profile.Name = profile.Name.Trim();
                profile.SimilarityDistance = Math.Clamp(profile.SimilarityDistance, 1, 16);
                profile.RecognitionModelId =
                    RecognitionModels.Get(profile.RecognitionModelId).Id;
                return profile;
            })
            .ToList();
    }

    private static List<ScanProfile> DefaultProfiles() =>
    [
        new()
        {
            Name = "Быстрый",
            SimilarityDistance = 4,
            RecognitionEnabled = false
        },
        new()
        {
            Name = "Сбалансированный",
            SimilarityDistance = 6,
            RecognitionEnabled = true,
            RecognitionModelId = RecognitionModels.StandardId
        },
        new()
        {
            Name = "Широкий поиск",
            SimilarityDistance = 10,
            RecognitionEnabled = true,
            RecognitionModelId = RecognitionModels.StandardId
        }
    ];
}
