namespace Vela.Platform.Win32;

/// <summary>
/// 自顶向下的 32 位 GDI DIB + 内存 DC。
///
/// Skia 直接渲染到 <see cref="Bits"/>（BGRA），然后 <see cref="Present"/> 用
/// BitBlt 到窗口 DC。尺寸变化时重建 DIB；像素尺寸由窗口 DPI 决定。
/// 这是一个 CPU surface，不持有窗口句柄。
/// </summary>
public sealed class Win32Surface : IDisposable
{
    private IntPtr _memoryDc;
    private IntPtr _bitmap;
    private IntPtr _oldBitmap;
    private IntPtr _bits;
    private bool _disposed;

    public int WidthPixels { get; private set; }
    public int HeightPixels { get; private set; }
    public int RowBytes => WidthPixels * 4;
    public IntPtr Bits => _bits;
    public bool IsEmpty => _bits == IntPtr.Zero || WidthPixels <= 0 || HeightPixels <= 0;

    public void Resize(int widthPixels, int heightPixels)
    {
        ThrowIfDisposed();
        widthPixels = Math.Max(1, widthPixels);
        heightPixels = Math.Max(1, heightPixels);
        if (widthPixels == WidthPixels && heightPixels == HeightPixels && _bits != IntPtr.Zero)
        {
            return;
        }

        ReleaseResources();

        _memoryDc = NativeMethods.CreateCompatibleDC(IntPtr.Zero);
        var info = new NativeMethods.BITMAPINFO
        {
            bmiHeader = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                biWidth = widthPixels,
                biHeight = -heightPixels,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeMethods.BI_RGB,
                biSizeImage = (uint)(widthPixels * heightPixels * 4),
            },
        };

        _bitmap = NativeMethods.CreateDIBSection(_memoryDc, ref info, NativeMethods.DIB_RGB_COLORS, out _bits, IntPtr.Zero, 0);
        if (_bitmap == IntPtr.Zero)
        {
            var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"CreateDIBSection failed ({error}).");
        }

        _oldBitmap = NativeMethods.SelectObject(_memoryDc, _bitmap);
        WidthPixels = widthPixels;
        HeightPixels = heightPixels;
    }

    public void Present(IntPtr window)
    {
        ThrowIfDisposed();
        if (IsEmpty)
        {
            return;
        }

        var dc = NativeMethods.GetDC(window);
        if (dc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            NativeMethods.BitBlt(dc, 0, 0, WidthPixels, HeightPixels, _memoryDc, 0, 0, NativeMethods.SRCCOPY);
        }
        finally
        {
            _ = NativeMethods.ReleaseDC(window, dc);
        }
    }

    private void ReleaseResources()
    {
        if (_memoryDc != IntPtr.Zero && _oldBitmap != IntPtr.Zero)
        {
            NativeMethods.SelectObject(_memoryDc, _oldBitmap);
            _oldBitmap = IntPtr.Zero;
        }

        if (_bitmap != IntPtr.Zero)
        {
            NativeMethods.DeleteObject(_bitmap);
            _bitmap = IntPtr.Zero;
        }

        if (_memoryDc != IntPtr.Zero)
        {
            NativeMethods.DeleteDC(_memoryDc);
            _memoryDc = IntPtr.Zero;
        }

        _bits = IntPtr.Zero;
        WidthPixels = 0;
        HeightPixels = 0;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleaseResources();
    }
}
