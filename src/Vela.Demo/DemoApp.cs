namespace Vela.Demo;

public sealed partial class DemoApp
{
    private DemoPage _page;
    private bool _dark;
    private int _accentIndex;
    private bool _commandOpen;
    private bool _focusCommandSearch;
    private string _commandQuery = string.Empty;
    private bool _newProjectOpen;
    private string _newProjectName = string.Empty;
    private string _newProjectDescription = string.Empty;
    private int _newProjectVisibility;
    private string? _toast;
    private double _toastExpires;
    private int _chartRange;
    private float _sidebarUsage = 0.68f;

    // Component playground state
    private string _demoName = "Alex Morgan";
    private string _demoEmail = "alex@acme.design";
    private string _demoSearch = string.Empty;
    private bool _demoNotifications = true;
    private bool _demoPublicProfile;
    private bool _demoAutoSave = true;
    private bool _demoAnalytics = true;
    private float _demoVolume = 64;
    private float _demoOpacity = 0.82f;
    private int _demoPlan = 1;
    private int _demoVisibility;
    private bool _demoModalOpen;
    private int _demoChoice = 1;
    private string _projectSearch = string.Empty;
    private int _projectFilter;
    private string _inviteEmail = string.Empty;
    private float _labSplit = 320;
    private int _labSelectedRow;
    private int _labSelectedTree;
    private string _labNotes = "Vela keeps the UI a pure function of state.\nTry editing this note, resize the split pane, or keyboard-navigate the table and tree.";

    private readonly List<DemoProject> _projects = [.. DemoData.Projects, .. DemoData.ExtraProjects];
    private readonly HashSet<string> _labExpanded = ["workspace", "design", "engineering"];
    private readonly List<LabRow> _labRows = Enumerable.Range(1, 5000).Select(i => new LabRow(
        i,
        (i % 7) switch { 0 => "Aurora Design System", 1 => "Atlas Mobile", 2 => "Northstar Website", 3 => "Pulse Analytics", 4 => "Ember Brand", 5 => "Kite Components", _ => "Drift Docs" } + $" · {i:0000}",
        (i % 4) switch { 0 => "Mia Kim", 1 => "Sam Rivera", 2 => "Theo Nguyen", _ => "June Park" },
        (i % 3) switch { 0 => "In progress", 1 => "Review", _ => "Shipped" },
        (i * 17) % 101)).ToList();

    public DemoApp(bool dark = true, DemoPage page = DemoPage.Overview)
    {
        _dark = dark;
        _page = page;
        _accentIndex = 0;
    }

    public DemoPage Page
    {
        get => _page;
        set => _page = value;
    }

    public bool Dark
    {
        get => _dark;
        set => _dark = value;
    }

    public Theme CreateTheme() => _dark ? Theme.Dark(DemoData.Accents[_accentIndex]) : Theme.Light(DemoData.Accents[_accentIndex]);

    public void OpenCommandPalette()
    {
        _commandOpen = true;
        _focusCommandSearch = true;
        _commandQuery = string.Empty;
    }

    public void OpenNewProjectModal()
    {
        _newProjectOpen = true;
        _newProjectName = string.Empty;
        _newProjectDescription = string.Empty;
    }

    public void Build(Ui ui)
    {
        var theme = ui.Theme;
        if (ui.Input.DroppedFiles.Count > 0)
        {
            ShowToast(ui, $"Dropped {ui.Input.DroppedFiles.Count} file(s).");
        }

        if (ui.Input.IsShortcut(Key.K))
        {
            _commandOpen = !_commandOpen;
            if (_commandOpen)
            {
                _commandQuery = string.Empty;
                _focusCommandSearch = true;
            }
        }

        if (_toast is not null && ui.Input.Time > _toastExpires)
        {
            _toast = null;
        }

        BuildShell(ui, theme);
        BuildOverlayMenus(ui, theme);
        BuildCommandPalette(ui, theme);
        BuildNewProjectModal(ui, theme);
        BuildComponentModal(ui, theme);

        if (_toast is not null)
        {
            ui.Toast(_toast, Icons.CheckCircle, BadgeTone.Success);
        }
    }

