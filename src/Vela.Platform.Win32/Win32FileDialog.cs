using System.Runtime.InteropServices;
using System.Text;

namespace Vela.Platform.Win32;

/// <summary>
/// Windows 原生文件/文件夹对话框封装。
///
/// 使用 comdlg32 的 GetOpenFileNameW/GetSaveFileNameW 和 shell32 的
/// SHBrowseForFolderW。调用是同步的，会阻塞 UI 线程，这是系统模态对话框的
/// 预期行为；owner 是父窗口句柄。
///
/// 多选结果使用 unmanaged buffer 解析（目录 + 单个 null 分隔的文件名），
/// 不能直接用 StringBuilder.ToString()，否则会在第一个文件名处截断。
/// </summary>
public static class Win32FileDialog
{
    private const int OFN_READONLY = 0x00000001;
    private const int OFN_OVERWRITEPROMPT = 0x00000002;
    private const int OFN_NOCHANGEDIR = 0x00000008;
    private const int OFN_ALLOWMULTISELECT = 0x00000200;
    private const int OFN_EXPLORER = 0x00080000;
    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_PATHMUSTEXIST = 0x00000800;

    public static string? OpenFile(
        IntPtr owner = default,
        string title = "Open File",
        string filter = "All files|*.*",
        string? initialDirectory = null)
    {
        var result = Show(owner, title, filter, initialDirectory, save: false, multi: false);
        return result.Count > 0 ? result[0] : null;
    }

    public static IReadOnlyList<string> OpenFiles(
        IntPtr owner = default,
        string title = "Open Files",
        string filter = "All files|*.*",
        string? initialDirectory = null) =>
        Show(owner, title, filter, initialDirectory, save: false, multi: true);

    public static string? SaveFile(
        IntPtr owner = default,
        string title = "Save File",
        string filter = "All files|*.*",
        string? initialDirectory = null,
        string? defaultExtension = null)
    {
        var result = Show(owner, title, filter, initialDirectory, save: true, multi: false, defaultExtension);
        return result.Count > 0 ? result[0] : null;
    }

    public static string? PickFolder(IntPtr owner = default, string title = "Select Folder")
    {
        var browse = new BROWSEINFOW
        {
            hwndOwner = owner,
            lpszTitle = title,
            ulFlags = 0x00000040, // BIF_NEWDIALOGSTYLE
        };
        var idList = SHBrowseForFolderW(ref browse);
        if (idList == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var buffer = new StringBuilder(32_768);
            return SHGetPathFromIDListW(idList, buffer) ? buffer.ToString() : null;
        }
        finally
        {
            Marshal.FreeCoTaskMem(idList);
        }
    }

    private static IReadOnlyList<string> Show(
        IntPtr owner,
        string title,
        string filter,
        string? initialDirectory,
        bool save,
        bool multi,
        string? defaultExtension = null)
    {
        const int bufferLength = 64 * 1024;
        var fileBuffer = Marshal.AllocHGlobal(bufferLength * sizeof(char));
        try
        {
            // Zero-initialize so the common dialog has a valid empty buffer.
            Marshal.Copy(new byte[bufferLength * sizeof(char)], 0, fileBuffer, bufferLength * sizeof(char));

            var ofn = new OPENFILENAMEW
            {
                lStructSize = Marshal.SizeOf<OPENFILENAMEW>(),
                hwndOwner = owner,
                lpstrFilter = BuildFilter(filter),
                lpstrFile = fileBuffer,
                nMaxFile = bufferLength,
                lpstrInitialDir = initialDirectory,
                lpstrTitle = title,
                lpstrDefExt = defaultExtension,
                Flags = OFN_EXPLORER | OFN_NOCHANGEDIR |
                        (save ? OFN_OVERWRITEPROMPT : OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST) |
                        (multi ? OFN_ALLOWMULTISELECT : 0),
            };

            var ok = save ? GetSaveFileNameW(ref ofn) : GetOpenFileNameW(ref ofn);
            if (!ok)
            {
                return Array.Empty<string>();
            }

            var chars = new char[bufferLength];
            Marshal.Copy(fileBuffer, chars, 0, bufferLength);
            var end = -1;
            for (var i = 0; i < chars.Length - 1; i++)
            {
                if (chars[i] == '\0' && (!multi || chars[i + 1] == '\0'))
                {
                    end = i;
                    break;
                }
            }

            if (end < 0)
            {
                end = chars.Length;
            }

            var raw = new string(chars, 0, end);
            if (!multi)
            {
                return raw.Length == 0 ? Array.Empty<string>() : new[] { raw };
            }

            // Multi-select results are a directory followed by null-separated names.
            var parts = raw.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1)
            {
                return parts.Length == 1 ? new[] { parts[0] } : Array.Empty<string>();
            }

            var directory = parts[0];
            var results = new List<string>(parts.Length - 1);
            for (var i = 1; i < parts.Length; i++)
            {
                results.Add(Path.Combine(directory, parts[i]));
            }

            return results;
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
        }
    }

    private static string BuildFilter(string filter)
    {
        // "Images|*.png;*.jpg|All files|*.*" -> double-null terminated pairs.
        var parts = filter.Split('|');
        var builder = new StringBuilder();
        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            builder.Append(parts[i]).Append('\0').Append(parts[i + 1]).Append('\0');
        }

        builder.Append('\0');
        return builder.ToString();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAMEW
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string? lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public string? lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BROWSEINFOW
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public string? pszDisplayName;
        public string? lpszTitle;
        public int ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileNameW(ref OPENFILENAMEW dialog);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileNameW(ref OPENFILENAMEW dialog);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHBrowseForFolderW(ref BROWSEINFOW browse);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1838", Justification = "The shell API writes into a caller-provided mutable buffer.")]
    private static extern bool SHGetPathFromIDListW(IntPtr pidl, StringBuilder path);
}



