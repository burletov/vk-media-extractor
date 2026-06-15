using System.Runtime.InteropServices;
using Windows.Storage;

namespace MediaTidy;

internal static class VideoThumbnailService
{
    private static readonly Guid ImageFactoryId =
        new("BCC18B79-BA16-442F-80C4-8A59C30C463B");

    public static async Task<Bitmap?> AnalyzeAsync(
        PhotoRecord video,
        int thumbnailSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(video.FullPath);
            var properties = await file.Properties.GetVideoPropertiesAsync();
            video.Width = checked((int)properties.Width);
            video.Height = checked((int)properties.Height);
            video.Duration = properties.Duration;
            if (file.DateCreated.Year > 1900)
            {
                video.CapturedAtUtc = file.DateCreated.UtcDateTime;
                video.DateSource = "Дата создания видео";
            }
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            InvalidOperationException or
            COMException)
        {
            // The thumbnail provider may still work when metadata is unavailable.
        }

        cancellationToken.ThrowIfCancellationRequested();
        return GetShellThumbnail(video.FullPath, thumbnailSize);
    }

    public static Bitmap? GetShellThumbnail(string path, int size)
    {
        IShellItemImageFactory? factory = null;
        IntPtr bitmapHandle = IntPtr.Zero;
        try
        {
            SHCreateItemFromParsingName(
                path,
                IntPtr.Zero,
                ImageFactoryId,
                out factory);
            var result = factory.GetImage(
                new NativeSize(size, size),
                ShellImageFlags.BiggerSizeOk |
                ShellImageFlags.ThumbnailOnly |
                ShellImageFlags.ScaleUp,
                out bitmapHandle);
            if (result < 0 || bitmapHandle == IntPtr.Zero)
            {
                return null;
            }

            using var source = Image.FromHbitmap(bitmapHandle);
            return new Bitmap(source);
        }
        catch (Exception exception) when (
            exception is COMException or
            ExternalException or
            ArgumentException)
        {
            return null;
        }
        finally
        {
            if (bitmapHandle != IntPtr.Zero)
            {
                DeleteObject(bitmapHandle);
            }

            if (factory is not null)
            {
                Marshal.FinalReleaseComObject(factory);
            }
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr bindContext,
        [MarshalAs(UnmanagedType.LPStruct)] Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory imageFactory);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr objectHandle);

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(
            NativeSize size,
            ShellImageFlags flags,
            out IntPtr bitmapHandle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize
    {
        public NativeSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public readonly int Width;
        public readonly int Height;
    }

    [Flags]
    private enum ShellImageFlags
    {
        BiggerSizeOk = 0x1,
        ThumbnailOnly = 0x8,
        ScaleUp = 0x100
    }
}
