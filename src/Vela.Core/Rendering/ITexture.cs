namespace Vela;

/// <summary>An opaque image resource owned by a renderer.</summary>
public interface ITexture
{
    int PixelWidth { get; }
    int PixelHeight { get; }
    string? Name { get; }
}
