using SkiaSharp;
using Vela.Rendering.Skia;
using Xunit;

namespace Vela.Rendering.Tests;

public sealed class SmokeRenderTests
{
    [Fact]
    public void RendersTextAndShapesToPng()
    {
        using var text = new SkiaTextEngine();
        using var renderer = new SkiaRenderer(text);
        var theme = Theme.Dark();
        var ui = new Ui(text, theme) { CaptureDiagnostics = true };
        var input = FrameInput.Empty(new Size(900, 600));

        ui.BeginFrame(input);
        using (ui.Column("app", new Style { Padding = Thickness.All(40), Gap = 16 }))
        {
            ui.Text("Vela immediate-mode UI", null, theme.Display, theme.Text);
            ui.Text("A frame is a pure function of state and input.", new Style { MaxWidth = 620 }, theme.Body, theme.TextMuted);
            using (ui.Row("cards", new Style { Gap = 16, Height = 170 }))
            {
                for (var i = 0; i < 3; i++)
                {
                    using (ui.Card($"card-{i}", new Style
                    {
                        Grow = 1,
                        Padding = Thickness.All(20),
                        Background = new Brush(theme.Surface),
                        BorderColor = theme.Border,
                        BorderWidth = 1,
                        CornerRadius = CornerRadius.All(16),
                    }))
                    {
                        ui.Icon(Icons.Sparkles, 20, theme.Accent);
                        ui.Space(10);
                        ui.Text($"Card {i + 1}", null, theme.Heading, theme.Text);
                        ui.Caption("Composed from primitives.");
                    }
                }
            }

            ui.Canvas("chart", view =>
            {
                var d = view.DrawList;
                d.FillRect(view.Bounds, new LinearGradient(
                    new Vec2(0, 0),
                    new Vec2(1, 1),
                    new GradientStop(0, Color.FromHex("#221E42")),
                    new GradientStop(1, Color.FromHex("#16243A"))), CornerRadius.All(18));
                var path = new PathBuilder();
                for (var i = 0; i < 20; i++)
                {
                    var x = view.Bounds.Left + 20 + (i * (view.Bounds.Width - 40) / 19f);
                    var y = view.Bounds.Bottom - 35 - (MathF.Sin(i * 0.55f) * 25) - (i * 3);
                    if (i == 0) path.MoveTo(x, y);
                    else path.LineTo(x, y);
                }

                d.StrokePath(path.Build(), theme.Accent, 2.5f);
            }, new Style { Height = 170 });
        }

        var frame = ui.EndFrame();
        Assert.NotNull(frame.Diagnostics);
        Assert.True(frame.DrawList.Count > 0);
        Assert.True(text.Measure("Hello Vela", theme.Body, new TextMeasureOptions(500)).Width > 20);

        var root = FindRepositoryRoot();
        var output = Path.Combine(root, "artifacts");
        Directory.CreateDirectory(output);
        var png = Path.Combine(output, "smoke.png");
        const float scale = 1.5f;
        renderer.SavePng(frame.DrawList, png, (int)(900 * scale), (int)(600 * scale), scale, theme.Background);
        Assert.True(File.Exists(png));
        using var bitmap = SKBitmap.Decode(png);
        Assert.Equal(1350, bitmap.Width);
        Assert.Equal(900, bitmap.Height);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vela.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
