namespace Vela;

/// <summary>
/// Row/Column 的核心布局实现。
///
/// 两遍测量：
/// <list type="number">
/// <item>BuildItems 用父约束求每个子项的 base main / cross；</item>
/// <item>AllocateMain 按 grow/shrink/gap 分配主轴；</item>
/// <item>在最终主轴尺寸下再次 MeasureChild，让文本和 nested container 正确换行；</item>
/// <item>Arrange 重新做同样的分配，并按 JustifyContent/AlignItems 放到最终坐标。</item>
/// </list>
///
/// 为什么需要第二遍？子项的交叉轴尺寸（尤其文本高度）取决于最终主轴宽度；
/// 只测一遍会在换行时得到错误高度。
/// </summary>
internal sealed class FlexWidget : Widget
{
    public static FlexWidget Instance { get; } = new();

    private struct Item
    {
        public Element Element;
        public float BaseMain;
        public float FinalMain;
        public float Cross;
        public float? ExplicitCross;
        public float MarginMainStart;
        public float MarginMainEnd;
        public float MarginCrossStart;
        public float MarginCrossEnd;
        public float Grow;
        public float Shrink;
        public bool CrossAuto;
        public bool Stretch;
    }

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        if (constraints.IsTightWidth && constraints.IsTightHeight && element.Style.AspectRatio is null)
        {
            return new Size(constraints.MinWidth, constraints.MinHeight);
        }

        // 坐标约定：row 时主轴=X、交叉轴=Y；column 时相反。
        // margin 在 BuildItems 中拆成 main/cross 两段，之后只用这两段。
        var row = LayoutUtil.IsRow(element.Style.Direction);
        var insets = LayoutUtil.Insets(element.Style);
        var innerMaxW = MathF.Max(0, constraints.MaxWidth - insets.HorizontalSum);
        var innerMaxH = MathF.Max(0, constraints.MaxHeight - insets.VerticalSum);

        var width = ResolveOwnWidth(element, constraints, innerMaxW);
        var height = ResolveOwnHeight(element, constraints, innerMaxH);

        var innerW = MathF.Max(0, (width ?? constraints.MaxWidth) - insets.HorizontalSum);
        var innerH = MathF.Max(0, (height ?? constraints.MaxHeight) - insets.VerticalSum);

        var mainLimit = row ? innerW : innerH;
        var crossLimit = row ? innerH : innerW;
        var crossKnown = row ? height : width;
        var mainKnown = row ? width : height;

        var items = BuildItems(element, context, row, mainLimit, crossLimit, crossKnown is not null);
        var gaps = items.Count > 1 ? element.Style.Gap * (items.Count - 1) : 0;
        var naturalMain = 0f;
        foreach (var item in items)
        {
            naturalMain += item.BaseMain + item.MarginMainStart + item.MarginMainEnd;
        }

        var innerMain = mainKnown is { } knownMain
            ? MathF.Max(0, knownMain - MainInsets(insets, row))
            : MathF.Min(naturalMain + gaps, mainLimit);

        // 主轴分配：先尊重 base size，再按 Grow 分配剩余、按 Shrink 压缩。
        // gap 固定占用，不参与 grow/shrink。
        AllocateMain(items, innerMain, gaps, row);

        var innerCross = crossKnown is { } knownCross
            ? MathF.Max(0, knownCross - CrossInsets(insets, row))
            : 0f;

        if (crossKnown is null)
        {
            foreach (var item in items)
            {
                innerCross = MathF.Max(innerCross, item.Cross + item.MarginCrossStart + item.MarginCrossEnd);
            }
        }

