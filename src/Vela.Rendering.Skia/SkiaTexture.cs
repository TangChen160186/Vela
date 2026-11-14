using SkiaSharp;

namespace Vela.Rendering.Skia;

/// <summary>An image resource owned by the Skia renderer.</summary>
public sealed class SkiaTexture : ITexture, IDisposable
{
    private SkiaTexture(SKImage image, string? name)
    {
        Image = image;
        Name = name;
    }

    public SKImage Image { get; }
    public int PixelWidth => Image.Width;
    public int PixelHeight => Image.Height;
    public string? Name { get; }

    public static SkiaTexture FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var image = SKImage.FromEncodedData(path)
            ?? throw new InvalidOperationException($"Could not decode image '{path}'.");
        return new SkiaTexture(image, Path.GetFileName(path));
    }

    public static SkiaTexture FromEncoded(ReadOnlySpan<byte> data, string? name = null)
    {
        var image = SKImage.FromEncodedData(data.ToArray())
            ?? throw new InvalidOperationException("Could not decode the supplied image data.");
        return new SkiaTexture(image, name);
    }

    public static SkiaTexture FromBitmap(SKBitmap bitmap, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var image = SKImage.FromBitmap(bitmap);
        return new SkiaTexture(image, name);
    }

    public static SkiaTexture Create(int width, int height, Color color, string? name = null)
    {
        var info = new SKImageInfo(Math.Max(1, width), Math.Max(1, height), SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(new SKColor(color.R, color.G, color.B, color.A));
        var image = surface.Snapshot();
        return new SkiaTexture(image, name);
    }

    public void Dispose() => Image.Dispose();
}
