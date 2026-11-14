namespace Vela;

/// <summary>
/// Layout and paint properties for one element. Unspecified visual properties inherit
/// from the parent where the property is inheritable (foreground and font), and from
/// the active <see cref="Theme"/> elsewhere.
/// </summary>
public sealed record Style
{
    public FlexDirection Direction { get; init; } = FlexDirection.Column;

    public Dimension Width { get; init; }
    public Dimension Height { get; init; }

    /// <summary>Weight for distributing free space along the parent's main axis.</summary>
    public float Grow { get; init; }

    /// <summary>Shrink weight when content overflows the parent's main axis.</summary>
    public float Shrink { get; init; } = 1;

    public float? MinWidth { get; init; }
    public float? MaxWidth { get; init; }
    public float? MinHeight { get; init; }
    public float? MaxHeight { get; init; }

    public Thickness Padding { get; init; }
    public Thickness Margin { get; init; }
    public float Gap { get; init; }

    public AlignItems AlignItems { get; init; } = AlignItems.Stretch;
    public JustifyContent JustifyContent { get; init; } = JustifyContent.Start;

    public PositionKind Position { get; init; }
    public float? Left { get; init; }
    public float? Top { get; init; }
    public float? Right { get; init; }
    public float? Bottom { get; init; }

    public bool ClipContent { get; init; }
    public bool Visible { get; init; } = true;
    public float Opacity { get; init; } = 1;

    public Brush? Background { get; init; }
    public Color? BorderColor { get; init; }
    public float BorderWidth { get; init; }
    public CornerRadius CornerRadius { get; init; }
    public BoxShadow? Shadow { get; init; }

    public Color? Foreground { get; init; }
    public FontSpec? Font { get; init; }
    public TextAlignment TextAlignment { get; init; } = TextAlignment.Left;
    public VerticalAlignment VerticalTextAlignment { get; init; } = VerticalAlignment.Top;
    public TextWrapMode TextWrap { get; init; } = TextWrapMode.Wrap;
    public TextOverflowMode TextOverflow { get; init; } = TextOverflowMode.Ellipsis;
    public int MaxLines { get; init; }

    public float? AspectRatio { get; init; }
    public CursorKind? Cursor { get; init; }

    /// <summary>Docking side for a child of <see cref="Ui.Dock"/>.</summary>
    public DockSide? Dock { get; init; }

    /// <summary>Zero-based grid column; -1 means automatic placement.</summary>
    public int GridColumn { get; init; } = -1;

    /// <summary>Zero-based grid row; -1 means automatic placement.</summary>
    public int GridRow { get; init; } = -1;

    public int GridColumnSpan { get; init; } = 1;
    public int GridRowSpan { get; init; } = 1;

    public static Style Row(float gap = 0) => new() { Direction = FlexDirection.Row, Gap = gap };
    public static Style Column(float gap = 0) => new() { Direction = FlexDirection.Column, Gap = gap };
    public static Style Fill => new() { Grow = 1 };
    public static Style FillRow(float gap = 0) => new() { Direction = FlexDirection.Row, Gap = gap, Grow = 1 };
    public static Style FillColumn(float gap = 0) => new() { Direction = FlexDirection.Column, Gap = gap, Grow = 1 };

    /// <summary>Returns a copy with the specified padding.</summary>
    public Style WithPadding(Thickness padding) => this with { Padding = padding };

    /// <summary>Returns a copy with the specified fixed width.</summary>
    public Style WithWidth(float width) => this with { Width = Dimension.Px(width) };

    /// <summary>Returns a copy with the specified fixed height.</summary>
    public Style WithHeight(float height) => this with { Height = Dimension.Px(height) };

    public Style WithBackground(Color color) => this with { Background = new Brush(color) };
    public Style WithBackground(Brush brush) => this with { Background = brush };
    public Style WithRadius(float radius) => this with { CornerRadius = CornerRadius.All(radius) };
}

