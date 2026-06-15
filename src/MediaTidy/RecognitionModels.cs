using System.Net;
using System.Net.Http.Headers;

namespace MediaTidy;

internal sealed record RecognitionModelInfo(
    string Id,
    string DisplayName,
    string Description,
    string ModelFileName,
    string EmbeddingsFileName,
    string CacheVersion,
    bool Bundled,
    Uri? DownloadUri,
    long DownloadSizeBytes,
    int MinimumRamGb,
    int RecommendedRamGb)
{
    public string Requirements =>
        $"Размер: {FormatSize(DownloadSizeBytes)}. ОЗУ: минимум {MinimumRamGb} ГБ, " +
        $"рекомендуется {RecommendedRamGb} ГБ. Выполнение: локально на CPU.";

    private static string FormatSize(long bytes) =>
        $"{bytes / 1024D / 1024D:0} МБ";
}

internal static class RecognitionModels
{
    public const string StandardId = "clip-b32";
    public const string StrongId = "clip-b16";
    public const string LargeId = "clip-l14";

    public static readonly IReadOnlyList<RecognitionModelInfo> All =
    [
        new(
            StandardId,
            "CLIP ViT-B/32 INT8",
            "Быстрая модель общего назначения, уже включённая в приложение. " +
            "Она выбрана по умолчанию, чтобы анализ работал без отдельной загрузки и не требовал мощного компьютера.",
            "clip-vision-int8.onnx",
            "clip-class-embeddings.json",
            "clip-vit-base-patch32-int8-conservative-v4",
            true,
            null,
            89_117_001,
            4,
            8),
        new(
            StrongId,
            "CLIP ViT-B/16 INT8",
            "Более плотная сетка изображения помогает различать мелкие детали. " +
            "Обычно работает медленнее B/32; результат всё равно остаётся вероятностной подсказкой.",
            "clip-b16-vision-int8.onnx",
            "clip-b16-class-embeddings.json",
            "clip-vit-base-patch16-q8-conservative-v4",
            false,
            new Uri(
                "https://huggingface.co/Xenova/clip-vit-base-patch16/resolve/main/onnx/vision_model_quantized.onnx"),
            87_500_000,
            6,
            8),
        new(
            LargeId,
            "CLIP ViT-L/14 INT8",
            "Самая тяжёлая модель в каталоге. Лучше подходит для сложных сцен и мелких визуальных различий, " +
            "но заметно увеличивает время анализа и расход памяти.",
            "clip-l14-vision-int8.onnx",
            "clip-l14-class-embeddings.json",
            "clip-vit-large-patch14-int8-conservative-v1",
            false,
            new Uri(
                "https://huggingface.co/Xenova/clip-vit-large-patch14/resolve/main/onnx/vision_model_int8.onnx"),
            306_000_000,
            12,
            16)
    ];

    public static RecognitionModelInfo Get(string? id) =>
        All.FirstOrDefault(model => model.Id == id) ?? All[0];

    public static string ModelDirectory(RecognitionModelInfo model)
    {
        if (model.Bundled)
        {
            return Path.Combine(AppContext.BaseDirectory, "Models");
        }

        var localAppData =
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var preferred = Path.Combine(localAppData, "MediaTidy", "Models", model.Id);
        var legacy = Path.Combine(localAppData, "MediaParserForVK", "Models", model.Id);
        if (!File.Exists(Path.Combine(preferred, model.ModelFileName)) &&
            File.Exists(Path.Combine(legacy, model.ModelFileName)))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(preferred)!);
                if (!Directory.Exists(preferred))
                {
                    Directory.Move(legacy, preferred);
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                return legacy;
            }
        }

        return preferred;
    }

    public static string ModelPath(RecognitionModelInfo model) =>
        Path.Combine(ModelDirectory(model), model.ModelFileName);

    public static string EmbeddingsPath(RecognitionModelInfo model)
    {
        if (model.Bundled)
        {
            return Path.Combine(ModelDirectory(model), model.EmbeddingsFileName);
        }

        var installed = Path.Combine(ModelDirectory(model), model.EmbeddingsFileName);
        return File.Exists(installed)
            ? installed
            : Path.Combine(AppContext.BaseDirectory, "Models", model.EmbeddingsFileName);
    }

    public static bool IsInstalled(RecognitionModelInfo model) =>
        File.Exists(ModelPath(model)) && File.Exists(EmbeddingsPath(model));
}

internal sealed class RecognitionModelInstaller
{
    private readonly HttpClient _client;

    public RecognitionModelInstaller(HttpClient? client = null)
    {
        _client = client ?? new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MediaTidy", "2.6.2"));
    }

    public async Task InstallAsync(
        RecognitionModelInfo model,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        if (model.Bundled || model.DownloadUri is null)
        {
            return;
        }

        var directory = RecognitionModels.ModelDirectory(model);
        Directory.CreateDirectory(directory);
        var targetPath = RecognitionModels.ModelPath(model);
        var temporaryPath = targetPath + ".part";
        var existingBytes = File.Exists(temporaryPath)
            ? new FileInfo(temporaryPath).Length
            : 0;
        using var request = new HttpRequestMessage(HttpMethod.Get, model.DownloadUri);
        if (existingBytes > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingBytes, null);
        }

        using var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            File.Delete(temporaryPath);
            throw new IOException(
                "Сервер отклонил продолжение модели. Запустите загрузку ещё раз.");
        }

        response.EnsureSuccessStatusCode();
        var append = existingBytes > 0 &&
                     response.StatusCode == HttpStatusCode.PartialContent;
        if (!append)
        {
            existingBytes = 0;
        }

        var contentLength = response.Content.Headers.ContentLength;
        var total = contentLength.HasValue
            ? existingBytes + contentLength.Value
            : model.DownloadSizeBytes;
        {
            await using var source =
                await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(
                temporaryPath,
                append ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                useAsync: true);
            var buffer = new byte[1024 * 1024];
            var written = existingBytes;
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                written += read;
                if (total > 0)
                {
                    progress?.Report((int)Math.Clamp(written * 100 / total, 0, 100));
                }
            }

            await destination.FlushAsync(cancellationToken);
        }

        if (new FileInfo(temporaryPath).Length < model.DownloadSizeBytes * 0.7)
        {
            throw new InvalidDataException(
                "Сервер вернул неполный файл модели. Частичная загрузка сохранена для продолжения.");
        }

        File.Move(temporaryPath, targetPath, overwrite: true);
        var bundledEmbeddings = Path.Combine(
            AppContext.BaseDirectory,
            "Models",
            model.EmbeddingsFileName);
        if (!File.Exists(bundledEmbeddings))
        {
            throw new FileNotFoundException(
                "В приложении нет совместимых эталонов классов для этой модели.",
                bundledEmbeddings);
        }

        File.Copy(
            bundledEmbeddings,
            Path.Combine(directory, model.EmbeddingsFileName),
            overwrite: true);
        progress?.Report(100);
    }
}
