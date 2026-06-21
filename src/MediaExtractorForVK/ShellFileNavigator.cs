using System.Runtime.InteropServices;

namespace MediaExtractorForVK;

internal static partial class ShellFileNavigator
{
    public static bool SelectFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var itemIdList = ILCreateFromPath(path);
        if (itemIdList == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return SHOpenFolderAndSelectItems(itemIdList, 0, IntPtr.Zero, 0) >= 0;
        }
        finally
        {
            ILFree(itemIdList);
        }
    }

    [LibraryImport("shell32.dll", EntryPoint = "ILCreateFromPathW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr ILCreateFromPath(string path);

    [LibraryImport("shell32.dll")]
    private static partial int SHOpenFolderAndSelectItems(
        IntPtr folderItemIdList,
        uint itemCount,
        IntPtr childItemIdLists,
        uint flags);

    [LibraryImport("shell32.dll")]
    private static partial void ILFree(IntPtr itemIdList);
}
