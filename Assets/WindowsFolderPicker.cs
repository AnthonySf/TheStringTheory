using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public static class WindowsFolderPicker
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const uint BIF_RETURNONLYFSDIRS = 0x0001;
    private const uint BIF_EDITBOX = 0x0010;
    private const uint BIF_NEWDIALOGSTYLE = 0x0040;
    private const uint BIF_USENEWUI = BIF_EDITBOX | BIF_NEWDIALOGSTYLE;
    private const uint BFFM_INITIALIZED = 1;
    private const uint BFFM_SETSELECTIONW = 0x0467;
    private const int MaxPath = 260;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BrowseInfo
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public IntPtr pszDisplayName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszTitle;
        public uint ulFlags;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public BrowseCallbackProc lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    private delegate int BrowseCallbackProc(IntPtr hwnd, uint msg, IntPtr lParam, IntPtr lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHBrowseForFolder(ref BrowseInfo lpbi);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder pszPath);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();
#endif

    public static bool TryPickFolder(string title, string initialDirectory, out string selectedPath)
    {
        selectedPath = string.Empty;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        IntPtr displayNameBuffer = IntPtr.Zero;
        IntPtr initialDirectoryPtr = IntPtr.Zero;
        IntPtr itemIdList = IntPtr.Zero;
        try
        {
            displayNameBuffer = Marshal.AllocHGlobal(MaxPath * sizeof(char));
            string normalizedInitialDirectory = NormalizeInitialDirectory(initialDirectory);
            if (!string.IsNullOrWhiteSpace(normalizedInitialDirectory))
                initialDirectoryPtr = Marshal.StringToHGlobalUni(normalizedInitialDirectory);

            BrowseCallbackProc callback = BrowseCallback;
            BrowseInfo info = new BrowseInfo
            {
                hwndOwner = GetActiveWindow(),
                pidlRoot = IntPtr.Zero,
                pszDisplayName = displayNameBuffer,
                lpszTitle = string.IsNullOrWhiteSpace(title) ? "Select Folder" : title,
                ulFlags = BIF_RETURNONLYFSDIRS | BIF_USENEWUI,
                lpfn = callback,
                lParam = initialDirectoryPtr,
                iImage = 0
            };

            itemIdList = SHBrowseForFolder(ref info);
            if (itemIdList == IntPtr.Zero)
                return false;

            StringBuilder pathBuilder = new StringBuilder(MaxPath);
            if (!SHGetPathFromIDList(itemIdList, pathBuilder))
                return false;

            selectedPath = pathBuilder.ToString().Trim();
            return !string.IsNullOrWhiteSpace(selectedPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WindowsFolderPicker] Failed to open folder picker: {ex.Message}");
            selectedPath = string.Empty;
            return false;
        }
        finally
        {
            if (itemIdList != IntPtr.Zero)
                CoTaskMemFree(itemIdList);
            if (initialDirectoryPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(initialDirectoryPtr);
            if (displayNameBuffer != IntPtr.Zero)
                Marshal.FreeHGlobal(displayNameBuffer);
        }
#else
        return false;
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private static int BrowseCallback(IntPtr hwnd, uint msg, IntPtr lParam, IntPtr lpData)
    {
        if (msg == BFFM_INITIALIZED && lpData != IntPtr.Zero)
            SendMessage(hwnd, BFFM_SETSELECTIONW, new IntPtr(1), lpData);

        return 0;
    }

    private static string NormalizeInitialDirectory(string initialDirectory)
    {
        if (string.IsNullOrWhiteSpace(initialDirectory))
            return string.Empty;

        try
        {
            return System.IO.Path.GetFullPath(initialDirectory.Trim());
        }
        catch
        {
            return string.Empty;
        }
    }
#endif
}
