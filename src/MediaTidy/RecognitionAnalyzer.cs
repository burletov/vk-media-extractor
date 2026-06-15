namespace MediaTidy;

internal sealed class RecognitionAnalyzer
{
    public async Task<string> AnalyzeAsync(
        string rootPath,
        IReadOnlyList<PhotoRecord> photos,
        RecognitionModelInfo model,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ClipImageClassifier classifier;
        try
        {
            classifier = new ClipImageClassifier(model);
        }
        catch (Exception exception)
        {
            foreach (var photo in photos)
            {
                photo.RecognitionError = exception.Message;
            }

            return $"CLIP недоступен: {exception.Message}";
        }

        using (classifier)
        {
            var ocr = new WindowsOcrService();
            var cache = await RecognitionCache.LoadAsync(
                rootPath,
                model.CacheVersion,
                cancellationToken);
            var pending = new List<PhotoRecord>();
            var completed = 0;

            foreach (var photo in photos.Where(photo => photo.DecodeError is null))
            {
                if (cache.TryApply(photo))
                {
                    completed++;
                }
                else
                {
                    pending.Add(photo);
                }
            }
            var total = completed + pending.Count;

            progress?.Report(new ScanProgress
            {
                Stage = "Локальное распознавание CLIP и OCR",
                Current = completed,
                Total = total
            });

            await Parallel.ForEachAsync(
                pending,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Math.Min(2, Math.Max(1, Environment.ProcessorCount / 4))
                },
                async (photo, token) =>
                {
                    try
                    {
                        var ocrAnalysis = await ocr.AnalyzeAsync(photo.FullPath, token);
                        var scores = classifier.Classify(photo, ocrAnalysis);
                        var primary = scores.MaxBy(pair => pair.Value);

                        photo.OcrWordCount = ocrAnalysis.WordCount;
                        photo.OcrTextLength = ocrAnalysis.TextLength;
                        photo.OcrTextAreaRatio = ocrAnalysis.TextAreaRatio;
                        photo.OcrPreview = ocrAnalysis.Preview;
                        photo.CategoryScores = scores;
                        ApplyConservativeDecision(photo, ocrAnalysis, primary);
                        cache.Store(photo);
                    }
                    catch (Exception exception) when (
                        exception is IOException or
                        UnauthorizedAccessException or
                        ArgumentException or
                        InvalidOperationException or
                        Microsoft.ML.OnnxRuntime.OnnxRuntimeException)
                    {
                        photo.RecognitionError = exception.Message;
                    }

                    var current = Interlocked.Increment(ref completed);
                    progress?.Report(new ScanProgress
                    {
                        Stage = "Локальное распознавание CLIP и OCR",
                        Current = current,
                        Total = total
                    });
                });

            await cache.SaveAsync(cancellationToken);
            return ocr.IsAvailable
                ? $"{model.DisplayName}; Windows OCR; из кэша: {photos.Count(photo => photo.RecognitionFromCache):N0}"
                : $"{model.DisplayName}; Windows OCR недоступен";
        }
    }

    internal static void ApplyConservativeDecision(
        PhotoRecord photo,
        OcrAnalysis ocr,
        KeyValuePair<PhotoCategory, double> primary)
    {
        var ordered = photo.CategoryScores
            .OrderByDescending(pair => pair.Value)
            .ToArray();
        var second = ordered.Length > 1 ? ordered[1].Value : 0;
        var margin = primary.Value - second;
        var searchable =
            $"{Path.GetFileName(photo.FullPath)} {Path.GetDirectoryName(photo.FullPath)}"
                .ToLowerInvariant();
        var recognizedText = ocr.Preview.ToLowerInvariant();

        var decision = PhotoCategory.Unknown;
        var confidence = 0.0;

        if (ContainsAny(searchable, "screenshot", "screen_capture", "скриншот", "снимок экрана"))
        {
            decision = PhotoCategory.Screenshot;
            confidence = 0.98;
        }
        else if (ContainsAny(searchable, "meme", "мем", "reaction"))
        {
            decision = PhotoCategory.Meme;
            confidence = 0.95;
        }
        else if (ocr.WordCount >= 3 &&
                 ContainsAny(
                     recognizedText,
                     "total", "subtotal", "tax", "cash", "visa", "mastercard",
                     "итого", "сумма", "касса", "чек", "ндс", "руб"))
        {
            decision = PhotoCategory.Receipt;
            confidence = 0.92;
        }
        else if (photo.HasCameraMetadata && photo.SharpnessScore < 8)
        {
            decision = PhotoCategory.Blurry;
            confidence = 0.86;
        }
        else if (photo.HasCameraMetadata && ocr.WordCount < 5)
        {
            decision = PhotoCategory.Photo;
            confidence = 0.90;
        }
        else if (ocr.WordCount >= 24 && ocr.TextAreaRatio >= 0.025)
        {
            decision = PhotoCategory.Document;
            confidence = Math.Max(0.75, primary.Key == PhotoCategory.Document
                ? primary.Value
                : 0);
        }
        else if (!photo.HasCameraMetadata &&
                 ocr.WordCount >= 6 &&
                 Math.Max(photo.AspectRatio, photo.AspectRatio == 0 ? 0 : 1 / photo.AspectRatio) >= 1.45)
        {
            decision = PhotoCategory.Screenshot;
            confidence = 0.72;
        }
        else if (IsModelDecisionSupported(primary.Key, photo, ocr) &&
                 primary.Value >= 0.72 &&
                 margin >= 0.25)
        {
            decision = primary.Key;
            confidence = primary.Value;
        }

        photo.PrimaryCategory = decision;
        photo.CategoryConfidence = confidence;
    }

    private static bool IsModelDecisionSupported(
        PhotoCategory category,
        PhotoRecord photo,
        OcrAnalysis ocr) => category switch
    {
        PhotoCategory.Photo => photo.HasCameraMetadata ||
                               (ocr.WordCount == 0 && photo.PixelCount >= 2_000_000),
        PhotoCategory.Screenshot => !photo.HasCameraMetadata && ocr.WordCount >= 3,
        PhotoCategory.Meme => !photo.HasCameraMetadata && ocr.WordCount is >= 1 and <= 30,
        PhotoCategory.Document => ocr.WordCount >= 8,
        PhotoCategory.Receipt => ocr.WordCount >= 5,
        PhotoCategory.Blurry => photo.HasCameraMetadata && photo.SharpnessScore < 15,
        PhotoCategory.Graphic => !photo.HasCameraMetadata && ocr.WordCount <= 5,
        _ => false
    };

    private static bool ContainsAny(string text, params string[] markers) =>
        markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
