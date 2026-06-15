using System.Text.Json;

namespace MediaTidy;

internal sealed class QuarantineResult
{
    public required string QuarantineRoot { get; init; }
    public required IReadOnlyList<QuarantineMove> Moves { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
}

internal sealed class RestoreResult
{
    public required IReadOnlyList<QuarantineMove> Restored { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
}

internal sealed class QuarantineService
{
    private const string JournalName = "operation.json";
    private const string FolderPattern = ".MediaTidy_Quarantine_*";
    private const string LegacyFolderPattern = ".PhotoCleaner_Quarantine_*";

    public async Task<QuarantineResult> MoveAsync(
        string rootPath,
        IEnumerable<string> selectedPaths,
        CancellationToken cancellationToken)
    {
        var quarantineRoot = Path.Combine(
            rootPath,
            $".MediaTidy_Quarantine_{DateTime.Now:yyyyMMdd_HHmmss}");
        var moves = new List<QuarantineMove>();
        var errors = new List<string>();

        foreach (var sourcePath in selectedPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var relativePath = Path.GetRelativePath(rootPath, sourcePath);
                if (relativePath.StartsWith("..", StringComparison.Ordinal))
                {
                    errors.Add($"{sourcePath}: файл находится вне выбранной папки");
                    continue;
                }

                var destinationPath = GetAvailablePath(Path.Combine(quarantineRoot, relativePath));
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Move(sourcePath, destinationPath);
                moves.Add(new QuarantineMove
                {
                    OriginalPath = sourcePath,
                    QuarantinePath = destinationPath
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
            Directory.CreateDirectory(quarantineRoot);
            var journalPath = Path.Combine(quarantineRoot, JournalName);
            await File.WriteAllTextAsync(
                journalPath,
                JsonSerializer.Serialize(
                    moves,
                    new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
        }

        return new QuarantineResult
        {
            QuarantineRoot = quarantineRoot,
            Moves = moves,
            Errors = errors
        };
    }

    public async Task<RestoreResult> RestoreAsync(
        IReadOnlyList<QuarantineMove> moves,
        CancellationToken cancellationToken)
    {
        var restored = new List<QuarantineMove>();
        var errors = new List<string>();

        foreach (var move in moves.Reverse())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!File.Exists(move.QuarantinePath))
                {
                    errors.Add($"{move.QuarantinePath}: файл не найден в карантине");
                    continue;
                }

                if (File.Exists(move.OriginalPath))
                {
                    errors.Add($"{move.OriginalPath}: место уже занято");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(move.OriginalPath)!);
                File.Move(move.QuarantinePath, move.OriginalPath);
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
        return new RestoreResult
        {
            Restored = restored,
            Errors = errors
        };
    }

    public async Task<IReadOnlyList<QuarantineMove>> FindLatestOperationAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var latestJournal = new[] { FolderPattern, LegacyFolderPattern }
                .SelectMany(pattern =>
                    Directory.GetDirectories(rootPath, pattern, SearchOption.TopDirectoryOnly))
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => Path.Combine(path, JournalName))
                .FirstOrDefault(File.Exists);

            if (latestJournal is null)
            {
                return Array.Empty<QuarantineMove>();
            }

            var json = await File.ReadAllTextAsync(latestJournal, cancellationToken);
            return JsonSerializer.Deserialize<List<QuarantineMove>>(json) ?? [];
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            return Array.Empty<QuarantineMove>();
        }
    }

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
}
