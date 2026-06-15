using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Media.FaceAnalysis;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MediaTidy;

internal sealed class WindowsFaceAnalysisService
{
    private readonly Lazy<Task<FaceDetector?>> _detector = new(CreateDetectorAsync);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task AnalyzeAsync(
        PhotoRecord photo,
        CancellationToken cancellationToken)
    {
        var detector = await _detector.Value;
        if (detector is null)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(photo.FullPath);
            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var maximum = Math.Max(decoder.PixelWidth, decoder.PixelHeight);
            var transform = new BitmapTransform();
            if (maximum > 1280)
            {
                var scale = 1280D / maximum;
                transform.ScaledWidth =
                    Math.Max(1, (uint)Math.Round(decoder.PixelWidth * scale));
                transform.ScaledHeight =
                    Math.Max(1, (uint)Math.Round(decoder.PixelHeight * scale));
            }

            using var bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage);
            cancellationToken.ThrowIfCancellationRequested();
            var faces = await detector.DetectFacesAsync(bitmap);
            photo.FaceCount = faces.Count;
            photo.FaceAnalysisAvailable = true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
            ArgumentException or InvalidOperationException or COMException)
        {
            photo.FaceAnalysisAvailable = false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<FaceDetector?> CreateDetectorAsync()
    {
        try
        {
            return FaceDetector.IsSupported
                ? await FaceDetector.CreateAsync()
                : null;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or COMException)
        {
            return null;
        }
    }
}
