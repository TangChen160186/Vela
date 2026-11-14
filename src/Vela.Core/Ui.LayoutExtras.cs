namespace Vela;

public sealed partial class Ui
{
    /// <summary>
    /// Starts a grid container. Children are auto-placed row-major unless their
    /// styles set <see cref="Style.GridColumn"/> / <see cref="Style.GridRow"/>.
    /// </summary>
    public UiScope Grid(
        string? id,
        IReadOnlyList<GridTrack> columns,
        IReadOnlyList<GridTrack>? rows = null,
        Style? style = null)
    {
        ArgumentNullException.ThrowIfNull(columns);
        var frame = RequireFrame();
        var scope = frame.BeginContainer(GridWidget.Instance, id, style ?? ColumnStyle, "grid");
        frame.Current.Payload = new GridDefinition { Columns = columns, Rows = rows };
        return scope;
    }

    /// <summary>Starts a wrapping row. Children keep their measured size.</summary>
    public UiScope Wrap(string? id = null, Style? style = null) =>
        RequireFrame().BeginContainer(WrapWidget.Instance, id, style ?? RowStyle, "wrap");

    /// <summary>
    /// Starts a dock container. Child styles use <see cref="Style.Dock"/> to
    /// claim Top / Bottom / Left / Right edges before the Fill region.
    /// </summary>
    public UiScope Dock(string? id = null, Style? style = null) =>
        RequireFrame().BeginContainer(DockWidget.Instance, id, style ?? ColumnStyle, "dock");

    /// <summary>
    /// Builds a resizable two-pane split view. The first pane uses
    /// <paramref name="firstSize"/>; the second fills the remainder.
    /// </summary>
    public void SplitPane(
        string id,
        Axis axis,
        ref float firstSize,
        Action first,
        Action second,
        float minFirst = 120,
        float maxFirst = float.PositiveInfinity,
        float minSecond = 120,
        float thickness = 7,
        Style? style = null)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        var frame = RequireFrame();
        var horizontal = axis == Axis.Horizontal;
        var available = horizontal ? ViewportSize.Width : ViewportSize.Height;
        var hardMax = MathF.Min(maxFirst, MathF.Max(minFirst, available - minSecond - thickness));
        firstSize = LayoutUtil.Clamp(firstSize, minFirst, hardMax);

        var direction = horizontal ? FlexDirection.Row : FlexDirection.Column;
        var outerStyle = (style ?? new Style { Grow = 1 }) with
        {
            Direction = direction,
            Grow = style?.Grow ?? 1,
        };

        using (frame.BeginContainer(FlexWidget.Instance, id, outerStyle, "split-pane"))
        {
            var firstStyle = new Style
            {
                Direction = FlexDirection.Column,
                Width = horizontal ? Dimension.Px(firstSize) : Dimension.Auto,
                Height = horizontal ? Dimension.Auto : Dimension.Px(firstSize),
                MinWidth = horizontal ? minFirst : null,
                MaxWidth = horizontal ? maxFirst : null,
                MinHeight = horizontal ? null : minFirst,
                MaxHeight = horizontal ? null : maxFirst,
                Shrink = 0,
            };

            using (frame.BeginContainer(FlexWidget.Instance, $"{id}:first", firstStyle, "split-first"))
            {
                first();
            }

            var splitterStyle = new Style
            {
                Width = horizontal ? Dimension.Px(thickness) : Dimension.Auto,
                Height = horizontal ? Dimension.Auto : Dimension.Px(thickness),
                Cursor = horizontal ? CursorKind.ResizeHorizontal : CursorKind.ResizeVertical,
                Shrink = 0,
            };
            var splitter = frame.AddElement(
                SplitterWidget.Instance,
                $"{id}:splitter",
                splitterStyle,
                "splitter",
                interactive: true,
                focusable: true);
            splitter.Payload = axis;
            splitter.HandlesArrowKeys = true;
            splitter.AccessibilityRole = AccessibilityRole.Slider;
            splitter.AccessibilityName = "Splitter";
            splitter.Cursor = horizontal ? CursorKind.ResizeHorizontal : CursorKind.ResizeVertical;
            frame.UpdateResponse(splitter);
            var response = splitter.Response;
            var state = State.GetOrCreate<SplitterState>(splitter.Id);

            if (response.Pressed)
            {
                state.Dragging = true;
                state.StartValue = firstSize;
                state.StartPointer = Input.PointerPosition;
            }

            if (!Input.IsButtonDown(PointerButton.Left))
            {
                state.Dragging = false;
            }

            if (response.Active && state.Dragging)
            {
                var delta = horizontal
                    ? Input.PointerPosition.X - state.StartPointer.X
                    : Input.PointerPosition.Y - state.StartPointer.Y;
                firstSize = LayoutUtil.Clamp(state.StartValue + delta, minFirst, hardMax);
            }

            if (response.Focused)
            {
                var step = Input.Shift ? 40 : 10;
                if (horizontal && (Input.WasKeyPressedOrRepeated(Key.Left) || Input.WasKeyPressedOrRepeated(Key.Right)))
                {
                    firstSize = LayoutUtil.Clamp(firstSize + (Input.WasKeyPressedOrRepeated(Key.Right) ? step : -step), minFirst, hardMax);
                }
                else if (!horizontal && (Input.WasKeyPressedOrRepeated(Key.Up) || Input.WasKeyPressedOrRepeated(Key.Down)))
                {
                    firstSize = LayoutUtil.Clamp(firstSize + (Input.WasKeyPressedOrRepeated(Key.Down) ? step : -step), minFirst, hardMax);
                }
            }

            var secondStyle = new Style
            {
                Direction = FlexDirection.Column,
                Grow = 1,
                MinWidth = horizontal ? minSecond : null,
                MinHeight = horizontal ? null : minSecond,
            };

            using (frame.BeginContainer(FlexWidget.Instance, $"{id}:second", secondStyle, "split-second"))
            {
                second();
            }
        }
    }
}