        // 第二遍测量：用最终主轴尺寸重新测量子项。
        // 文本在这里重新换行，嵌套容器也在这里解析自己的子级。
        // 这是 Flex 尺寸正确性的关键，不能省略。
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var crossAvailable = MathF.Max(0, innerCross - item.MarginCrossStart - item.MarginCrossEnd);
            var stretch = item.Stretch && item.CrossAuto && crossKnown is not null;
            var tightCross = stretch ? crossAvailable : item.ExplicitCross;
            var size = MeasureChild(item.Element, context, row, item.FinalMain, tightCross, crossAvailable,
                item.MarginMainStart + item.MarginMainEnd, item.MarginCrossStart + item.MarginCrossEnd, item.FinalMain);
            item.Cross = row ? size.Height : size.Width;
            items[i] = item;
        }

        if (crossKnown is null)
        {
            innerCross = 0;
            foreach (var item in items)
            {
                innerCross = MathF.Max(innerCross, item.Cross + item.MarginCrossStart + item.MarginCrossEnd);
            }
        }

        var resolvedWidth = width ?? ((row ? innerMain : innerCross) + insets.HorizontalSum);
        var resolvedHeight = height ?? ((row ? innerCross : innerMain) + insets.VerticalSum);

        if (element.Style.AspectRatio is { } ratio && ratio > 0)
        {
            if (element.Style.Width.IsAuto)
            {
                resolvedWidth = resolvedHeight * ratio;
            }
            else if (element.Style.Height.IsAuto)
            {
                resolvedHeight = resolvedWidth / ratio;
            }
        }

        return FinalizeSize(element, constraints, resolvedWidth, resolvedHeight);
    }

    /// <summary>
    /// 按最终 bounds 重新分配并放置子项，不信任 Measure 阶段的临时坐标。
    /// 这样父级缩放/滚动/窗口 resize 时，子级几何仍然按同一套规则重算。
    /// </summary>
    public override void Arrange(Element element, LayoutContext context, Rect bounds)
    {
        var row = LayoutUtil.IsRow(element.Style.Direction);
        var reversed = LayoutUtil.IsReversed(element.Style.Direction);
        var insets = LayoutUtil.Insets(element.Style);
        var innerW = MathF.Max(0, bounds.Width - insets.HorizontalSum);
        var innerH = MathF.Max(0, bounds.Height - insets.VerticalSum);
        var innerMain = row ? innerW : innerH;
        var innerCross = row ? innerH : innerW;

        var items = BuildItems(element, context, row, innerMain, innerCross, crossKnown: true);
        var gaps = items.Count > 1 ? element.Style.Gap * (items.Count - 1) : 0;
        AllocateMain(items, innerMain, gaps, row);

        var usedMain = gaps;
        foreach (var item in items)
        {
            usedMain += item.FinalMain + item.MarginMainStart + item.MarginMainEnd;
        }

        var free = MathF.Max(0, innerMain - usedMain);
        var (justifyOffset, extraGap) = Justify(element.Style.JustifyContent, free, items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var crossAvailable = MathF.Max(0, innerCross - item.MarginCrossStart - item.MarginCrossEnd);
            var stretch = item.Stretch && item.CrossAuto;
            var tightCross = stretch ? crossAvailable : item.ExplicitCross;
            var size = MeasureChild(item.Element, context, row, item.FinalMain, tightCross, crossAvailable,
                item.MarginMainStart + item.MarginMainEnd, item.MarginCrossStart + item.MarginCrossEnd, item.FinalMain);
            item.Cross = stretch ? crossAvailable : (row ? size.Height : size.Width);
            items[i] = item;
        }

        var cursor = justifyOffset;
        for (var i = 0; i < items.Count; i++)
        {
            // cursor 包含 margin 和 gap；reversed 时从右/下反向放置。
            var item = items[i];
            var outerSize = item.FinalMain + item.MarginMainStart + item.MarginMainEnd;
            var outerStart = cursor;
            var mainStart = outerStart + item.MarginMainStart;
            cursor += outerSize + element.Style.Gap + extraGap;

            if (reversed)
            {
                mainStart = innerMain - outerStart - item.MarginMainStart - item.FinalMain;
            }

            var crossStart = item.MarginCrossStart;
            if (!(item.Stretch && item.CrossAuto))
            {
                crossStart = element.Style.AlignItems switch
                {
                    AlignItems.Center => item.MarginCrossStart + ((innerCross - item.MarginCrossStart - item.MarginCrossEnd - item.Cross) * 0.5f),
                    AlignItems.End => innerCross - item.MarginCrossEnd - item.Cross,
                    _ => item.MarginCrossStart,
                };
            }

            var x = bounds.X + insets.Left + (row ? mainStart : crossStart);
            var y = bounds.Y + insets.Top + (row ? crossStart : mainStart);
            var width = row ? item.FinalMain : item.Cross;
            var height = row ? item.Cross : item.FinalMain;
            context.Arrange(item.Element, new Rect(x, y, MathF.Max(0, width), MathF.Max(0, height)));
        }

        ArrangeAbsoluteChildren(element, context, bounds);
    }

    private static List<Item> BuildItems(
        Element element,
        LayoutContext context,
        bool row,
        float mainLimit,
        float crossLimit,
        bool crossKnown)
    {
        var items = new List<Item>(element.Children.Count);
        foreach (var child in element.Children)
        {
            if (!child.IsVisible || child.Style.Position == PositionKind.Absolute)
            {
                continue;
            }

            var margin = child.Style.Margin;
            var item = new Item
            {
                Element = child,
                Grow = child.Style.Grow,
                Shrink = child.Style.Shrink,
                MarginMainStart = row ? margin.Left : margin.Top,
                MarginMainEnd = row ? margin.Right : margin.Bottom,
                MarginCrossStart = row ? margin.Top : margin.Left,
                MarginCrossEnd = row ? margin.Bottom : margin.Right,
                CrossAuto = row ? child.Style.Height.IsAuto : child.Style.Width.IsAuto,
                Stretch = element.Style.AlignItems == AlignItems.Stretch &&
                          (row ? child.Style.MaxHeight is null : child.Style.MaxWidth is null),
            };

            var mainSpec = row ? child.Style.Width : child.Style.Height;
            var crossSpec = row ? child.Style.Height : child.Style.Width;
            var mainAvailable = MathF.Max(0, mainLimit - item.MarginMainStart - item.MarginMainEnd);
            var crossAvailable = MathF.Max(0, crossLimit - item.MarginCrossStart - item.MarginCrossEnd);

            if (!crossSpec.IsAuto)
            {
                item.ExplicitCross = crossSpec.Kind == DimensionKind.Percent
                    ? (float.IsFinite(crossLimit) ? crossLimit * crossSpec.Value : 0)
                    : crossSpec.Value;
            }

            var stretch = crossKnown && item.Stretch && item.CrossAuto;
            var tightCross = item.ExplicitCross ?? (stretch ? crossAvailable : null);

            if (!mainSpec.IsAuto)
            {
                var baseMain = mainSpec.Kind == DimensionKind.Percent
                    ? (float.IsFinite(mainLimit) ? mainLimit * mainSpec.Value : 0)
                    : mainSpec.Value;
                item.BaseMain = MathF.Max(0, baseMain);

                var size = MeasureChild(child, context, row, item.BaseMain, tightCross, crossAvailable,
                    item.MarginMainStart + item.MarginMainEnd, item.MarginCrossStart + item.MarginCrossEnd, item.BaseMain);
                item.Cross = row ? size.Height : size.Width;
            }
            else
            {
                var size = MeasureChild(child, context, row, null, tightCross, crossAvailable,
                    item.MarginMainStart + item.MarginMainEnd, item.MarginCrossStart + item.MarginCrossEnd, mainAvailable);
                item.BaseMain = MathF.Max(0, row ? size.Width : size.Height);
                item.Cross = MathF.Max(0, row ? size.Height : size.Width);
            }

            item.BaseMain = LayoutUtil.Clamp(
                item.BaseMain,
                row ? child.Style.MinWidth ?? 0 : child.Style.MinHeight ?? 0,
                row ? child.Style.MaxWidth ?? float.PositiveInfinity : child.Style.MaxHeight ?? float.PositiveInfinity);
            items.Add(item);
        }

        return items;
    }

    private static Size MeasureChild(
        Element child,
        LayoutContext context,
        bool row,
        float? tightMain,
        float? tightCross,
        float crossMax,
        float mainMargins,
        float crossMargins,
        float mainMax)
    {
        var constraints = row
            ? new Constraints(
                tightMain ?? 0,
                tightMain ?? MathF.Max(0, mainMax),
                tightCross ?? 0,
                tightCross ?? MathF.Max(0, crossMax))
            : new Constraints(
                tightCross ?? 0,
                tightCross ?? MathF.Max(0, crossMax),
                tightMain ?? 0,
                tightMain ?? MathF.Max(0, mainMax));

        return context.Measure(child, constraints);
    }

    private static void AllocateMain(List<Item> items, float innerMain, float gaps, bool row)
    {
        var used = gaps;
        foreach (var item in items)
        {
            used += item.BaseMain + item.MarginMainStart + item.MarginMainEnd;
        }

        var free = innerMain - used;
        if (free > 0.01f)
        {
            var totalGrow = 0f;
            foreach (var item in items)
            {
                totalGrow += MathF.Max(0, item.Grow);
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var add = totalGrow > 0 ? free * (MathF.Max(0, item.Grow) / totalGrow) : 0;
                item.FinalMain = ClampMain(item, item.BaseMain + add, row);
                items[i] = item;
            }
        }
        else if (free < -0.01f)
        {
            var deficit = -free;
            var totalShrink = 0f;
            foreach (var item in items)
            {
                totalShrink += MathF.Max(0, item.Shrink) * item.BaseMain;
            }

            if (totalShrink > 0)
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var weight = MathF.Max(0, item.Shrink) * item.BaseMain;
                    var reduce = deficit * (weight / totalShrink);
                    item.FinalMain = ClampMain(item, MathF.Max(0, item.BaseMain - reduce), row);
                    items[i] = item;
                }
            }
            else
            {
                SetBaseSizes(items);
            }
        }
        else
        {
            SetBaseSizes(items);
        }
    }

    private static void SetBaseSizes(List<Item> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            item.FinalMain = item.BaseMain;
            items[i] = item;
        }
    }

    private static float ClampMain(in Item item, float value, bool row)
    {
        var min = row ? item.Element.Style.MinWidth ?? 0 : item.Element.Style.MinHeight ?? 0;
        var max = row ? item.Element.Style.MaxWidth ?? float.PositiveInfinity : item.Element.Style.MaxHeight ?? float.PositiveInfinity;
        return LayoutUtil.Clamp(value, MathF.Max(0, min), max);
    }

    private static (float Offset, float ExtraGap) Justify(JustifyContent justify, float free, int count)
    {
        if (count == 0 || free <= 0)
        {
            return (0, 0);
        }

        return justify switch
        {
            JustifyContent.Center => (free * 0.5f, 0),
            JustifyContent.End => (free, 0),
            JustifyContent.SpaceBetween when count > 1 => (0, free / (count - 1)),
            JustifyContent.SpaceAround => (free / count * 0.5f, free / count),
            JustifyContent.SpaceEvenly => (free / (count + 1), free / (count + 1)),
            _ => (0, 0),
        };
    }

    private static float MainInsets(Thickness insets, bool row) => row ? insets.HorizontalSum : insets.VerticalSum;
    private static float CrossInsets(Thickness insets, bool row) => row ? insets.VerticalSum : insets.HorizontalSum;

    private static float? ResolveOwnWidth(Element element, Constraints constraints, float innerMax)
    {
        if (constraints.IsTightWidth)
        {
            return constraints.MinWidth;
        }

        if (element.Style.Width.IsAuto)
        {
            return null;
        }

        var value = element.Style.Width.Kind == DimensionKind.Percent
            ? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth * element.Style.Width.Value : innerMax)
            : element.Style.Width.Value;

        return LayoutUtil.Clamp(
            value,
            MathF.Max(0, element.Style.MinWidth ?? 0),
            element.Style.MaxWidth ?? float.PositiveInfinity);
    }

    private static float? ResolveOwnHeight(Element element, Constraints constraints, float innerMax)
    {
        if (constraints.IsTightHeight)
        {
            return constraints.MinHeight;
        }

        if (element.Style.Height.IsAuto)
        {
            return null;
        }

        var value = element.Style.Height.Kind == DimensionKind.Percent
            ? (float.IsFinite(constraints.MaxHeight) ? constraints.MaxHeight * element.Style.Height.Value : innerMax)
            : element.Style.Height.Value;

        return LayoutUtil.Clamp(
            value,
            MathF.Max(0, element.Style.MinHeight ?? 0),
            element.Style.MaxHeight ?? float.PositiveInfinity);
    }

    private static Size FinalizeSize(Element element, Constraints constraints, float width, float height)
    {
        var minW = MathF.Max(constraints.MinWidth, element.Style.MinWidth ?? 0);
        var maxW = MathF.Min(constraints.MaxWidth, element.Style.MaxWidth ?? float.PositiveInfinity);
        var minH = MathF.Max(constraints.MinHeight, element.Style.MinHeight ?? 0);
        var maxH = MathF.Min(constraints.MaxHeight, element.Style.MaxHeight ?? float.PositiveInfinity);

        width = LayoutUtil.Clamp(width, minW, maxW);
        height = LayoutUtil.Clamp(height, minH, maxH);
        return new Size(width, height);
    }

    private static void ArrangeAbsoluteChildren(Element element, LayoutContext context, Rect bounds)
    {
        var insets = LayoutUtil.Insets(element.Style);
        var inner = new Rect(
            bounds.X + insets.Left,
            bounds.Y + insets.Top,
            MathF.Max(0, bounds.Width - insets.HorizontalSum),
            MathF.Max(0, bounds.Height - insets.VerticalSum));

        foreach (var child in element.Children)
        {
            if (!child.IsVisible || child.Style.Position != PositionKind.Absolute)
            {
                continue;
            }

            var constraints = Constraints.Loose(inner.Width, inner.Height);
            var size = context.Measure(child, constraints);
            var left = child.Style.Left;
            var top = child.Style.Top;
            var right = child.Style.Right;
            var bottom = child.Style.Bottom;

            var width = left is not null && right is not null
                ? MathF.Max(0, inner.Width - left.Value - right.Value)
                : child.Style.Width.IsAuto ? size.Width : LayoutUtil.Clamp(size.Width, 0, inner.Width);
            var height = top is not null && bottom is not null
                ? MathF.Max(0, inner.Height - top.Value - bottom.Value)
                : child.Style.Height.IsAuto ? size.Height : LayoutUtil.Clamp(size.Height, 0, inner.Height);

            var x = inner.X + (left ?? (right is not null ? inner.Width - right.Value - width : 0));
            var y = inner.Y + (top ?? (bottom is not null ? inner.Height - bottom.Value - height : 0));
            context.Arrange(child, new Rect(x, y, width, height));
        }
    }
}

