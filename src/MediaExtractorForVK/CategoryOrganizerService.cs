using System.Text.Json;

namespace MediaExtractorForVK;

internal sealed class CategoryOrganizeResult
{
    public required string DestinationRoot { get; init; }
    public required IReadOnlyList<CategoryMove> Moves { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
    public required int SkippedLowConfidence { get; init; }
    public required int SkippedAlreadyOrganized { get; init; }
}

internal sealed class CategoryRestoreResult
{
    public required IReadOnlyList<CategoryMove> Restored { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
}

internal sealed class CategoryOrganizerService
{
    public const string DestinationFolderName = "Media Extractor for VK - Категории";
    private const string JournalPrefix = "category-operation-";

    public async Task<CategoryOrganizeResult> OrganizeAsync(
        string rootPath,
        IReadOnlyList<PhotoRecord> photos,
        double minimumConfidence,
        bool includeUnknown,
        bool preserveOriginalFolders,
        CancellationToken cancellationToken)
    {
        var destinationRoot = Path.Combine(rootPath, DestinationFolderName);
        var destinationPrefix = Path.GetFullPath(destinationRoot)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var moves = new List<CategoryMove>();
        var errors = new List<string>();
        var skippedLowConfidence = 0;
        var skippedAlreadyOrganized = 0;

        foreach (var photo in photos
                     .DistinctBy(photo => photo.FullPath, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = Path.GetFullPath(photo.FullPath);
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            if (sourcePath.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase))
            {
                skippedAlreadyOrganized++;
                continue;
            }

            if (photo.PrimaryCategory == PhotoCategory.Unknown)
            {
                if (!includeUnknown)
                {
                    skippedLowConfidence++;
                    continue;
                }
            }
            else if (photo.CategoryConfidence < minimumConfidence)
            {
                skippedLowConfidence++;
                continue;
            }

            try
            {
                var categoryFolder = CategoryFolderName(photo.PrimaryCategory);
                var relativePath = Path.GetRelativePath(rootPath, sourcePath);
                if (relativePath.StartsWith("..", StringComparison.Ordinal))
                {
                    errors.Add($"{sourcePath}: файл находится вне выбранной папки");
                    continue;
                }

                var relativeDirectory = preserveOriginalFolders
                    ? Path.GetDirectoryName(relativePath)
                    : null;
                var targetDirectory = string.IsNullOrWhiteSpace(relativeDirectory) ||
                                      relativeDirectory == "."
                    ? Path.Combine(destinationRoot, categoryFolder)
                    : Path.Combine(destinationRoot, categoryFolder, relativeDirectory);
                Directory.CreateDirectory(targetDirectory);

                var destinationPath = GetAvailablePath(
                    Path.Combine(targetDirectory, Path.GetFileName(sourcePath)));
                File.Move(sourcePath, destinationPath);
                moves.Add(new CategoryMove
                {
                    OriginalPath = sourcePath,
                    CategoryPath = destinationPath,
                    Category = photo.PrimaryCategory
                });
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                NotSupportedException)
            {
                errors.Add($"{sourcePath}: {exception.Message}");
            }
        }

        if (moves.Count > 0)
        {
            Directory.CreateDirectory(destinationRoot);
            var journalPath = Path.Combine(
                destinationRoot,
                $"{JournalPrefix}{DateTime.Now:yyyyMMdd_HHmmss_fff}.json");
            await File.WriteAllTextAsync(
                journalPath,
                JsonSerializer.Serialize(
                    moves,
                    new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
        }

        return new CategoryOrganizeResult
        {
            DestinationRoot = destinationRoot,
            Moves = moves,
            Errors = errors,
            SkippedLowConfidence = skippedLowConfidence,
            SkippedAlreadyOrganized = skippedAlreadyOrganized
        };
    }

    public async Task<CategoryRestoreResult> RestoreAsync(
        IReadOnlyList<CategoryMove> moves,
        CancellationToken cancellationToken)
    {
        var restored = new List<CategoryMove>();
        var errors = new List<string>();

        foreach (var move in moves.Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!File.Exists(move.CategoryPath))
                {
                    errors.Add($"{move.CategoryPath}: файл не найден");
                    continue;
                }

                if (File.Exists(move.OriginalPath))
                {
                    errors.Add($"{move.OriginalPath}: исходное место уже занято");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(move.OriginalPath)!);
                File.Move(move.CategoryPath, move.OriginalPath);
                restored.Add(move);
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException or
                NotSupportedException)
            {
                errors.Add($"{move.OriginalPath}: {exception.Message}");
            }
        }

        await Task.CompletedTask;
        RemoveEmptyCategoryDirectories(moves);
        return new CategoryRestoreResult
        {
            Restored = restored,
            Errors = errors
        };
    }

    public async Task<IReadOnlyList<CategoryMove>> FindLatestOperationAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var destinationRoot = ResolveDestinationRoot(rootPath);
            if (!Directory.Exists(destinationRoot))
            {
                return Array.Empty<CategoryMove>();
            }

            var journal = Directory
                .GetFiles(destinationRoot, $"{JournalPrefix}*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (journal is null)
            {
                return Array.Empty<CategoryMove>();
            }

            var json = await File.ReadAllTextAsync(journal, cancellationToken);
            return JsonSerializer.Deserialize<List<CategoryMove>>(json) ?? [];
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            return Array.Empty<CategoryMove>();
        }
    }

    public static string CategoryFolderName(PhotoCategory category) => category switch
    {
        PhotoCategory.Photo => "Фото",
        PhotoCategory.Video => "Видео",
        PhotoCategory.Screenshot => "Скриншоты",
        PhotoCategory.Meme => "Мемы",
        PhotoCategory.Document => "Документы",
        PhotoCategory.Receipt => "Чеки",
        PhotoCategory.Blurry => "Размытые",
        PhotoCategory.Graphic => "Графика",
        _ => "Не определено"
    };

    private static string GetAvailablePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path)!;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var suffix = 1; ; suffix++)
        {
            var candidate = Path.Combine(directory, $"{fileName}_{suffix}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static void RemoveEmptyCategoryDirectories(IEnumerable<CategoryMove> moves)
    {
        var directories = moves
            .Select(move => Path.GetDirectoryName(move.CategoryPath))
            .Where(path => path is not null)
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path => path.Length);

        foreach (var directory in directories)
        {
            var current = directory;
            while (Directory.Exists(current) &&
                   !Path.GetFileName(current)
                       .Equals(DestinationFolderName, StringComparison.OrdinalIgnoreCase) &&
                   !Directory.EnumerateFileSystemEntries(current).Any())
            {
                var parent = Directory.GetParent(current)?.FullName;
                Directory.Delete(current);
                if (parent is null)
                {
                    break;
                }

                current = parent;
            }
        }
    }

    private static string ResolveDestinationRoot(string rootPath)
        => Path.Combine(rootPath, DestinationFolderName);
}