    private void BuildShell(Ui ui, Theme theme)
    {
        var compact = ui.ViewportSize.Width < 980;
        using (ui.Row("shell", new Style { Direction = FlexDirection.Row, Grow = 1 }))
        {
            if (!compact)
            {
                BuildSidebar(ui, theme);
            }

            using (ui.Column("main", new Style
            {
                Direction = FlexDirection.Column,
                Grow = 1,
                Background = new Brush(theme.Background),
            }))
            {
                BuildTopbar(ui, theme, compact);
                ui.Divider();
                using (ui.ScrollArea("main-scroll", 0, new Style { Grow = 1 }))
                {
                    var horizontalPadding = compact ? 20f : 30f;
                    using (ui.Column("page", new Style
                    {
                        Direction = FlexDirection.Column,
                        Padding = new Thickness(horizontalPadding, 24, horizontalPadding + 10, 44),
                        Gap = 22,
                    }))
                    {
                        switch (_page)
                        {
                            case DemoPage.Overview:
                                BuildOverview(ui, theme);
                                break;
                            case DemoPage.Projects:
                                BuildProjectsPage(ui, theme);
                                break;
                            case DemoPage.Analytics:
                                BuildAnalyticsPage(ui, theme);
                                break;
                            case DemoPage.Components:
                                BuildComponentsPage(ui, theme);
                                break;
                            case DemoPage.DataLab:
                                BuildDataLabPage(ui, theme);
                                break;
                            case DemoPage.Settings:
                                BuildSettingsPage(ui, theme);
                                break;
                        }
                    }
                }
            }
        }
    }

    private void BuildSidebar(Ui ui, Theme theme)
    {
        using (ui.Panel("sidebar", new Style
        {
            Direction = FlexDirection.Column,
            Width = 250,
            Padding = new Thickness(14, 16, 14, 14),
            Gap = 2,
            Background = new Brush(theme.BackgroundSecondary),
            BorderColor = theme.Border,
            BorderWidth = 1,
        }))
        {
            using (ui.Row("brand-row", new Style
            {
                Direction = FlexDirection.Row,
                Height = 40,
                AlignItems = AlignItems.Center,
                Gap = 10,
            }))
            {
                BuildLogo(ui, theme);
                using (ui.Column("brand-text", new Style { Direction = FlexDirection.Column, Gap = 0, Grow = 1, JustifyContent = JustifyContent.Center }))
                {
                    ui.Text("Vela", new Style { MaxLines = 1 }, new FontSpec(theme.Body.Family, 16f, FontWeight.SemiBold), theme.Text);
                    ui.Caption("STUDIO", style: new Style { MaxLines = 1 });
                }

                ui.Badge("PRO", BadgeTone.Accent);
            }

            var workspace = ui.Canvas("workspace-switcher", view => PaintWorkspaceSwitcher(view, theme), new Style
            {
                Height = 56,
                Margin = new Thickness(0, 12, 0, 10),
            }, interactive: true);
            if (workspace.Clicked)
            {
                ui.OpenPopup("workspace-menu", workspace.Id);
            }

            ui.Caption("WORKSPACE", style: new Style { Margin = new Thickness(10, 14, 0, 6) });

            if (ui.NavItem("Overview", Icons.LayoutGrid, _page == DemoPage.Overview, "nav-overview", badge: null).Clicked)
            {
                _page = DemoPage.Overview;
            }

            if (ui.NavItem("Projects", Icons.Folder, _page == DemoPage.Projects, "nav-projects", badge: "08").Clicked)
            {
                _page = DemoPage.Projects;
            }

            if (ui.NavItem("Analytics", Icons.BarChart, _page == DemoPage.Analytics, "nav-analytics").Clicked)
            {
                _page = DemoPage.Analytics;
            }

            ui.Caption("BUILD", style: new Style { Margin = new Thickness(10, 14, 0, 6) });

            if (ui.NavItem("Components", Icons.Layers, _page == DemoPage.Components, "nav-components", badge: "New").Clicked)
            {
                _page = DemoPage.Components;
            }

            if (ui.NavItem("Data Lab", Icons.Database, _page == DemoPage.DataLab, "nav-data-lab", badge: "5k").Clicked)
            {
                _page = DemoPage.DataLab;
            }

            if (ui.NavItem("Activity", Icons.Zap, false, "nav-activity", badge: "4").Clicked)
            {
                ShowToast(ui, "Activity feed is up to date.");
            }

            if (ui.NavItem("Settings", Icons.Settings, _page == DemoPage.Settings, "nav-settings").Clicked)
            {
                _page = DemoPage.Settings;
            }

            ui.Spacer(new Style { Grow = 1 });

            BuildStorageCard(ui, theme);

            ui.Divider(style: new Style { Margin = new Thickness(4, 12, 4, 10) });

            var profile = ui.Canvas("profile-card", view => PaintProfileCard(view, theme), new Style { Height = 50 }, interactive: true);
            if (profile.Clicked)
            {
                ui.OpenPopup("user-menu", profile.Id);
            }
        }
    }

