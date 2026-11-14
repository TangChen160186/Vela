namespace Vela.Demo;

internal static class DemoCharts
{
    public static void Sparkline(DrawList draw, Rect bounds, IReadOnlyList<float> values, Color color, Theme theme)
    {
        if (values.Count < 2 || bounds.Width <= 4 || bounds.Height <= 4)
        {
            return;
        }

        var points = Project(values, bounds, 2);
        var line = SmoothPath(points);
        var area = SmoothArea(points, bounds);

        draw.FillPath(area, new LinearGradient(
            new Vec2(0, 0),
            new Vec2(0, 1),
            new GradientStop(0, color.WithAlpha(0.24f)),
            new GradientStop(1, color.WithAlpha(0f))));
        draw.StrokePath(line, color, 1.8f, LineCap.Round, LineJoin.Round);
    }

    public static void AreaChart(
        DrawList draw,
        Rect bounds,
        IReadOnlyList<float> values,
        IReadOnlyList<string> labels,
        Theme theme,
        bool hovered,
        Vec2 pointer)
    {
        if (values.Count < 2)
        {
            return;
        }

        var plot = new Rect(bounds.X + 3, bounds.Y + 8, bounds.Width - 6, MathF.Max(20, bounds.Height - 30));
        var gridColor = theme.Border.WithAlpha(0.55f);
        var labelFont = new FontSpec(theme.Body.Family, 10f, FontWeight.Medium, LineHeightMultiplier: 1.2f);
        var labelFormat = new TextFormat(labelFont, TextAlignment.Left, VerticalAlignment.Top, TextWrapMode.NoWrap);

        for (var i = 0; i <= 4; i++)
        {
            var y = plot.Top + (plot.Height * i / 4f);
            draw.Line(new Vec2(plot.Left, y), new Vec2(plot.Right, y), gridColor with { A = (byte)(i == 4 ? 150 : 70) }, 1);
        }

        var points = Project(values, plot, 14);
        var linePath = SmoothPath(points);
        var areaPath = SmoothArea(points, plot);

        draw.FillPath(areaPath, new LinearGradient(
            new Vec2(0, 0),
            new Vec2(0, 1),
            new GradientStop(0, theme.Accent.WithAlpha(0.30f)),
            new GradientStop(0.75f, theme.Accent.WithAlpha(0.06f)),
            new GradientStop(1, theme.Accent.WithAlpha(0f))));
        draw.StrokePath(linePath, theme.Accent, 2.4f, LineCap.Round, LineJoin.Round);

        var count = Math.Min(labels.Count, 7);
        for (var i = 0; i < count; i++)
        {
            var index = (int)MathF.Round(i * (values.Count - 1f) / MathF.Max(1, count - 1));
            var x = points[index].X;
            var label = labels[Math.Min(i, labels.Count - 1)];
            var width = 44f;
            draw.Text(
                new Rect(x - (width * 0.5f), plot.Bottom + 8, width, 14),
                label,
                labelFormat with { Alignment = TextAlignment.Center },
                theme.TextSubtle);
        }

        if (!hovered || !plot.Contains(pointer))
        {
            return;
        }

        var ratio = Math.Clamp((pointer.X - plot.Left) / MathF.Max(1, plot.Width), 0, 1);
        var nearest = (int)MathF.Round(ratio * (values.Count - 1));
        var point = points[nearest];
        draw.Line(new Vec2(point.X, plot.Top), new Vec2(point.X, plot.Bottom), theme.TextSubtle.WithAlpha(0.6f), 1);
        draw.FillCircle(point, 5.5f, new Brush(theme.SurfaceRaised));
        draw.FillCircle(point, 3.2f, new Brush(theme.Accent));

        var valueLabel = values[nearest].ToString("0", System.Globalization.CultureInfo.CurrentCulture);
        var tooltipWidth = 64f;
        var tooltip = new Rect(
            Math.Clamp(point.X - (tooltipWidth * 0.5f), bounds.Left, bounds.Right - tooltipWidth),
            MathF.Max(bounds.Top, point.Y - 44),
            tooltipWidth,
            30);
        draw.FillRect(tooltip, new Brush(theme.TooltipBackground), CornerRadius.All(7));
        draw.Text(
            tooltip,
            valueLabel,
            new TextFormat(new FontSpec(theme.Body.Family, 12f, FontWeight.SemiBold), TextAlignment.Center, VerticalAlignment.Center, TextWrapMode.NoWrap),
            theme.TooltipText);
    }

    public static void Donut(DrawList draw, Rect bounds, Theme theme)
    {
        var size = MathF.Min(bounds.Width, bounds.Height) * 0.84f;
        var center = bounds.Center;
        var radius = size * 0.33f;
        var width = MathF.Max(8, size * 0.075f);
        draw.StrokeCircle(center, radius, theme.Border.WithAlpha(0.55f), width);

        var segments = new (float Fraction, Color Color)[]
        {
            (0.54f, theme.Accent),
            (0.24f, theme.Info),
            (0.13f, theme.Success),
            (0.09f, theme.Warning),
        };

        var start = -MathF.PI * 0.5f;
        foreach (var (fraction, color) in segments)
        {
            var sweep = fraction * MathF.PI * 2f;
            var path = new PathBuilder()
                .AddArc(center, radius, start + 0.045f, MathF.Max(0.02f, sweep - 0.09f))
                .Build();
            draw.StrokePath(path, color, width, LineCap.Round, LineJoin.Round);
            start += sweep;
        }

        draw.Text(
            Rect.Centered(new Vec2(center.X, center.Y - 5), radius * 1.7f, 30),
            "68%",
            new TextFormat(new FontSpec(theme.Body.Family, MathF.Max(18, radius * 0.42f), FontWeight.SemiBold), TextAlignment.Center, VerticalAlignment.Center, TextWrapMode.NoWrap),
            theme.Text);
        draw.Text(
            Rect.Centered(new Vec2(center.X, center.Y + radius * 0.32f), radius * 1.7f, 18),
            "on track",
            new TextFormat(theme.Caption, TextAlignment.Center, VerticalAlignment.Center, TextWrapMode.NoWrap),
            theme.TextMuted);
    }

