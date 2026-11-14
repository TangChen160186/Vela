namespace Vela;

/// <summary>
/// The visual language of a Vela application: colors, typography, spacing and shape.
/// Themes are immutable records, so <c>theme with { Accent = ... }</c> is cheap and safe.
/// </summary>
public sealed record Theme
{
    public required bool IsDark { get; init; }

    // Surfaces
    public required Color Background { get; init; }
    public required Color BackgroundSecondary { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceRaised { get; init; }
    public required Color SurfaceHover { get; init; }
    public required Color SurfaceActive { get; init; }
    public required Color InputBackground { get; init; }
    public required Color CodeBackground { get; init; }
    public required Color TooltipBackground { get; init; }
    public required Color TooltipText { get; init; }

    // Lines
    public required Color Border { get; init; }
    public required Color BorderStrong { get; init; }
    public required Color Scrollbar { get; init; }
    public required Color ScrollbarHover { get; init; }

    // Content
    public required Color Text { get; init; }
    public required Color TextMuted { get; init; }
    public required Color TextSubtle { get; init; }
    public required Color OnAccent { get; init; }

    // Accents and semantics
    public required Color Accent { get; init; }
    public required Color AccentHover { get; init; }
    public required Color AccentPressed { get; init; }
    public required Color AccentSoft { get; init; }
    public required Color Success { get; init; }
    public required Color SuccessSoft { get; init; }
    public required Color Warning { get; init; }
    public required Color WarningSoft { get; init; }
    public required Color Danger { get; init; }
    public required Color DangerSoft { get; init; }
    public required Color Info { get; init; }
    public required Color InfoSoft { get; init; }
    public required Color Selection { get; init; }
    public required Color FocusRing { get; init; }
    public required Color Overlay { get; init; }

    // Typography
    public required FontSpec Body { get; init; }
    public required FontSpec BodyMedium { get; init; }
    public required FontSpec Small { get; init; }
    public required FontSpec SmallMedium { get; init; }
    public required FontSpec Caption { get; init; }
    public required FontSpec CaptionMedium { get; init; }
    public required FontSpec Button { get; init; }
    public required FontSpec Heading { get; init; }
    public required FontSpec Title { get; init; }
    public required FontSpec Display { get; init; }
    public required FontSpec Mono { get; init; }

    // Shape and spacing
    public float RadiusSmall { get; init; } = 6;
    public float RadiusMedium { get; init; } = 9;
    public float RadiusLarge { get; init; } = 14;
    public float RadiusExtraLarge { get; init; } = 20;
    public float ControlHeight { get; init; } = 34;
    public float InputHeight { get; init; } = 36;
    public float SpacingXs { get; init; } = 4;
    public float SpacingSm { get; init; } = 8;
    public float SpacingMd { get; init; } = 12;
    public float SpacingLg { get; init; } = 16;
    public float SpacingXl { get; init; } = 24;
    public float SpacingXxl { get; init; } = 32;

    public Thickness ButtonPadding { get; init; } = Thickness.Symmetric(14, 0);
    public Thickness InputPadding { get; init; } = Thickness.Symmetric(12, 0);
    public Thickness CardPadding { get; init; } = Thickness.All(20);

    public BoxShadow CardShadow { get; init; }
    public BoxShadow PopupShadow { get; init; }
    public BoxShadow ButtonShadow { get; init; }

    /// <summary>Sidebar width used by application shells. Purely a design token.</summary>
    public float SidebarWidth { get; init; } = 244;
    public float TitleBarHeight { get; init; } = 62;

    /// <summary>Returns a copy of this theme with a new accent and matching derived shades.</summary>
    public Theme WithAccent(Color accent) => this with
    {
        Accent = accent,
        AccentHover = Color.Lerp(accent, Color.White, IsDark ? 0.16f : 0.08f),
        AccentPressed = Color.Lerp(accent, Color.Black, 0.18f),
        AccentSoft = accent.WithAlpha(IsDark ? 0.16f : 0.10f).Over(Background),
        Selection = accent.WithAlpha(IsDark ? 0.30f : 0.18f).Over(Background),
        FocusRing = Color.Lerp(accent, Color.White, 0.30f),
    };

    public static Theme Dark(Color? accent = null)
    {
        var theme = new Theme
        {
            IsDark = true,
            Background = Color.FromHex("#0A0C11"),
            BackgroundSecondary = Color.FromHex("#0D1017"),
            Surface = Color.FromHex("#12161F"),
            SurfaceRaised = Color.FromHex("#171C27"),
            SurfaceHover = Color.FromHex("#1D2330"),
            SurfaceActive = Color.FromHex("#252C3B"),
            InputBackground = Color.FromHex("#0E121A"),
            CodeBackground = Color.FromHex("#0C1017"),
            TooltipBackground = Color.FromHex("#252D3D"),
            TooltipText = Color.FromHex("#F4F6FB"),
            Border = Color.FromHex("#242B38"),
            BorderStrong = Color.FromHex("#353E50"),
            Scrollbar = Color.FromHex("#343D4F"),
            ScrollbarHover = Color.FromHex("#48536A"),
            Text = Color.FromHex("#EEF1F7"),
            TextMuted = Color.FromHex("#9AA5B8"),
            TextSubtle = Color.FromHex("#6B768A"),
            OnAccent = Color.White,
            Accent = Color.FromHex("#7A6CFF"),
            AccentHover = Color.FromHex("#8D81FF"),
            AccentPressed = Color.FromHex("#685AF0"),
            AccentSoft = Color.FromHex("#1B1A33"),
            Success = Color.FromHex("#45D6A0"),
            SuccessSoft = Color.FromHex("#10281F"),
            Warning = Color.FromHex("#F4B860"),
            WarningSoft = Color.FromHex("#2A2113"),
            Danger = Color.FromHex("#FF6B7A"),
            DangerSoft = Color.FromHex("#2B151C"),
            Info = Color.FromHex("#63A9FF"),
            InfoSoft = Color.FromHex("#111F31"),
            Selection = Color.FromHex("#342F5C"),
            FocusRing = Color.FromHex("#A79DFF"),
            Overlay = Color.FromHex("#99000000"),
            Body = new("Segoe UI", 14),
            BodyMedium = new("Segoe UI", 14, FontWeight.Medium),
            Small = new("Segoe UI", 12.5f),
            SmallMedium = new("Segoe UI", 12.5f, FontWeight.Medium),
            Caption = new("Segoe UI", 11.5f),
            CaptionMedium = new("Segoe UI", 11.5f, FontWeight.SemiBold),
            Button = new("Segoe UI", 13.5f, FontWeight.Medium),
            Heading = new("Segoe UI", 20f, FontWeight.SemiBold),
            Title = new("Segoe UI", 27f, FontWeight.SemiBold),
            Display = new("Segoe UI", 34f, FontWeight.SemiBold),
            Mono = new("Cascadia Code", 12.5f),
            CardShadow = new BoxShadow(new Vec2(0, 6), 14, 0, Color.FromHex("#30000000")),
            PopupShadow = new BoxShadow(new Vec2(0, 18), 52, -4, Color.FromHex("#99000000")),
            ButtonShadow = new BoxShadow(new Vec2(0, 4), 14, -2, Color.FromHex("#33000000")),
        };

        return accent is { } value ? theme.WithAccent(value) : theme;
    }

    public static Theme Light(Color? accent = null)
    {
        var theme = new Theme
        {
            IsDark = false,
            Background = Color.FromHex("#F5F7FB"),
            BackgroundSecondary = Color.FromHex("#EEF1F7"),
            Surface = Color.FromHex("#FFFFFF"),
            SurfaceRaised = Color.FromHex("#FFFFFF"),
            SurfaceHover = Color.FromHex("#F2F4F9"),
            SurfaceActive = Color.FromHex("#E8ECF4"),
            InputBackground = Color.FromHex("#F8FAFD"),
            CodeBackground = Color.FromHex("#F1F4F9"),
            TooltipBackground = Color.FromHex("#1C2230"),
            TooltipText = Color.FromHex("#F6F8FC"),
            Border = Color.FromHex("#E2E7F0"),
            BorderStrong = Color.FromHex("#CBD3E0"),
            Scrollbar = Color.FromHex("#C5CDDA"),
            ScrollbarHover = Color.FromHex("#AAB5C6"),
            Text = Color.FromHex("#151A25"),
            TextMuted = Color.FromHex("#5D6879"),
            TextSubtle = Color.FromHex("#8B95A6"),
            OnAccent = Color.White,
            Accent = Color.FromHex("#5D5AE8"),
            AccentHover = Color.FromHex("#6D6AF3"),
            AccentPressed = Color.FromHex("#4B48CE"),
            AccentSoft = Color.FromHex("#ECEBFF"),
            Success = Color.FromHex("#128A68"),
            SuccessSoft = Color.FromHex("#E3F7F0"),
            Warning = Color.FromHex("#B97614"),
            WarningSoft = Color.FromHex("#FDF3E2"),
            Danger = Color.FromHex("#D64555"),
            DangerSoft = Color.FromHex("#FCEAEC"),
            Info = Color.FromHex("#2775D0"),
            InfoSoft = Color.FromHex("#E8F1FC"),
            Selection = Color.FromHex("#E3E1FF"),
            FocusRing = Color.FromHex("#6D67F2"),
            Overlay = Color.FromHex("#66111827"),
            Body = new("Segoe UI", 14),
            BodyMedium = new("Segoe UI", 14, FontWeight.Medium),
            Small = new("Segoe UI", 12.5f),
            SmallMedium = new("Segoe UI", 12.5f, FontWeight.Medium),
            Caption = new("Segoe UI", 11.5f),
            CaptionMedium = new("Segoe UI", 11.5f, FontWeight.SemiBold),
            Button = new("Segoe UI", 13.5f, FontWeight.Medium),
            Heading = new("Segoe UI", 20f, FontWeight.SemiBold),
            Title = new("Segoe UI", 27f, FontWeight.SemiBold),
            Display = new("Segoe UI", 34f, FontWeight.SemiBold),
            Mono = new("Cascadia Code", 12.5f),
            CardShadow = new BoxShadow(new Vec2(0, 5), 12, 0, Color.FromHex("#120E1A2B")),
            PopupShadow = new BoxShadow(new Vec2(0, 16), 48, -4, Color.FromHex("#33101A2B")),
            ButtonShadow = new BoxShadow(new Vec2(0, 3), 10, -2, Color.FromHex("#1A0E1A2B")),
        };

        return accent is { } value ? theme.WithAccent(value) : theme;
    }
}

