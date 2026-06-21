using System.Runtime.InteropServices;

namespace MediaExtractorForVK;

internal static class NativeFolderPicker
{
    public static IReadOnlyList<string> PickFolders(
        IWin32Window owner,
        string? initialFolder)
    {
        try
        {
            return PickFoldersCore(owner, initialFolder);
        }
        catch (COMException)
        {
            using var fallback = new FolderBrowserDialog
            {
                Description = "Выберите папку. Дополнительные папки можно добавить повторно.",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true,
                InitialDirectory = Directory.Exists(initialFolder)
                    ? initialFolder
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };
            return fallback.ShowDialog(owner) == DialogResult.OK
                ? [fallback.SelectedPath]
                : Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> PickFoldersCore(
        IWin32Window owner,
        string? initialFolder)
    {
        var dialogType = Type.GetTypeFromCLSID(
            new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7"),
            throwOnError: true)!;
        var dialog = (IFileOpenDialog)Activator.CreateInstance(dialogType)!;
        try
        {
            dialog.GetOptions(out var options);
            dialog.SetOptions(
                options |
                FileOpenOptions.PickFolders |
                FileOpenOptions.AllowMultiSelect |
                FileOpenOptions.ForceFileSystem);
            dialog.SetTitle("Выберите одну или несколько папок");
            dialog.SetOkButtonLabel("Добавить");

            if (!string.IsNullOrWhiteSpace(initialFolder) &&
                Directory.Exists(initialFolder) &&
                SHCreateItemFromParsingName(
                    initialFolder,
                    IntPtr.Zero,
                    typeof(IShellItem).GUID,
                    out var initialItem) == 0)
            {
                dialog.SetFolder(initialItem);
                Marshal.ReleaseComObject(initialItem);
            }

            var result = dialog.Show(owner.Handle);
            if (result == unchecked((int)0x800704C7))
            {
                return Array.Empty<string>();
            }

            Marshal.ThrowExceptionForHR(result);
            dialog.GetResults(out var items);
            try
            {
                items.GetCount(out var count);
                var paths = new List<string>((int)count);
                for (uint index = 0; index < count; index++)
                {
                    items.GetItemAt(index, out var item);
                    try
                    {
                        item.GetDisplayName(ShellItemDisplayName.FileSystemPath, out var pointer);
                        try
                        {
                            var path = Marshal.PtrToStringUni(pointer);
                            if (!string.IsNullOrWhiteSpace(path))
                            {
                                paths.Add(path);
                            }
                        }
                        finally
                        {
                            Marshal.FreeCoTaskMem(pointer);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(item);
                    }
                }

                return paths;
            }
            finally
            {
                Marshal.ReleaseComObject(items);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        [MarshalAs(UnmanagedType.LPStruct)] Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

    [Flags]
    private enum FileOpenOptions : uint
    {
        PickFolders = 0x20,
        ForceFileSystem = 0x40,
        AllowMultiSelect = 0x200
    }

    private enum ShellItemDisplayName : uint
    {
        FileSystemPath = 0x80058000
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(ShellItemDisplayName displayName, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport]
    [Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray
    {
        void BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr result);
        void GetPropertyStore(int flags, ref Guid interfaceId, out IntPtr result);
        void GetPropertyDescriptionList(IntPtr propertyKey, ref Guid interfaceId, out IntPtr result);
        void GetAttributes(uint flags, uint mask, out uint attributes);
        void GetCount(out uint count);
        void GetItemAt(uint index, out IShellItem item);
        void EnumItems(out IntPtr enumerator);
    }

    [ComImport]
    [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig]
        int Show(IntPtr owner);
        void SetFileTypes(uint count, IntPtr filterSpec);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(FileOpenOptions options);
        void GetOptions(out FileOpenOptions options);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, int placement);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int result);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr filter);
        void GetResults(out IShellItemArray items);
        void GetSelectedItems(out IShellItemArray items);
    }
}
