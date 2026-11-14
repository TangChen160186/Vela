using SkiaSharp;
using Vela.Rendering.Skia;
using Xunit;

namespace Vela.Rendering.Tests;

public sealed class TextEngineTests
{
    [Fact]
    public void MeasuresAndWrapsUnicodeText()
    {
        using var engine = new SkiaTextEngine();
        var theme = Theme.Dark();
        var latin = engine.Measure("Vela renders text", theme.Body, new TextMeasureOptions(120));
        var cjk = engine.Measure("即时模式用户界面框架", theme.Body, new TextMeasureOptions(120));
        Assert.True(latin.Width > 0);
        Assert.True(latin.Height > 0);
        Assert.True(cjk.Width > 0);
        Assert.True(cjk.LineCount >= 1);
        Assert.True(engine.Measure("One line", theme.Body, TextMeasureOptions.SingleLine).LineCount == 1);
    }

    [Fact]
    public void HitTestingSnapsToTextPositions()
    {
        using var engine = new SkiaTextEngine();
        var theme = Theme.Dark();
        var text = "Hello world";
        Assert.Equal(0, engine.HitTestTextPosition(text, theme.Body, -10));
        Assert.True(engine.HitTestTextPosition(text, theme.Body, 1000) >= text.Length - 1);
    }

    [Fact]
    public void RendersCjkFallbackToPng()
    {
        using var engine = new SkiaTextEngine();
        using var renderer = new SkiaRenderer(engine);
        var theme = Theme.Dark();
        var ui = new Ui(engine, theme);
        ui.BeginFrame(FrameInput.Empty(new Size(420, 160)));
        using (ui.Column("root", new Style { Padding = Thickness.All(24), Gap = 8 }))
        {
            ui.Text("Vela — 即时 UI 框架", null, theme.Heading, theme.Text);
            ui.Text("AB CD 你好世界", null, theme.Body, theme.TextMuted);
        }

        var frame = ui.EndFrame();
        using var bitmap = renderer.RenderBitmap(frame.DrawList, 420, 160, 1f, theme.Background);
        Assert.Equal(420, bitmap.Width);
        var nonBackground = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) != new SKColor(theme.Background.R, theme.Background.G, theme.Background.B, theme.Background.A))
                {
                    nonBackground++;
                }
            }
        }

        Assert.True(nonBackground > 1000);
    }

    [Fact]
    public void AllIconsRenderWithoutExceptions()
    {
        using var engine = new SkiaTextEngine();
        using var renderer = new SkiaRenderer(engine);
        var theme = Theme.Dark();
        var ui = new Ui(engine, theme);
        ui.BeginFrame(FrameInput.Empty(new Size(800, 600)));
        using (ui.Column("icons", new Style { Padding = Thickness.All(16), Gap = 4 }))
        {
            foreach (var icon in Icons.All)
            {
                ui.Icon(icon, 16, theme.Text);
            }
        }

        var frame = ui.EndFrame();
        using var bitmap = renderer.RenderBitmap(frame.DrawList, 800, 600, 1f, theme.Background);
        Assert.Equal(800, bitmap.Width);
    }
}
