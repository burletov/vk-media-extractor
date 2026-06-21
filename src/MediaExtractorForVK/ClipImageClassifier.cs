using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MediaExtractorForVK;

internal sealed class ClipImageClassifier : IDisposable
{
    private const int InputSize = 224;
    private static readonly float[] Mean = [0.48145466f, 0.4578275f, 0.40821073f];
    private static readonly float[] StandardDeviation = [0.26862954f, 0.26130258f, 0.27577711f];

    private readonly InferenceSession _session;
    private readonly IReadOnlyDictionary<PhotoCategory, float[]> _classEmbeddings;

    public ClipImageClassifier(RecognitionModelInfo model)
    {
        var modelPath = RecognitionModels.ModelPath(model);
        var embeddingsPath = RecognitionModels.EmbeddingsPath(model);
        if (!File.Exists(modelPath) || !File.Exists(embeddingsPath))
        {
            throw new FileNotFoundException("Файлы локальной CLIP-модели не найдены.");
        }

        _classEmbeddings = LoadClassEmbeddings(embeddingsPath);
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount - 1),
            InterOpNumThreads = 1
        };
        _session = new InferenceSession(modelPath, options);
    }

    public IReadOnlyDictionary<PhotoCategory, double> Classify(
        PhotoRecord photo,
        OcrAnalysis ocr)
    {
        using var image = LoadOrientedImage(photo.FullPath);
        using var inputBitmap = CreateClipInput(image);
        var input = ToTensor(inputBitmap);

#pragma warning disable CS0618
        using var results = _session.Run(
        [
            NamedOnnxValue.CreateFromTensor("pixel_values", input)
        ]);
#pragma warning restore CS0618

        var embedding = results.First(result => result.Name == "image_embeds")
            .AsEnumerable<float>()
            .ToArray();
        Normalize(embedding);

        var logits = _classEmbeddings.ToDictionary(
            pair => pair.Key,
            pair => 12.0 * Dot(embedding, pair.Value));

        ApplyMetadataSignals(logits, photo, ocr);
        return Softmax(logits);
    }

    public void Dispose() => _session.Dispose();

    private static IReadOnlyDictionary<PhotoCategory, float[]> LoadClassEmbeddings(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var classes = document.RootElement.GetProperty("classes");
        var result = new Dictionary<PhotoCategory, float[]>();

        foreach (var property in classes.EnumerateObject())
        {
            var category = CategoryNames.ParseModelName(property.Name);
            if (category == PhotoCategory.Unknown)
            {
                continue;
            }

            result[category] = property.Value
                .EnumerateArray()
                .Select(value => value.GetSingle())
                .ToArray();
        }

        return result;
    }

    private static Image LoadOrientedImage(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var source = Image.FromStream(
            stream,
            useEmbeddedColorManagement: false,
            validateImageData: false);
        var copy = new Bitmap(source);
        ImageScanner.ApplyExifOrientation(copy);
        return copy;
    }

    private static Bitmap CreateClipInput(Image image)
    {
        var side = Math.Min(image.Width, image.Height);
        var sourceX = (image.Width - side) / 2f;
        var sourceY = (image.Height - side) / 2f;
        var bitmap = new Bitmap(InputSize, InputSize, PixelFormat.Format24bppRgb);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(
            image,
            new Rectangle(0, 0, InputSize, InputSize),
            sourceX,
            sourceY,
            side,
            side,
            GraphicsUnit.Pixel);
        return bitmap;
    }

    private static DenseTensor<float> ToTensor(Bitmap bitmap)
    {
        var data = new float[3 * InputSize * InputSize];
        var bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, InputSize, InputSize),
            ImageLockMode.ReadOnly,
            PixelFormat.Format24bppRgb);

        try
        {
            unsafe
            {
                var start = (byte*)bitmapData.Scan0;
                var plane = InputSize * InputSize;
                for (var y = 0; y < InputSize; y++)
                {
                    var row = start + (y * bitmapData.Stride);
                    for (var x = 0; x < InputSize; x++)
                    {
                        var pixel = row + (x * 3);
                        var offset = (y * InputSize) + x;
                        data[offset] = ((pixel[2] / 255f) - Mean[0]) / StandardDeviation[0];
                        data[plane + offset] = ((pixel[1] / 255f) - Mean[1]) / StandardDeviation[1];
                        data[(2 * plane) + offset] =
                            ((pixel[0] / 255f) - Mean[2]) / StandardDeviation[2];
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return new DenseTensor<float>(data, [1, 3, InputSize, InputSize]);
    }

    private static void ApplyMetadataSignals(
        Dictionary<PhotoCategory, double> logits,
        PhotoRecord photo,
        OcrAnalysis ocr)
    {
        var searchable = $"{Path.GetFileName(photo.FullPath)} {Path.GetDirectoryName(photo.FullPath)}"
            .ToLowerInvariant();

        if (photo.HasCameraMetadata)
        {
            logits[PhotoCategory.Photo] += 1.8;
            logits[PhotoCategory.Screenshot] -= 0.8;
            logits[PhotoCategory.Document] -= 0.3;
        }

        AddForMarkers(logits, searchable, PhotoCategory.Screenshot, 4.5,
            "screenshot", "screen shot", "скриншот", "снимок экрана", "screen_capture");
        AddForMarkers(logits, searchable, PhotoCategory.Meme, 4.5,
            "meme", "мем", "reaction");
        AddForMarkers(logits, searchable, PhotoCategory.Receipt, 3.0,
            "receipt", "invoice", "чек", "квитанц", "касс");
        AddForMarkers(logits, searchable, PhotoCategory.Document, 2.5,
            "document", "scan", "скан", "документ", "passport", "договор");
        AddForMarkers(logits, searchable, PhotoCategory.Graphic, 1.8,
            "logo", "poster", "wallpaper", "icon", "artwork", "баннер");
        AddForMarkers(logits, searchable, PhotoCategory.Photo, 1.2,
            "dcim", "camera", "photos", "pictures", "фото");

        if (ocr.WordCount >= 30)
        {
            logits[PhotoCategory.Document] += 3.0;
            logits[PhotoCategory.Screenshot] += 1.0;
            logits[PhotoCategory.Photo] -= 1.0;
        }
        else if (ocr.WordCount >= 5)
        {
            logits[PhotoCategory.Screenshot] += 1.2;
            logits[PhotoCategory.Document] += 1.0;
            logits[PhotoCategory.Meme] += 0.6;
        }

        if (ocr.TextAreaRatio >= 0.12)
        {
            logits[PhotoCategory.Document] += 1.5;
            logits[PhotoCategory.Screenshot] += 1.0;
            logits[PhotoCategory.Meme] += 0.7;
        }

        var recognizedText = ocr.Preview.ToLowerInvariant();
        if (ContainsAny(
                recognizedText,
                "total", "subtotal", "tax", "cash", "visa", "mastercard",
                "итого", "сумма", "касса", "чек", "ндс", "руб"))
        {
            logits[PhotoCategory.Receipt] += 4.0;
        }

        var aspect = photo.AspectRatio;
        var portraitAspect = aspect == 0 ? 0 : Math.Max(aspect, 1 / aspect);
        if (!photo.HasCameraMetadata && portraitAspect >= 2.0)
        {
            logits[PhotoCategory.Screenshot] += 1.4;
            logits[PhotoCategory.Receipt] += 0.5;
        }

        if (ocr.WordCount >= 20 && aspect is >= 0.60 and <= 0.85)
        {
            logits[PhotoCategory.Document] += 1.0;
        }

        if (photo.SharpnessScore < 8)
        {
            logits[PhotoCategory.Blurry] += photo.HasCameraMetadata ? 5.0 : 2.0;
        }
        else if (photo.SharpnessScore < 20)
        {
            logits[PhotoCategory.Blurry] += photo.HasCameraMetadata ? 3.0 : 1.0;
        }
        else if (photo.SharpnessScore > 100)
        {
            logits[PhotoCategory.Blurry] -= 2.0;
        }

        if (!photo.HasCameraMetadata && ocr.WordCount == 0 && photo.PixelCount < 1_000_000)
        {
            logits[PhotoCategory.Graphic] += 0.6;
        }
    }

    private static void AddForMarkers(
        IDictionary<PhotoCategory, double> logits,
        string text,
        PhotoCategory category,
        double bonus,
        params string[] markers)
    {
        if (ContainsAny(text, markers))
        {
            logits[category] += bonus;
        }
    }

    private static bool ContainsAny(string text, params string[] markers) =>
        markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<PhotoCategory, double> Softmax(
        IReadOnlyDictionary<PhotoCategory, double> logits)
    {
        var maximum = logits.Values.Max();
        var exponentials = logits.ToDictionary(
            pair => pair.Key,
            pair => Math.Exp(pair.Value - maximum));
        var total = exponentials.Values.Sum();
        return exponentials.ToDictionary(pair => pair.Key, pair => pair.Value / total);
    }

    private static double Dot(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        double result = 0;
        for (var index = 0; index < left.Count; index++)
        {
            result += left[index] * right[index];
        }

        return result;
    }

    private static void Normalize(float[] vector)
    {
        double squaredNorm = 0;
        foreach (var value in vector)
        {
            squaredNorm += value * value;
        }

        var norm = Math.Sqrt(squaredNorm);
        if (norm == 0)
        {
            return;
        }

        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] = (float)(vector[index] / norm);
        }
    }
}
