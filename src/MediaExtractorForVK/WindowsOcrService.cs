using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
using System.Runtime.InteropServices;

namespace MediaExtractorForVK;

internal sealed class WindowsOcrService
{
    private readonly OcrEngine? _engine = OcrEngine.TryCreateFromUserProfileLanguages();

    public bool IsAvailable => _engine is not null;

    public async Task<OcrAnalysis> AnalyzeAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (_engine is null)
        {
            return OcrAnalysis.Empty;
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);

            var transform = new BitmapTransform();
            var width = decoder.PixelWidth;
            var height = decoder.PixelHeight;
            var maximum = (uint)OcrEngine.MaxImageDimension;
            if (Math.Max(width, height) > maximum)
            {
                var scale = (double)maximum / Math.Max(width, height);
                transform.ScaledWidth = Math.Max(1, (uint)Math.Round(width * scale));
                transform.ScaledHeight = Math.Max(1, (uint)Math.Round(height * scale));
            }

            using var bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage);
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _engine.RecognizeAsync(bitmap);
            var words = result.Lines.SelectMany(line => line.Words).ToArray();
            var text = string.Join(" ", result.Lines.Select(line => line.Text)).Trim();
            var imageArea = Math.Max(1.0, bitmap.PixelWidth * (double)bitmap.PixelHeight);
            var wordArea = words.Sum(word =>
                Math.Max(0, word.BoundingRect.Width) *
                Math.Max(0, word.BoundingRect.Height));

            return new OcrAnalysis
            {
                WordCount = words.Length,
                TextLength = text.Length,
                TextAreaRatio = Math.Min(1.0, wordArea / imageArea),
                Preview = text.Length <= 500 ? text : text[..500]
            };
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            InvalidOperationException or
            COMException)
        {
            return OcrAnalysis.Empty;
        }
    }
}
