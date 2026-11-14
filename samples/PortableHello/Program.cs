using Vela;
using Vela.Desktop.Portable;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var name = "Vela";
        using var app = new PortableApp(new PortableAppOptions
        {
            Title = "Portable Vela",
            Width = 900,
            Height = 620,
            Theme = Theme.Dark(),
            UseGpu = true,
        });

        app.Run(ui =>
        {
            using (ui.Column("app", new Style { Width = 620, Padding = Thickness.All(36), Gap = 14 }))
            {
                ui.Heading("Portable Vela");
                ui.Caption("SDL3 + Skia OpenGL, runs on Windows, macOS and Linux.");
                ui.TextField("name", ref name, "Your name");
                if (ui.Button("Say hello", "hello", ButtonVariant.Primary, Icons.ArrowRight).Clicked)
                {
                    ui.Toast($"Hello, {name}!");
                }
            }
        });
    }
}
