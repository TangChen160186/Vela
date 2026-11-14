using System.Diagnostics;
using SkiaSharp;
using Vela.Desktop;
using Vela.Rendering.Skia;

namespace Vela.Demo;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var options = CommandLine.Parse(args);
        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        var demo = new DemoApp(options.Dark, options.Page);
        if (options.Overlay.Equals("command", StringComparison.OrdinalIgnoreCase))
        {
            demo.OpenCommandPalette();
        }
        else if (options.Overlay.Equals("project", StringComparison.OrdinalIgnoreCase))
        {
            demo.OpenNewProjectModal();
        }

        if (options.BenchmarkFrames > 0)
        {
            return RunBenchmark(demo, options);
        }

        if (options.SnapshotPath is { } snapshotPath)
        {
            return RenderSnapshot(demo, options, snapshotPath);
        }

        using var app = new DesktopApp(new DesktopAppOptions
        {
            Title = "Vela Studio",
            Width = options.Width,
            Height = options.Height,
            Theme = demo.CreateTheme(),
            TargetFps = 60,
            CaptureDiagnostics = true,
            MaxFrames = options.SmokeFrames,
        });

        app.Run(ui => demo.Build(ui));
        return 0;
    }

    private static int RunBenchmark(DemoApp demo, CommandLineOptions options)
    {
        using var text = new SkiaTextEngine();
        using var renderer = new SkiaRenderer(text);
        var theme = demo.CreateTheme();
        var ui = new Ui(new UiOptions
        {
            TextEngine = text,
            Theme = theme,
            Clipboard = NullClipboard.Instance,
            CaptureDiagnostics = false,
        });

        var size = new Size(options.Width, options.Height);
        using var bitmap = new SKBitmap(new SKImageInfo(options.Width, options.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        var pixels = bitmap.GetPixels();

        void RenderFrame()
        {
            ui.BeginFrame(FrameInput.Empty(size));
            demo.Build(ui);
            var frame = ui.EndFrame();
            renderer.RenderToPixels(frame.DrawList, pixels, options.Width, options.Height, bitmap.RowBytes, 1f, ui.Theme.Background);
        }

        for (var i = 0; i < 10; i++)
        {
            RenderFrame();
        }

        var frames = options.BenchmarkFrames;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < frames; i++)
        {
            RenderFrame();
        }

        clock.Stop();
        var perFrame = clock.Elapsed.TotalMilliseconds / frames;
        Console.WriteLine($"Benchmark: {frames} frames at {size.Width}x{size.Height} (CPU Skia, 1x)");
        Console.WriteLine($"Average frame: {perFrame:0.00} ms  (~{1000 / perFrame:0} FPS ceiling)");
        return 0;
    }

    private static int RenderSnapshot(DemoApp demo, CommandLineOptions options, string path)
    {
        using var text = new SkiaTextEngine();
        using var renderer = new SkiaRenderer(text);
        var theme = demo.CreateTheme();
        var ui = new Ui(new UiOptions
        {
            TextEngine = text,
            Theme = theme,
            CaptureDiagnostics = true,
            Clipboard = NullClipboard.Instance,
        });

        var size = new Size(options.Width, options.Height);
        UiFrameResult? frame = null;
        for (var i = 0; i < 2; i++)
        {
            ui.BeginFrame(FrameInput.Empty(size));
            demo.Build(ui);
            frame = ui.EndFrame();
        }

        if (frame is null)
        {
            return 1;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var scale = MathF.Max(0.5f, options.Scale);
        var pixelWidth = (int)MathF.Round(size.Width * scale);
        var pixelHeight = (int)MathF.Round(size.Height * scale);
        renderer.SavePng(frame.DrawList, path, pixelWidth, pixelHeight, scale, ui.Theme.Background);

        var diagnostics = frame.Diagnostics;
        Console.WriteLine($"Rendered {path} ({pixelWidth}x{pixelHeight})");
        if (diagnostics is not null)
        {
            Console.WriteLine($"Elements: {diagnostics.ElementCount}  Draw commands: {diagnostics.DrawCommandCount}");
            Console.WriteLine($"Build {diagnostics.BuildMilliseconds:0.00} ms  Layout {diagnostics.LayoutMilliseconds:0.00} ms  Snapshot {diagnostics.SnapshotMilliseconds:0.00} ms  Paint {diagnostics.PaintMilliseconds:0.00} ms");
            if (Environment.GetEnvironmentVariable("VELA_DUMP_LAYOUT") == "1")
            {
                foreach (var element in diagnostics.Elements)
                {
                    if (element.Depth <= 7 || element.Text is not null)
                    {
                        Console.WriteLine($"  d{element.Depth,-2} {element.Name,-16} {element.Id} {element.Bounds} text='{element.Text}'");
                    }
                }
            }
        }

        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Vela Studio demo");
        Console.WriteLine();
        Console.WriteLine("  dotnet run --project src/Vela.Demo -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --theme <dark|light>     Initial color mode (default: dark)");
        Console.WriteLine("  --page <overview|projects|analytics|components|data-lab|settings>");
        Console.WriteLine("  --width <pixels>         Logical viewport width (default: 1440)");
        Console.WriteLine("  --height <pixels>        Logical viewport height (default: 900)");
        Console.WriteLine("  --snapshot <path>        Render a PNG instead of opening a window");
        Console.WriteLine("  --scale <factor>         DPI scale for snapshots (default: 1.5)");
        Console.WriteLine("  --overlay <command|project>  Open an overlay in snapshots");
        Console.WriteLine("  --smoke <frames>         Open a window, render N frames and exit");
        Console.WriteLine("  --benchmark <frames>     Measure the full CPU frame pipeline without a window");
        Console.WriteLine("  --help                   Show this help");
    }
}

internal sealed record CommandLineOptions
{
    public bool Dark { get; init; } = true;
    public DemoPage Page { get; init; } = DemoPage.Overview;
    public int Width { get; init; } = 1440;
    public int Height { get; init; } = 900;
    public float Scale { get; init; } = 1.5f;
    public string? SnapshotPath { get; init; }
    public string Overlay { get; init; } = string.Empty;
    public int SmokeFrames { get; init; }
    public int BenchmarkFrames { get; init; }
    public bool ShowHelp { get; init; }
}

internal static class CommandLine
{
    public static CommandLineOptions Parse(string[] args)
    {
        var dark = true;
        var page = DemoPage.Overview;
        var width = 1440;
        var height = 900;
        var scale = 1.5f;
        string? snapshot = null;
        var overlay = string.Empty;
        var smokeFrames = 0;
        var benchmarkFrames = 0;
        bool help = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--theme" when i + 1 < args.Length:
                    dark = !args[++i].Equals("light", StringComparison.OrdinalIgnoreCase);
                    break;
                case "--page" when i + 1 < args.Length:
                    var pageName = args[++i].Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
                    if (Enum.TryParse<DemoPage>(pageName, true, out var parsedPage))
                    {
                        page = parsedPage;
                    }

                    break;
                case "--width" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var parsedWidth)) width = parsedWidth;
                    break;
                case "--height" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var parsedHeight)) height = parsedHeight;
                    break;
                case "--scale" when i + 1 < args.Length:
                    if (float.TryParse(args[++i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedScale)) scale = parsedScale;
                    break;
                case "--snapshot" when i + 1 < args.Length:
                    snapshot = args[++i];
                    break;
                case "--overlay" when i + 1 < args.Length:
                    overlay = args[++i];
                    break;
                case "--smoke" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var parsedSmokeFrames)) smokeFrames = parsedSmokeFrames;
                    break;
                case "--benchmark" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var parsedBenchmarkFrames)) benchmarkFrames = parsedBenchmarkFrames;
                    break;
                case "--help":
                case "-h":
                    help = true;
                    break;
            }
        }

        width = Math.Clamp(width, 800, 4096);
        height = Math.Clamp(height, 600, 4096);
        scale = Math.Clamp(scale, 0.5f, 3f);
        snapshot ??= null;
        return new CommandLineOptions
        {
            Dark = dark,
            Page = page,
            Width = width,
            Height = height,
            Scale = scale,
            SnapshotPath = snapshot,
            Overlay = overlay,
            SmokeFrames = Math.Max(0, smokeFrames),
            BenchmarkFrames = Math.Max(0, benchmarkFrames),
            ShowHelp = help,
        };
    }
}


