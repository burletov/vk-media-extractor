namespace MediaTidy;

internal sealed record FolderMergeProgress(int Current, int Total, string FileName);
internal sealed record FolderMergeResult(string DestinationPath, int Copied, IReadOnlyList<string> Errors);

internal sealed class FolderMergeService
{
    public async Task<FolderMergeResult> CopyNewestFirstAsync(
        IReadOnlyList<string> roots,
        string destinationParent,
        string folderName,
        IProgress<FolderMergeProgress>? progress,
        CancellationToken cancellationToken)
    {
        var safeName = SanitizeName(folderName);
        var destination = Path.Combine(destinationParent, safeName);
        Directory.CreateDirectory(destination);

        var files = roots
            .SelectMany(EnumerateFilesSafe)
            .Where(ImageScanner.IsSupportedMediaPath)
            .Where(path => !IsInside(path, destination))
            .Select(path =>
            {
                try
                {
                    return (Path: path, Date: File.GetLastWriteTimeUtc(path));
                }
                catch
                {
                    return (Path: "", Date: DateTime.MinValue);
                }
            })
            .Where(item => item.Path.Length > 0)
            .OrderByDescending(item => item.Date)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var errors = new List<string>();
        var copied = 0;
        for (var index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = files[index];
            progress?.Report(new FolderMergeProgress(
                index + 1,
                files.Length,
                Path.GetFileName(item.Path)));
            try
            {
                var stamp = item.Date.ToLocalTime().ToString("yyyy-MM-dd_HH-mm-ss");
                var targetName =
                    $"{index + 1:D6}_{stamp}_{Path.GetFileName(item.Path)}";
                var targetPath = UniquePath(destination, targetName);
                await using (var source = new FileStream(
                                 item.Path,
                                 FileMode.Open,
                                 FileAccess.Read,
                                 FileShare.ReadWrite | FileShare.Delete,
                                 1024 * 1024,
                                 useAsync: true))
                await using (var target = new FileStream(
                                 targetPath,
                                 FileMode.CreateNew,
                                 FileAccess.Write,
                                 FileShare.None,
                                 1024 * 1024,
                                 useAsync: true))
                {
                    await source.CopyToAsync(target, cancellationToken);
                    await target.FlushAsync(cancellationToken);
                }

                File.SetLastWriteTimeUtc(targetPath, item.Date);
                copied++;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{item.Path}: {exception.Message}");
            }
        }

        return new FolderMergeResult(destination, copied, errors);
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            IEnumerable<string> files;
            IEnumerable<string> directories;
            try
            {
                files = Directory.EnumerateFiles(current).ToArray();
                directories = Directory.EnumerateDirectories(current).ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            foreach (var directory in directories)
            {
                pending.Push(directory);
            }
        }
    }

    private static bool IsInside(string path, string directory)
    {
        var relative = Path.GetRelativePath(directory, path);
        return relative != ".." &&
               !Path.IsPathRooted(relative) &&
               !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string SanitizeName(string value)
    {
        var result = string.IsNullOrWhiteSpace(value) ? "Общая медиатека" : value.Trim();
        foreach (var character in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(character, '_');
        }

        return result.Trim().TrimEnd('.');
    }

    private static string UniquePath(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            return path;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var suffix = 2; ; suffix++)
        {
            path = Path.Combine(directory, $"{stem}_{suffix}{extension}");
            if (!File.Exists(path))
            {
                return path;
            }
        }
    }
}