    private void BuildStorageCard(Ui ui, Theme theme)
    {
        using (ui.Column("storage-card", new Style
        {
            Direction = FlexDirection.Column,
            Padding = Thickness.All(12),
            Gap = 7,
            Background = new Brush(theme.Surface),
            BorderColor = theme.Border,
            BorderWidth = 1,
            CornerRadius = CornerRadius.All(10),
        }))
        {
            using (ui.Row("storage-heading", new Style { Direction = FlexDirection.Row, AlignItems = AlignItems.Center }))
            {
                ui.Text("Workspace storage", new Style { Grow = 1, MaxLines = 1 }, theme.SmallMedium, theme.TextMuted);
                ui.Text("68%", null, theme.CaptionMedium, theme.TextSubtle);
            }

            ui.ProgressBar(_sidebarUsage, 5, theme.Accent);
            ui.Caption("34.2 GB of 50 GB used");
            ui.Button("Manage plan", variant: ButtonVariant.Ghost, style: new Style
            {
                Height = 26,
                Padding = Thickness.Zero,
                Foreground = theme.Accent,
                TextAlignment = TextAlignment.Left,
            });
        }
    }

    private void BuildTopbar(Ui ui, Theme theme, bool compact)
    {
        var (title, subtitle) = PageText();
        using (ui.Row("topbar", new Style
        {
            Direction = FlexDirection.Row,
            Height = 66,
            Padding = new Thickness(compact ? 20 : 28, 0, compact ? 18 : 26, 0),
            AlignItems = AlignItems.Center,
            Gap = 10,
        }))
        {
            using (ui.Column("topbar-title", new Style
            {
                Direction = FlexDirection.Column,
                Grow = 1,
                Gap = 0,
                JustifyContent = JustifyContent.Center,
            }))
            {
                ui.Text(title, new Style { MaxLines = 1, TextWrap = TextWrapMode.NoWrap }, new FontSpec(theme.Body.Family, 18f, FontWeight.SemiBold), theme.Text);
                if (!compact)
                {
                    ui.Text(subtitle, new Style { MaxLines = 1, TextWrap = TextWrapMode.NoWrap }, theme.Caption, theme.TextMuted);
                }
            }

            var searchWidth = ui.ViewportSize.Width > 1240 ? 262f : 190f;
            var search = ui.SearchTrigger("global-search", "Search or jump to…", "Ctrl K", style: new Style { Width = searchWidth });
            if (search.Clicked)
            {
                _commandOpen = true;
                _focusCommandSearch = true;
                _commandQuery = string.Empty;
            }

            var themeButton = ui.IconButton(_dark ? Icons.Sun : Icons.Moon, "Toggle theme", "theme-toggle", ButtonVariant.Ghost);
            ui.Tooltip(_dark ? "Switch to light" : "Switch to dark", themeButton);
            if (themeButton.Clicked)
            {
                _dark = !_dark;
                ui.Theme = CreateTheme();
            }

            var accentButton = ui.IconButton(Icons.Palette, "Accent color", "accent-toggle", ButtonVariant.Ghost);
            ui.Tooltip("Accent color", accentButton);
            if (accentButton.Clicked)
            {
                ui.OpenPopup("accent-menu", accentButton.Id);
            }

            var notifications = ui.IconButton(Icons.Bell, "Notifications", "notifications", ButtonVariant.Ghost);
            ui.Tooltip("Notifications", notifications);
            if (notifications.Clicked)
            {
                ShowToast(ui, "You're all caught up.");
            }

            var avatar = ui.Canvas("topbar-avatar", view => PaintAvatarButton(view, theme, 36), new Style
            {
                Width = 36,
                Height = 36,
                Margin = new Thickness(4, 0, 0, 0),
            }, interactive: true);
            if (avatar.Clicked)
            {
                ui.OpenPopup("user-menu", avatar.Id);
            }
        }
    }

    private (string Title, string Subtitle) PageText() => _page switch
    {
        DemoPage.Overview => ("Overview", "Here is what is happening across Acme Design."),
        DemoPage.Projects => ("Projects", "Plan, track and ship work in one place."),
        DemoPage.Analytics => ("Analytics", "Signals and trends from the last 30 days."),
        DemoPage.Components => ("Components", "The building blocks available to your team."),
        DemoPage.DataLab => ("Data Lab", "Virtualized tables, trees, split panes and multiline text."),
        DemoPage.Settings => ("Settings", "Tune the workspace to fit the way you work."),
        _ => ("Vela Studio", string.Empty),
    };

