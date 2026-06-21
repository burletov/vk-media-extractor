using System.Text.Json;

namespace MediaExtractorForVK;

internal sealed class RecognitionCache
{
    private const string CacheFileName = ".MediaExtractorForVK_RecognitionCache.json";
    private readonly string _path;
    private readonly string _modelVersion;
    private readonly Dictionary<string, RecognitionCacheEntry> _entries;
    private readonly object _sync = new();

    private RecognitionCache(
        string path,
        string modelVersion,
        Dictionary<string, RecognitionCacheEntry> entries)
    {
        _path = path;
        _modelVersion = modelVersion;
        _entries = entries;
    }

    public static async Task<RecognitionCache> LoadAsync(
        string rootPath,
        string modelVersion,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(rootPath, CacheFileName);
        try
        {
            if (!File.Exists(path))
            {
                return new RecognitionCache(path, modelVersion, new(StringComparer.OrdinalIgnoreCase));
            }

            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var document = JsonSerializer.Deserialize<RecognitionCacheDocument>(json);
            if (document?.ModelVersion != modelVersion)
            {
                return new RecognitionCache(path, modelVersion, new(StringComparer.OrdinalIgnoreCase));
            }

            return new RecognitionCache(
                path,
                modelVersion,
                new Dictionary<string, RecognitionCacheEntry>(
                    document.Entries,
                    StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            return new RecognitionCache(path, modelVersion, new(StringComparer.OrdinalIgnoreCase));
        }
    }

    public bool TryApply(PhotoRecord photo)
    {
        var key = RelativeKey(photo.FullPath);
        if (!_entries.TryGetValue(key, out var entry) ||
            entry.Size != photo.Size ||
            entry.LastWriteTimeUtcTicks != photo.LastWriteTimeUtc.Ticks)
        {
            return false;
        }

        photo.OcrWordCount = entry.OcrWordCount;
        photo.OcrTextLength = entry.OcrTextLength;
        photo.OcrTextAreaRatio = entry.OcrTextAreaRatio;
        photo.OcrPreview = entry.OcrPreview;
        photo.PrimaryCategory = entry.PrimaryCategory;
        photo.CategoryConfidence = entry.CategoryConfidence;
        photo.CategoryScores = entry.CategoryScores;
        photo.RecognitionFromCache = true;
        return true;
    }

    public void Store(PhotoRecord photo)
    {
        var entry = new RecognitionCacheEntry
        {
            Size = photo.Size,
            LastWriteTimeUtcTicks = photo.LastWriteTimeUtc.Ticks,
            OcrWordCount = photo.OcrWordCount,
            OcrTextLength = photo.OcrTextLength,
            OcrTextAreaRatio = photo.OcrTextAreaRatio,
            OcrPreview = photo.OcrPreview,
            PrimaryCategory = photo.PrimaryCategory,
            CategoryConfidence = photo.CategoryConfidence,
            CategoryScores = new Dictionary<PhotoCategory, double>(photo.CategoryScores)
        };
        lock (_sync)
        {
            _entries[RelativeKey(photo.FullPath)] = entry;
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var document = new RecognitionCacheDocument
            {
                ModelVersion = _modelVersion,
                Entries = _entries
            };
            var temporaryPath = _path + ".tmp";
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(document),
                cancellationToken);
            File.Move(temporaryPath, _path, overwrite: true);
            File.SetAttributes(_path, File.GetAttributes(_path) | FileAttributes.Hidden);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            // Recognition remains usable even when the selected folder is read-only.
        }
    }

    private string RelativeKey(string path) =>
        Path.GetRelativePath(Path.GetDirectoryName(_path)!, path);

    private sealed class RecognitionCacheDocument
    {
        public string ModelVersion { get; set; } = "";
        public Dictionary<string, RecognitionCacheEntry> Entries { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class RecognitionCacheEntry
    {
        public long Size { get; set; }
        public long LastWriteTimeUtcTicks { get; set; }
        public int OcrWordCount { get; set; }
        public int OcrTextLength { get; set; }
        public double OcrTextAreaRatio { get; set; }
        public string OcrPreview { get; set; } = "";
        public PhotoCategory PrimaryCategory { get; set; }
        public double CategoryConfidence { get; set; }
        public Dictionary<PhotoCategory, double> CategoryScores { get; set; } = [];
    }
}
