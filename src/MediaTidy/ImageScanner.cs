using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace MediaTidy;

internal sealed class ImageScanner
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff",
        ".webp", ".heic", ".heif", ".avif"
    };
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".m4v", ".avi", ".mkv", ".webm", ".wmv",
        ".mts", ".m2ts", ".3gp", ".mpg", ".mpeg"
    };

    private static readonly string[] ScreenshotMarkers =
    {
        "screenshot", "screen shot", "screen_capture", "screen-capture",
        "снимок экрана", "скриншот"
    };

    private static readonly string[] MemeMarkers =
    {
        "meme", "mem_", "мем"
    };

    public async Task<ScanResult> ScanAsync(
        string rootPath,
        int similarityDistance,
        bool enableRecognition,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken) =>
        await ScanAsync(
            rootPath,
            similarityDistance,
            enableRecognition,
            RecognitionModels.StandardId,
            progress,
            cancellationToken);

    public async Task<ScanResult> ScanAsync(
        string rootPath,
        int similarityDistance,
        bool enableRecognition,
        string recognitionModelId,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken) =>
        await ScanAsync(
            new[] { rootPath },
            similarityDistance,
            enableRecognition,
            recognitionModelId,
            progress,
            cancellationToken);

    public async Task<ScanResult> ScanAsync(
        IReadOnlyList<string> rootPaths,
        int similarityDistance,
        bool enableRecognition,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken) =>
        await ScanAsync(
            rootPaths,
            similarityDistance,
            enableRecognition,
            RecognitionModels.StandardId,
            progress,
            cancellationToken);

    public async Task<ScanResult> ScanAsync(
        IReadOnlyList<string> rootPaths,
        int similarityDistance,
        bool enableRecognition,
        string recognitionModelId,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var normalizedRoots = rootPaths
            .Select(Path.GetFullPath)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedRoots.Length == 0)
        {
            throw new DirectoryNotFoundException("Не выбрана ни одна существующая папка.");
        }

        var files = EnumerateMediaFiles(normalizedRoots, cancellationToken).ToArray();
        var photoList = new List<PhotoRecord>(files.Length);
        foreach (var file in files)
        {
            try
            {
                var info = new FileInfo(file.Path);
                photoList.Add(new PhotoRecord
                {
                    FullPath = file.Path,
                    Size = info.Length,
                    LastWriteTimeUtc = info.LastWriteTimeUtc,
                    CapturedAtUtc = info.LastWriteTimeUtc,
                    MediaKind = file.Kind
                });
            }
            catch (Exception exception) when (
                exception is IOException or
                UnauthorizedAccessException)
            {
                // A file can disappear or become unavailable during enumeration.
            }
        }

        var photos = photoList.ToArray();

        progress?.Report(new ScanProgress
        {
            Stage = "Чтение фото и видео",
            Current = 0,
            Total = photos.Length
        });

        var analyzed = 0;
        await Parallel.ForEachAsync(
            photos,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
            },
            async (photo, token) =>
            {
                if (photo.MediaKind == MediaKind.Video)
                {
                    await AnalyzeVideoAsync(photo, token);
                }
                else
                {
                    AnalyzeImage(photo);
                }

                var current = Interlocked.Increment(ref analyzed);
                progress?.Report(new ScanProgress
                {
                    Stage = "Чтение фото и видео",
                    Current = current,
                    Total = photos.Length
                });
            });

        var duplicateSizeGroups = photos
            .GroupBy(photo => photo.Size)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .ToArray();

        progress?.Report(new ScanProgress
        {
            Stage = "Проверка точных копий",
            Current = 0,
            Total = duplicateSizeGroups.Length
        });

        var hashed = 0;
        await Parallel.ForEachAsync(
            duplicateSizeGroups,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Min(4, Math.Max(1, Environment.ProcessorCount / 2))
            },
            async (photo, token) =>
            {
                try
                {
                    photo.Sha256 = await ComputeSha256Async(photo.FullPath, token);
                }
                catch (Exception exception) when (
                    exception is IOException or
                    UnauthorizedAccessException)
                {
                    photo.Sha256 = null;
                }

                var current = Interlocked.Increment(ref hashed);
                progress?.Report(new ScanProgress
                {
                    Stage = "Проверка точных копий",
                    Current = current,
                    Total = duplicateSizeGroups.Length
                });
            });

        var groups = new List<FindingGroup>();
        var exactDuplicatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nextGroupId = 1;

        foreach (var hashGroup in duplicateSizeGroups
                     .Where(photo => photo.Sha256 is not null)
                     .GroupBy(photo => (photo.Size, photo.Sha256))
                     .Where(group => group.Count() > 1))
        {
            var groupPhotos = hashGroup.ToArray();
            var keep = ChooseBest(groupPhotos);
            foreach (var photo in groupPhotos)
            {
                if (!ReferenceEquals(photo, keep))
                {
                    exactDuplicatePaths.Add(photo.FullPath);
                }
            }

            groups.Add(new FindingGroup
            {
                Id = nextGroupId++,
                Kind = FindingKind.ExactDuplicate,
                Reason = "Содержимое совпадает полностью (SHA-256)",
                Photos = groupPhotos,
                RecommendedKeep = keep
            });
        }

        var similarityCandidates = photos
            .Where(photo => photo.DifferenceHash.HasValue)
            .Where(photo => !exactDuplicatePaths.Contains(photo.FullPath))
            .ToArray();

        progress?.Report(new ScanProgress
        {
            Stage = "Поиск похожих кадров",
            Current = 0,
            Total = similarityCandidates.Length
        });

        var unionFind = new UnionFind(similarityCandidates.Length);
        var tree = new HammingBkTree();

        for (var index = 0; index < similarityCandidates.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentPhoto = similarityCandidates[index];
            var hash = currentPhoto.DifferenceHash!.Value;

            foreach (var matchIndex in tree.Search(hash, similarityDistance))
            {
                var match = similarityCandidates[matchIndex];
                if (ArePlausiblyRelated(currentPhoto, match))
                {
                    unionFind.Union(index, matchIndex);
                }
            }

            tree.Add(hash, index);
            progress?.Report(new ScanProgress
            {
                Stage = "Поиск похожих кадров",
                Current = index + 1,
                Total = similarityCandidates.Length
            });
        }

        var similarityGroups = Enumerable.Range(0, similarityCandidates.Length)
            .GroupBy(unionFind.Find)
            .Select(group => group.Select(index => similarityCandidates[index]).ToArray())
            .Where(group => group.Length > 1)
            .ToArray();

        var faceCandidates = similarityGroups
            .SelectMany(group => group)
            .DistinctBy(photo => photo.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (faceCandidates.Length > 0)
        {
            var faceAnalyzer = new WindowsFaceAnalysisService();
            var analyzedFaces = 0;
            await Parallel.ForEachAsync(
                faceCandidates,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = 2
                },
                async (photo, token) =>
                {
                    await faceAnalyzer.AnalyzeAsync(photo, token);
                    var current = Interlocked.Increment(ref analyzedFaces);
                    progress?.Report(new ScanProgress
                    {
                        Stage = "Оценка лиц в похожих кадрах",
                        Current = current,
                        Total = faceCandidates.Length
                    });
                });
        }

        foreach (var groupPhotos in similarityGroups)
        {
            groups.Add(new FindingGroup
            {
                Id = nextGroupId++,
                Kind = FindingKind.SimilarImage,
                Reason = $"Визуальная близость, порог dHash ≤ {similarityDistance}",
                Photos = groupPhotos,
                RecommendedKeep = ChooseBest(groupPhotos)
            });
        }

        var recognitionStatus = "Распознавание отключено";
        if (enableRecognition)
        {
            recognitionStatus = await new RecognitionAnalyzer().AnalyzeAsync(
                normalizedRoots[0],
                photos.Where(photo => photo.MediaKind == MediaKind.Image).ToArray(),
                RecognitionModels.Get(recognitionModelId),
                progress,
                cancellationToken);
        }

        foreach (var photo in photos)
        {
            if (photo.MediaKind == MediaKind.Video)
            {
                continue;
            }

            var reviewReason = GetReviewReason(photo);
            if (reviewReason is null)
            {
                continue;
            }

            groups.Add(new FindingGroup
            {
                Id = nextGroupId++,
                Kind = FindingKind.ReviewCandidate,
                Reason = reviewReason,
                Photos = new[] { photo },
                RecommendedKeep = photo
            });
        }

        return new ScanResult
        {
            RootPath = normalizedRoots[0],
            RootPaths = normalizedRoots,
            Photos = photos,
            Groups = groups
                .OrderBy(group => group.Kind)
                .ThenByDescending(group => group.Photos.Sum(photo => photo.Size))
                .ToArray(),
            FailedToDecode = photos.Count(photo => photo.DecodeError is not null),
            RecognitionEnabled = enableRecognition,
            RecognitionStatus = recognitionStatus
        };
    }

    internal static bool IsSupportedMediaPath(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path)) ||
        VideoExtensions.Contains(Path.GetExtension(path));

    private static IEnumerable<(string Path, MediaKind Kind)> EnumerateMediaFiles(
        IReadOnlyList<string> rootPaths,
        CancellationToken cancellationToken)
    {
        var directories = new Stack<string>();
        foreach (var rootPath in rootPaths.Reverse())
        {
            directories.Push(rootPath);
        }

        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var yieldedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (directories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = Path.GetFullPath(directories.Pop());
            if (!visitedDirectories.Add(directory))
            {
                continue;
            }

            var directoryName = Path.GetFileName(directory);
            if (directoryName.StartsWith(".MediaTidy_Quarantine", StringComparison.OrdinalIgnoreCase) ||
                directoryName.StartsWith(".PhotoCleaner_Quarantine", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] childDirectories;
            string[] files;
            try
            {
                childDirectories = Directory.GetDirectories(directory);
                files = Directory.GetFiles(directory);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (!yieldedFiles.Add(file))
                {
                    continue;
                }

                if (ImageExtensions.Contains(Path.GetExtension(file)))
                {
                    yield return (file, MediaKind.Image);
                }
                else if (VideoExtensions.Contains(Path.GetExtension(file)))
                {
                    yield return (file, MediaKind.Video);
                }
            }

            foreach (var childDirectory in childDirectories)
            {
                try
                {
                    var attributes = File.GetAttributes(childDirectory);
                    if ((attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        directories.Push(childDirectory);
                    }
                }
                catch (IOException)
                {
                    // The directory may disappear during a long scan.
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip folders that cannot be inspected.
                }
            }
        }
    }

    private static void AnalyzeImage(PhotoRecord photo)
    {
        try
        {
            using var stream = new FileStream(
                photo.FullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);

            photo.HasCameraMetadata =
                image.PropertyIdList.Contains(0x0110) ||
                image.PropertyIdList.Contains(0x9003);
            photo.CapturedAtUtc = GetCapturedAtUtc(image, photo.LastWriteTimeUtc, out var dateSource);
            photo.DateSource = dateSource;

            ApplyExifOrientation(image);
            AnalyzeVisual(photo, image);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ExternalException or
            OutOfMemoryException or
            IOException or
            UnauthorizedAccessException)
        {
            photo.DecodeError = exception.Message;
            photo.CapturedAtUtc = photo.LastWriteTimeUtc;
            photo.DateSource = "Файл";
        }
    }

    private static async Task AnalyzeVideoAsync(
        PhotoRecord video,
        CancellationToken cancellationToken)
    {
        video.PrimaryCategory = PhotoCategory.Video;
        video.CategoryConfidence = 1;
        video.CategoryScores = new Dictionary<PhotoCategory, double>
        {
            [PhotoCategory.Video] = 1
        };

        try
        {
            using var thumbnail = await VideoThumbnailService.AnalyzeAsync(
                video,
                512,
                cancellationToken);
            if (thumbnail is null)
            {
                video.DecodeError = "Windows не смогла извлечь кадр видео";
                return;
            }

            AnalyzeVisual(video, thumbnail, preserveDimensions: true);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            ExternalException or
            COMException or
            InvalidOperationException)
        {
            video.DecodeError = exception.Message;
        }
    }

    private static void AnalyzeVisual(
        PhotoRecord photo,
        Image image,
        bool preserveDimensions = false)
    {
        if (!preserveDimensions || photo.Width <= 0 || photo.Height <= 0)
        {
            photo.Width = image.Width;
            photo.Height = image.Height;
        }

        photo.DifferenceHash = ComputeDifferenceHash(image);
        photo.DetailScore = ComputeDetailScore(image);
        photo.SharpnessScore = ComputeSharpnessScore(image);
    }

    internal static void ApplyExifOrientation(Image image)
    {
        const int orientationId = 0x0112;
        if (!image.PropertyIdList.Contains(orientationId))
        {
            return;
        }

        try
        {
            var orientationBytes = image.GetPropertyItem(orientationId)?.Value;
            if (orientationBytes is null || orientationBytes.Length == 0)
            {
                return;
            }

            var orientation = orientationBytes[0];
            var rotateFlip = orientation switch
            {
                2 => RotateFlipType.RotateNoneFlipX,
                3 => RotateFlipType.Rotate180FlipNone,
                4 => RotateFlipType.Rotate180FlipX,
                5 => RotateFlipType.Rotate90FlipX,
                6 => RotateFlipType.Rotate90FlipNone,
                7 => RotateFlipType.Rotate270FlipX,
                8 => RotateFlipType.Rotate270FlipNone,
                _ => RotateFlipType.RotateNoneFlipNone
            };
            image.RotateFlip(rotateFlip);
        }
        catch (ArgumentException)
        {
            // Invalid EXIF should not prevent analysis.
        }
    }

    private static ulong ComputeDifferenceHash(Image image)
    {
        using var resized = Resize(image, 9, 8);
        ulong hash = 0;
        var bit = 0;

        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var left = Luminance(resized.GetPixel(x, y));
                var right = Luminance(resized.GetPixel(x + 1, y));
                if (left > right)
                {
                    hash |= 1UL << bit;
                }

                bit++;
            }
        }

        return hash;
    }

    private static double ComputeDetailScore(Image image)
    {
        using var resized = Resize(image, 64, 64);
        double differences = 0;
        var comparisons = 0;

        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var current = Luminance(resized.GetPixel(x, y));
                if (x + 1 < 64)
                {
                    differences += Math.Abs(current - Luminance(resized.GetPixel(x + 1, y)));
                    comparisons++;
                }

                if (y + 1 < 64)
                {
                    differences += Math.Abs(current - Luminance(resized.GetPixel(x, y + 1)));
                    comparisons++;
                }
            }
        }

        return comparisons == 0 ? 0 : differences / comparisons;
    }

    private static double ComputeSharpnessScore(Image image)
    {
        const int size = 128;
        using var resized = Resize(image, size, size);
        var gray = new double[size, size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                gray[x, y] = Luminance(resized.GetPixel(x, y));
            }
        }

        double sum = 0;
        double sumSquares = 0;
        var count = 0;
        for (var y = 1; y < size - 1; y++)
        {
            for (var x = 1; x < size - 1; x++)
            {
                var laplacian =
                    gray[x - 1, y] +
                    gray[x + 1, y] +
                    gray[x, y - 1] +
                    gray[x, y + 1] -
                    (4 * gray[x, y]);
                sum += laplacian;
                sumSquares += laplacian * laplacian;
                count++;
            }
        }

        if (count == 0)
        {
            return 0;
        }

        var mean = sum / count;
        return Math.Max(0, (sumSquares / count) - (mean * mean));
    }

    private static Bitmap Resize(Image image, int width, int height)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.InterpolationMode = InterpolationMode.Bilinear;
        graphics.SmoothingMode = SmoothingMode.HighSpeed;
        graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
        graphics.DrawImage(image, 0, 0, width, height);
        return bitmap;
    }

    private static double Luminance(Color color) =>
        (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);

    private static DateTime GetCapturedAtUtc(
        Image image,
        DateTime fallbackUtc,
        out string source)
    {
        int[] propertyIds = [0x9003, 0x9004, 0x0132];
        foreach (var propertyId in propertyIds)
        {
            if (!image.PropertyIdList.Contains(propertyId))
            {
                continue;
            }

            try
            {
                var bytes = image.GetPropertyItem(propertyId)?.Value;
                if (bytes is null)
                {
                    continue;
                }

                var text = Encoding.ASCII.GetString(bytes).Trim('\0', ' ');
                if (DateTime.TryParseExact(
                        text,
                        "yyyy:MM:dd HH:mm:ss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal,
                        out var capturedAt))
                {
                    source = "EXIF";
                    return capturedAt.ToUniversalTime();
                }
            }
            catch (ArgumentException)
            {
                // Ignore malformed EXIF dates.
            }
        }

        source = "Файл";
        return fallbackUtc;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1024 * 1024,
            useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static PhotoRecord ChooseBest(IEnumerable<PhotoRecord> photos) =>
        photos
            .OrderByDescending(photo => photo.FaceAnalysisAvailable)
            .ThenBy(photo => photo.ClosedEyeCount >= 0 ? photo.ClosedEyeCount : int.MaxValue)
            .ThenByDescending(photo => photo.FaceCount)
            .ThenByDescending(photo => photo.SharpnessScore)
            .ThenByDescending(photo => photo.PixelCount)
            .ThenByDescending(photo => photo.HasCameraMetadata)
            .ThenByDescending(photo => photo.DetailScore)
            .ThenBy(photo => PathPenalty(photo.FullPath))
            .ThenBy(photo => photo.FullPath.Length)
            .ThenBy(photo => photo.LastWriteTimeUtc)
            .First();

    private static int PathPenalty(string path)
    {
        var lower = path.ToLowerInvariant();
        var penalty = 0;
        if (lower.Contains("download"))
        {
            penalty += 2;
        }

        if (lower.Contains("temp") || lower.Contains("cache"))
        {
            penalty += 3;
        }

        if (ScreenshotMarkers.Any(lower.Contains))
        {
            penalty += 2;
        }

        return penalty;
    }

    private static bool ArePlausiblyRelated(PhotoRecord left, PhotoRecord right)
    {
        if (left.MediaKind != right.MediaKind)
        {
            return false;
        }

        if (left.Width == 0 || left.Height == 0 || right.Width == 0 || right.Height == 0)
        {
            return false;
        }

        var leftRatio = (double)left.Width / left.Height;
        var rightRatio = (double)right.Width / right.Height;
        var ratioDifference = Math.Abs(Math.Log(leftRatio / rightRatio));

        return ratioDifference < 0.12;
    }

    private static string? GetReviewReason(PhotoRecord photo)
    {
        if (photo.PrimaryCategory is not PhotoCategory.Photo and not PhotoCategory.Unknown &&
            photo.CategoryConfidence >= 0.40)
        {
            return
                $"Распознано как «{CategoryNames.Russian(photo.PrimaryCategory)}» " +
                $"({photo.CategoryConfidence:P0})";
        }

        var fileName = Path.GetFileNameWithoutExtension(photo.FullPath).ToLowerInvariant();
        if (ScreenshotMarkers.Any(fileName.Contains))
        {
            return "Вероятный скриншот по имени файла";
        }

        if (MemeMarkers.Any(fileName.Contains))
        {
            return "Возможный мем по имени файла";
        }

        if (Path.GetExtension(photo.FullPath).Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            return "GIF: стоит проверить отдельно";
        }

        if (photo.Width > 0 && photo.Height > 0 && photo.PixelCount < 300_000)
        {
            return "Низкое разрешение";
        }

        if (photo.Width > 0 &&
            photo.Height > 0 &&
            !photo.HasCameraMetadata &&
            IsCommonScreenResolution(photo.Width, photo.Height))
        {
            return "Возможный скриншот: экранное разрешение без EXIF камеры";
        }

        return null;
    }

    private static bool IsCommonScreenResolution(int width, int height)
    {
        var shortSide = Math.Min(width, height);
        var longSide = Math.Max(width, height);
        return (shortSide, longSide) is
            (720, 1280) or
            (1080, 1920) or
            (1080, 2340) or
            (1080, 2400) or
            (1170, 2532) or
            (1284, 2778) or
            (1440, 2560) or
            (1440, 3040) or
            (1440, 3200);
    }
}