    public static void Bars(DrawList draw, Rect bounds, IReadOnlyList<float> values, IReadOnlyList<string> labels, Theme theme)
    {
        if (values.Count == 0)
        {
            return;
        }

        var plot = new Rect(bounds.X + 4, bounds.Y + 8, bounds.Width - 8, MathF.Max(20, bounds.Height - 28));
        for (var i = 0; i <= 3; i++)
        {
            var y = plot.Top + (plot.Height * i / 3f);
            draw.Line(new Vec2(plot.Left, y), new Vec2(plot.Right, y), theme.Border.WithAlpha(0.55f), 1);
        }

        var max = values.Max();
        var gap = 10f;
        var barWidth = MathF.Max(8, (plot.Width - (gap * (values.Count - 1))) / values.Count);
        var labelFormat = new TextFormat(new FontSpec(theme.Body.Family, 10f, FontWeight.Medium), TextAlignment.Center, VerticalAlignment.Top, TextWrapMode.NoWrap);

        for (var i = 0; i < values.Count; i++)
        {
            var height = values[i] / max * (plot.Height - 4);
            var x = plot.Left + i * (barWidth + gap);
            var bar = new Rect(x, plot.Bottom - height, barWidth, height);
            draw.FillRect(bar, new LinearGradient(
                new Vec2(0, 0),
                new Vec2(0, 1),
                new GradientStop(0, theme.Accent),
                new GradientStop(1, theme.Accent.WithAlpha(0.35f))),
                CornerRadius.All(5));
            if (i < labels.Count)
            {
                draw.Text(new Rect(x - 8, plot.Bottom + 8, barWidth + 16, 14), labels[i], labelFormat, theme.TextSubtle);
            }
        }
    }

    public static void HeroArt(DrawList draw, Rect bounds, Theme theme)
    {
        var center = bounds.Center;
        draw.FillCircle(new Vec2(center.X - 20, center.Y - 8), 66, new Brush(theme.Accent.WithAlpha(0.10f)));
        draw.FillCircle(new Vec2(center.X + 46, center.Y + 26), 40, new Brush(theme.Info.WithAlpha(0.12f)));
        draw.StrokeCircle(new Vec2(center.X - 20, center.Y - 8), 72, theme.Accent.WithAlpha(0.25f), 1);
        draw.StrokeCircle(new Vec2(center.X - 20, center.Y - 8), 94, theme.Accent.WithAlpha(0.10f), 1);

        var chart = new Rect(bounds.X + 34, bounds.Y + 22, bounds.Width - 78, bounds.Height - 44);
        var values = new float[] { 22, 34, 29, 42, 38, 55, 48, 62, 58, 74 };
        var points = Project(values, chart, 10);
        draw.StrokePath(SmoothPath(points), Color.White.WithAlpha(0.75f), 2.2f, LineCap.Round, LineJoin.Round);
        draw.FillPath(SmoothArea(points, chart), new LinearGradient(
            new Vec2(0, 0),
            new Vec2(0, 1),
            new GradientStop(0, Color.White.WithAlpha(0.16f)),
            new GradientStop(1, Color.White.WithAlpha(0))));
        draw.FillCircle(points[^1], 4, new Brush(Color.White));
        draw.StrokeCircle(points[^1], 8, Color.White.WithAlpha(0.30f), 1.5f);
    }

    private static Vec2[] Project(IReadOnlyList<float> values, Rect bounds, float padding)
    {
        var min = values.Min();
        var max = values.Max();
        var range = MathF.Max(1, max - min);
        var points = new Vec2[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var x = bounds.Left + (i / MathF.Max(1, values.Count - 1) * bounds.Width);
            var normalized = (values[i] - min) / range;
            var y = bounds.Bottom - normalized * (bounds.Height - padding * 2) - padding;
            points[i] = new Vec2(x, y);
        }

        return points;
    }

    private static PathData SmoothPath(Vec2[] points)
    {
        var builder = new PathBuilder();
        builder.MoveTo(points[0]);
        for (var i = 0; i < points.Length - 1; i++)
        {
            var p0 = points[Math.Max(0, i - 1)];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = points[Math.Min(points.Length - 1, i + 2)];
            var c1 = p1 + (p2 - p0) / 6f;
            var c2 = p2 - (p3 - p1) / 6f;
            builder.CubicTo(c1, c2, p2);
        }

        return builder.Build();
    }

    private static PathData SmoothArea(Vec2[] points, Rect bounds)
    {
        var builder = new PathBuilder();
        builder.MoveTo(points[0]);
        for (var i = 0; i < points.Length - 1; i++)
        {
            var p0 = points[Math.Max(0, i - 1)];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = points[Math.Min(points.Length - 1, i + 2)];
            var c1 = p1 + (p2 - p0) / 6f;
            var c2 = p2 - (p3 - p1) / 6f;
            builder.CubicTo(c1, c2, p2);
        }

        builder.LineTo(points[^1].X, bounds.Bottom);
        builder.LineTo(points[0].X, bounds.Bottom);
        builder.Close();
        return builder.Build();
    }
}

