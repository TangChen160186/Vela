namespace Vela.Demo;

public sealed partial class DemoApp
{
    private static void BuildLogo(Ui ui, Theme theme)
    {
        ui.Canvas("brand-logo", view =>
        {
            var draw = view.DrawList;
            var bounds = view.Bounds;
            draw.FillRect(bounds, new LinearGradient(
                new Vec2(0, 0),
                new Vec2(1, 1),
                new GradientStop(0, theme.AccentHover),
                new GradientStop(1, theme.Info)), CornerRadius.All(10));
            draw.Shadow(bounds, new BoxShadow(new Vec2(0, 3), 10, 0, theme.Accent.WithAlpha(0.28f)), CornerRadius.All(10));
            draw.DrawIcon(Icons.Sparkles, bounds.Deflate(Thickness.All(7)), Color.White);
        }, new Style { Width = 34, Height = 34 });
    }

    private static void PaintWorkspaceSwitcher(CanvasView view, Theme theme)
    {
        var draw = view.DrawList;
        var bounds = view.Bounds;
        var hovered = view.Response.Hovered;
        draw.FillRect(bounds, new Brush(hovered ? theme.SurfaceHover : theme.Surface), CornerRadius.All(10));
        draw.StrokeRect(bounds.Deflate(Thickness.All(0.5f)), hovered ? theme.BorderStrong : theme.Border, 1, CornerRadius.All(10));

        var avatar = new Rect(bounds.Left + 10, bounds.Center.Y - 15, 30, 30);
        draw.FillRect(avatar, new LinearGradient(
            new Vec2(0, 0),
            new Vec2(1, 1),
            new GradientStop(0, theme.Accent),
            new GradientStop(1, theme.Info)), CornerRadius.All(8));
        DrawCenteredText(draw, avatar, "A", new FontSpec(theme.Body.Family, 13f, FontWeight.SemiBold), Color.White);

        var nameFormat = new TextFormat(new FontSpec(theme.Body.Family, 12.8f, FontWeight.Medium), TextAlignment.Left, VerticalAlignment.Top, TextWrapMode.NoWrap, TextOverflowMode.Ellipsis);
        var captionFormat = new TextFormat(theme.Caption, TextAlignment.Left, VerticalAlignment.Top, TextWrapMode.NoWrap);
        var textX = avatar.Right + 10;
        draw.Text(new Rect(textX, bounds.Center.Y - 17, bounds.Right - textX - 28, 18), "Acme Design", nameFormat, theme.Text);
        draw.Text(new Rect(textX, bounds.Center.Y + 2, bounds.Right - textX - 28, 16), "Pro workspace", captionFormat, theme.TextSubtle);
        draw.DrawIcon(Icons.ChevronDown, new Rect(bounds.Right - 25, bounds.Center.Y - 8, 16, 16), theme.TextSubtle);
    }

    private static void PaintProfileCard(CanvasView view, Theme theme)
    {
        var draw = view.DrawList;
        var bounds = view.Bounds;
        var hovered = view.Response.Hovered;
        if (hovered)
        {
            draw.FillRect(bounds, new Brush(theme.SurfaceHover), CornerRadius.All(10));
        }

        var avatar = new Rect(bounds.Left + 8, bounds.Center.Y - 17, 34, 34);
        DrawAvatar(draw, avatar, "AL", theme, 34);
        var textX = avatar.Right + 10;
        var nameFormat = new TextFormat(new FontSpec(theme.Body.Family, 12.8f, FontWeight.Medium), TextAlignment.Left, VerticalAlignment.Top, TextWrapMode.NoWrap, TextOverflowMode.Ellipsis);
        draw.Text(new Rect(textX, bounds.Center.Y - 16, bounds.Right - textX - 20, 18), "Alex Morgan", nameFormat, theme.Text);
        draw.Text(new Rect(textX, bounds.Center.Y + 2, bounds.Right - textX - 20, 16), "Product designer", new TextFormat(theme.Caption, TextAlignment.Left, VerticalAlignment.Top, TextWrapMode.NoWrap), theme.TextSubtle);
        draw.DrawIcon(Icons.ChevronRight, new Rect(bounds.Right - 20, bounds.Center.Y - 8, 14, 14), theme.TextSubtle);
    }

    private static void PaintAvatarButton(CanvasView view, Theme theme, float size)
    {
        var draw = view.DrawList;
        var bounds = view.Bounds;
        if (view.Response.Hovered)
        {
            draw.StrokeRect(bounds.Deflate(Thickness.All(1)), theme.Accent.WithAlpha(0.55f), 1.5f, CornerRadius.All(size * 0.5f));
        }

        DrawAvatar(draw, bounds.Deflate(Thickness.All(3)), "AL", theme, size - 6);
        draw.FillCircle(new Vec2(bounds.Right - 4, bounds.Bottom - 4), 4.5f, new Brush(theme.Background));
        draw.FillCircle(new Vec2(bounds.Right - 4, bounds.Bottom - 4), 3f, new Brush(theme.Success));
    }

    private static void DrawAvatar(DrawList draw, Rect bounds, string initials, Theme theme, float size)
    {
        var circle = Rect.Centered(bounds.Center, size, size);
        draw.FillRect(circle, new LinearGradient(
            new Vec2(0, 0),
            new Vec2(1, 1),
            new GradientStop(0, theme.Accent),
            new GradientStop(1, theme.Info)), CornerRadius.All(size * 0.5f));
        DrawCenteredText(draw, circle, initials, new FontSpec(theme.Body.Family, MathF.Max(9, size * 0.34f), FontWeight.SemiBold), Color.White);
    }

    private static void DrawCenteredText(DrawList draw, Rect rect, string text, FontSpec font, Color color)
    {
        draw.Text(rect, text, new TextFormat(font, TextAlignment.Center, VerticalAlignment.Center, TextWrapMode.NoWrap), color);
    }
}
