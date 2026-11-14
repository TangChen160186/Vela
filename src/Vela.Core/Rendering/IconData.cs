namespace Vela;

/// <summary>A scalable icon on a 24x24 canvas. Icons may be stroked or filled.</summary>
public sealed class IconData
{
    public IconData(string name, PathData path, bool filled = false, float strokeWidth = 2f)
    {
        Name = name;
        Path = path;
        Filled = filled;
        StrokeWidth = strokeWidth;
    }

    public string Name { get; }
    public PathData Path { get; }
    public bool Filled { get; }
    public float StrokeWidth { get; }

    public static IconData FromSvg(string name, string pathData, bool filled = false, float strokeWidth = 2f) =>
        new(name, SvgPathParser.Parse(pathData), filled, strokeWidth);

    public override string ToString() => Name;
}