    private void BuildOverlayMenus(Ui ui, Theme theme)
    {
        ui.Popup("workspace-menu", () =>
        {
            ui.Caption("SWITCH WORKSPACE", style: new Style { Margin = new Thickness(8, 4, 0, 6) });
            if (ui.MenuItem("Acme Design", "workspace-acme", Icons.Check, "Pro").Clicked)
            {
                ShowToast(ui, "Workspace switched to Acme Design.");
            }

            if (ui.MenuItem("Personal", "workspace-personal", Icons.User).Clicked)
            {
                ShowToast(ui, "Workspace switched to Personal.");
            }

            ui.Divider(style: new Style { Margin = new Thickness(4, 4, 4, 4) });
            if (ui.MenuItem("Create workspace", "workspace-create", Icons.Plus, "⌘ N").Clicked)
            {
                ShowToast(ui, "Workspace creation is coming soon.");
            }
        }, width: 250);

        ui.Popup("accent-menu", () =>
        {
            ui.Caption("ACCENT COLOR", style: new Style { Margin = new Thickness(8, 4, 0, 6) });
            for (var i = 0; i < DemoData.Accents.Length; i++)
            {
                var index = i;
                if (ui.MenuItem(DemoData.AccentNames[i], $"accent-{i}", Icons.Sparkles, i == _accentIndex ? "Active" : null).Clicked)
                {
                    _accentIndex = index;
                    ui.Theme = CreateTheme();
                    ShowToast(ui, $"{DemoData.AccentNames[index]} accent applied.");
                }
            }
        }, width: 230);

        ui.Popup("user-menu", () =>
        {
            ui.Caption("ALEX MORGAN", style: new Style { Margin = new Thickness(8, 4, 0, 2) });
            ui.Caption("alex@acme.design", style: new Style { Margin = new Thickness(8, 0, 0, 6) });
            ui.Divider(style: new Style { Margin = new Thickness(4, 2, 4, 4) });
            if (ui.MenuItem("Account settings", "user-account", Icons.User).Clicked)
            {
                _page = DemoPage.Settings;
            }

            if (ui.MenuItem("Command menu", "user-command", Icons.Command, "Ctrl K").Clicked)
            {
                _commandOpen = true;
                _focusCommandSearch = true;
                _commandQuery = string.Empty;
            }

            ui.Divider(style: new Style { Margin = new Thickness(4, 4, 4, 4) });
            if (ui.MenuItem("Sign out", "user-signout", Icons.ExternalLink, danger: true).Clicked)
            {
                ShowToast(ui, "Signed out of the demo workspace.");
            }
        }, width: 240);
    }

