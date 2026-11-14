namespace Vela.Demo;

public enum DemoPage
{
    Overview,
    Projects,
    Analytics,
    Components,
    DataLab,
    Settings,
}

internal sealed record DemoProject(
    string Name,
    string Description,
    string Status,
    BadgeTone StatusTone,
    string[] Members,
    int Progress,
    string Updated,
    Color Accent);

internal sealed record LabRow(int Id, string Name, string Owner, string Status, int Progress);

internal sealed record DemoActivity(
    string Initials,
    string Name,
    string Action,
    string Target,
    string Time,
    Color Accent);

internal static class DemoData
{
    public static readonly DemoProject[] Projects =
    [
        new("Aurora Design System", "Component library and design tokens", "In progress", BadgeTone.Accent, ["AL", "MK", "JR"], 68, "2h ago", Color.FromHex("#7A6CFF")),
        new("Atlas Mobile", "Cross-platform companion app", "Review", BadgeTone.Warning, ["SR", "AL"], 42, "5h ago", Color.FromHex("#4F8DFF")),
        new("Northstar Website", "Marketing site refresh", "Shipped", BadgeTone.Success, ["MK", "TN", "JR"], 100, "Yesterday", Color.FromHex("#2BB6A8")),
        new("Pulse Analytics", "Realtime product telemetry", "Planning", BadgeTone.Neutral, ["JR", "AL", "SR"], 18, "2d ago", Color.FromHex("#F2A94B")),
        new("Ember Brand", "Identity exploration and guidelines", "In progress", BadgeTone.Accent, ["TN", "MK"], 57, "3d ago", Color.FromHex("#F26D8D")),
    ];

    public static readonly DemoProject[] ExtraProjects =
    [
        new("Drift Docs", "Knowledge base for the product team", "Planning", BadgeTone.Neutral, ["AL", "TN"], 12, "4d ago", Color.FromHex("#63A9FF")),
        new("Kite Components", "Internal UI primitives", "In progress", BadgeTone.Accent, ["MK", "JR"], 74, "1w ago", Color.FromHex("#8A7CFF")),
    ];

    public static readonly DemoActivity[] Activity =
    [
        new("MK", "Mia Kim", "shipped", "Aurora v2.1", "12m", Color.FromHex("#7A6CFF")),
        new("SR", "Sam Rivera", "commented on", "Atlas Mobile › Checkout", "48m", Color.FromHex("#4F8DFF")),
        new("TN", "Theo Nguyen", "created", "Northstar launch plan", "2h", Color.FromHex("#2BB6A8")),
        new("JR", "June Park", "joined", "Pulse Analytics", "4h", Color.FromHex("#F2A94B")),
    ];

    public static readonly string[] AccentNames = ["Violet", "Blue", "Teal", "Rose", "Amber"];
    public static readonly Color[] Accents =
    [
        Color.FromHex("#7A6CFF"),
        Color.FromHex("#4F8DFF"),
        Color.FromHex("#2BB6A8"),
        Color.FromHex("#F26D8D"),
        Color.FromHex("#F2A94B"),
    ];

    public static readonly string[] Collaborators = ["AL", "MK", "SR", "TN", "JR"];
}

