using System.Runtime.InteropServices;

namespace Vela.Platform.Win32;

/// <summary>The Windows Unicode text clipboard.</summary>
public sealed class Win32Clipboard : IClipboard
{
    public string? GetText()
    {
        if (!NativeMethods.OpenClipboard(IntPtr.Zero))
        {
            return null;
        }

        try
        {
            var handle = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var pointer = NativeMethods.GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    public void SetText(string text)
    {
        text ??= string.Empty;
        if (!NativeMethods.OpenClipboard(IntPtr.Zero))
        {
            return;
        }

        try
        {
            NativeMethods.EmptyClipboard();
            var bytes = (nuint)((text.Length + 1) * sizeof(char));
            var handle = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, bytes);
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var pointer = NativeMethods.GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                NativeMethods.GlobalFree(handle);
                return;
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, pointer, text.Length);
                Marshal.WriteInt16(pointer, text.Length * sizeof(char), 0);
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }

            if (NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, handle) == IntPtr.Zero)
            {
                NativeMethods.GlobalFree(handle);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }
}