    private void BuildCommandPalette(Ui ui, Theme theme)
    {
        if (!_commandOpen)
        {
            return;
        }

        ui.Modal("command-palette", ref _commandOpen, title: null, build: () =>
        {
            var search = ui.TextField(
                "command-search",
                ref _commandQuery,
                "Search pages, projects and actions…",
                style: new Style
                {
                    Height = 48,
                    Font = new FontSpec(theme.Body.Family, 15f),
                    Padding = new Thickness(14, 0, 14, 0),
                    CornerRadius = CornerRadius.All(10),
                });
            if (_focusCommandSearch)
            {
                ui.Focus(search.Id, keyboardVisible: true);
                _focusCommandSearch = false;
            }

            ui.Caption(string.IsNullOrWhiteSpace(_commandQuery) ? "QUICK ACTIONS" : "RESULTS", style: new Style { Margin = new Thickness(4, 8, 0, 2) });

            var commands = CreateCommands(ui);
            var query = _commandQuery.Trim();
            var shown = 0;
            foreach (var command in commands)
            {
                if (query.Length > 0 &&
                    !command.Label.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                    !command.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var response = ui.MenuItem(command.Label, $"command-{command.Id}", command.Icon, command.Shortcut);
                if (response.Clicked)
                {
                    command.Invoke();
                    _commandOpen = false;
                    break;
                }

                shown++;
                if (shown >= 7)
                {
                    break;
                }
            }

            if (shown == 0)
            {
                using (ui.Column("command-empty", new Style { Padding = Thickness.All(14) }))
                {
                    ui.Caption("No matches. Try “project”, “theme” or “settings”.");
                }
            }

            using (ui.Row("command-footer", new Style
            {
                Direction = FlexDirection.Row,
                Gap = 8,
                AlignItems = AlignItems.Center,
                Margin = new Thickness(0, 8, 0, 2),
            }))
            {
                ui.Badge("Ctrl K", BadgeTone.Neutral);
                ui.Caption("Open anywhere");
                ui.Spacer(new Style { Grow = 1 });
                ui.Badge("ESC", BadgeTone.Neutral);
                ui.Caption("Close");
            }
        }, width: 600, bodyStyle: new Style
        {
            Direction = FlexDirection.Column,
            Padding = Thickness.All(10),
            Gap = 2,
        });
    }

    private List<DemoCommand> CreateCommands(Ui ui) =>
    [
        new("overview", "Go to Overview", "page navigate dashboard home", Icons.LayoutGrid, "G O", () => _page = DemoPage.Overview),
        new("projects", "Go to Projects", "page navigate project", Icons.Folder, "G P", () => _page = DemoPage.Projects),
        new("analytics", "Go to Analytics", "page chart metrics", Icons.BarChart, "G A", () => _page = DemoPage.Analytics),
        new("components", "Go to Components", "page widgets gallery", Icons.Layers, "G C", () => _page = DemoPage.Components),
        new("settings", "Open Settings", "preferences theme", Icons.Settings, "G S", () => _page = DemoPage.Settings),
        new("new-project", "Create a new project", "add create project", Icons.Plus, "N", () =>
        {
            _newProjectOpen = true;
            _newProjectName = string.Empty;
            _newProjectDescription = string.Empty;
        }),
        new("theme", "Toggle dark mode", "appearance light theme", _dark ? Icons.Moon : Icons.Sun, "T", () =>
        {
            _dark = !_dark;
            ui.Theme = CreateTheme();
        }),
        new("invite", "Invite a teammate", "team member people", Icons.Users, "I", () => ShowToast(ui, "Invite link copied to your clipboard.")),
    ];

    private void BuildNewProjectModal(Ui ui, Theme theme)
    {
        if (!_newProjectOpen)
        {
            return;
        }

        ui.Modal("new-project", ref _newProjectOpen, "Create a new project", () =>
        {
            ui.Text("Project name", null, theme.SmallMedium, theme.TextMuted);
            ui.TextField("new-project-name", ref _newProjectName, "e.g. Aurora Design System", style: new Style { Height = 38 });

            ui.Text("Description", null, theme.SmallMedium, theme.TextMuted);
            ui.TextField("new-project-description", ref _newProjectDescription, "What is this project about?", style: new Style { Height = 38 });

            ui.Text("Visibility", null, theme.SmallMedium, theme.TextMuted);
            ui.Dropdown("new-project-visibility", ["Workspace", "Private", "Public"], ref _newProjectVisibility, style: new Style { Height = 38 });

            using (ui.Row("new-project-actions", new Style
            {
                Direction = FlexDirection.Row,
                Gap = 8,
                JustifyContent = JustifyContent.End,
                Margin = new Thickness(0, 12, 0, 0),
            }))
            {
                if (ui.Button("Cancel", "new-project-cancel", ButtonVariant.Ghost).Clicked)
                {
                    _newProjectOpen = false;
                }

                var create = ui.Button("Create project", "new-project-create", ButtonVariant.Primary, Icons.ArrowRight, disabled: string.IsNullOrWhiteSpace(_newProjectName));
                if (create.Clicked)
                {
                    _projects.Insert(0, new DemoProject(
                        _newProjectName.Trim(),
                        string.IsNullOrWhiteSpace(_newProjectDescription) ? "A brand new project" : _newProjectDescription.Trim(),
                        "Planning",
                        BadgeTone.Neutral,
                        ["AL"],
                        0,
                        "Just now",
                        DemoData.Accents[_accentIndex]));
                    _newProjectOpen = false;
                    _page = DemoPage.Projects;
                    ShowToast(ui, $"“{_newProjectName.Trim()}” created.");
                }
            }
        }, width: 520, bodyStyle: new Style
        {
            Direction = FlexDirection.Column,
            Padding = Thickness.All(22),
            Gap = 8,
        });
    }

    private void ShowToast(Ui ui, string message)
    {
        _toast = message;
        _toastExpires = ui.Input.Time + 3.2;
    }

    private sealed record DemoCommand(
        string Id,
        string Label,
        string Keywords,
        IconData Icon,
        string? Shortcut,
        Action Invoke);

    private static Style CardStyle(Theme theme, Thickness? padding = null, float radius = 16) => new()
    {
        Direction = FlexDirection.Column,
        Padding = padding ?? Thickness.All(20),
        Gap = 12,
        Background = new Brush(theme.Surface),
        BorderColor = theme.Border,
        BorderWidth = 1,
        CornerRadius = CornerRadius.All(radius),
        Shadow = theme.CardShadow,
    };

}




