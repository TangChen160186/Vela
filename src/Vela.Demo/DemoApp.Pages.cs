using Vela.Platform.Win32;

namespace Vela.Demo;

public sealed partial class DemoApp
{
    private static readonly float[][] SparkData =
    [
        [12, 18, 14, 24, 20, 30, 26, 36],
        [30, 24, 32, 28, 38, 34, 44, 40],
        [8, 14, 12, 22, 19, 28, 24, 33],
        [20, 16, 24, 22, 30, 42, 36, 44],
    ];

    private static readonly (string Label, string Value, string Delta, BadgeTone Tone, IconData Icon)[] StatData =
    [
        ("Active projects", "24", "+12.4%", BadgeTone.Success, Icons.Folder),
        ("Weekly revenue", "$48.2k", "+8.1%", BadgeTone.Success, Icons.CreditCard),
        ("Tasks completed", "182", "+21.9%", BadgeTone.Accent, Icons.CheckCircle),
        ("Team velocity", "86 pts", "-2.3%", BadgeTone.Warning, Icons.Zap),
    ];

    private static readonly float[][] RevenueSeries =
    [
        [32, 38, 34, 46, 42, 55, 51],
        [24, 28, 25, 34, 31, 38, 36, 45, 42, 51, 47, 58, 54, 64],
        [18, 22, 20, 27, 25, 31, 29, 35, 33, 40, 38, 46, 44, 52, 50, 59, 56],
    ];

    private static readonly string[][] RevenueLabels =
    [
        ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"],
        ["Nov 1", "Nov 5", "Nov 10", "Nov 15", "Nov 20", "Nov 26", "Nov 30"],
        ["Sep", "Sep 15", "Oct", "Oct 15", "Nov", "Nov 15", "Nov 30"],
    ];

    private void BuildOverview(Ui ui, Theme theme)
    {
        BuildHero(ui, theme);
        BuildStats(ui, theme);
        BuildMiddle(ui, theme);
        BuildProjectsTable(ui, theme, 3, "Recent projects");
    }

    private void BuildHero(Ui ui, Theme theme)
    {
        using (ui.Card("hero", new Style
        {
            Direction = FlexDirection.Row,
            Height = 174,
            Padding = new Thickness(30, 22, 24, 22),
            Gap = 20,
            AlignItems = AlignItems.Center,
            Background = new LinearGradient(
                new Vec2(0, 0),
                new Vec2(1, 1),
                new GradientStop(0, Color.FromHex("#352975")),
                new GradientStop(0.48f, Color.FromHex("#4A3DC6")),
                new GradientStop(1, Color.FromHex("#1E6FA8"))),
            BorderWidth = 0,
            CornerRadius = CornerRadius.All(20),
            Shadow = new BoxShadow(new Vec2(0, 16), 44, -8, theme.Accent.WithAlpha(0.26f)),
        }))
        {
            using (ui.Column("hero-copy", new Style
            {
                Direction = FlexDirection.Column,
                Grow = 1,
                Gap = 7,
                JustifyContent = JustifyContent.Center,
            }))
            {
                ui.Caption("FRIDAY, NOVEMBER 13", style: new Style { MaxLines = 1 }, color: Color.White.WithAlpha(0.62f));
                ui.Text("Good evening, Alex", new Style { MaxLines = 1, TextWrap = TextWrapMode.NoWrap }, new FontSpec(theme.Body.Family, 29f, FontWeight.SemiBold), Color.White);
                ui.Text(
                    "You have 4 projects moving forward and 2 reviews waiting for your attention.",
                    new Style { MaxWidth = 600, MaxLines = 2 },
                    theme.Body,
                    Color.White.WithAlpha(0.72f));

                using (ui.Row("hero-actions", new Style
                {
                    Direction = FlexDirection.Row,
                    Gap = 9,
                    Margin = new Thickness(0, 7, 0, 0),
                }))
                {
                    var newProject = ui.Button("New project", "hero-new-project", ButtonVariant.Secondary, Icons.Plus, style: new Style
                    {
                        Height = 36,
                        Padding = new Thickness(14, 0, 16, 0),
                        Background = new Brush(Color.White),
                        Foreground = Color.FromHex("#2C2469"),
                        BorderColor = Color.Transparent,
                        CornerRadius = CornerRadius.All(8),
                    });
                    if (newProject.Clicked)
                    {
                        _newProjectOpen = true;
                        _newProjectName = string.Empty;
                        _newProjectDescription = string.Empty;
                    }

                    if (ui.Button("View roadmap", "hero-roadmap", ButtonVariant.Ghost, style: new Style
                    {
                        Height = 36,
                        Padding = Thickness.Symmetric(12, 0),
                        Foreground = Color.White.WithAlpha(0.9f),
                        BorderColor = Color.Transparent,
                    }).Clicked)
                    {
                        ShowToast(ui, "Roadmap opened in the demo.");
                    }
                }
            }

            ui.Canvas("hero-art", view => DemoCharts.HeroArt(view.DrawList, view.Bounds, theme), new Style
            {
                Width = 300,
                Height = 132,
            });
        }
    }

    private static void BuildStats(Ui ui, Theme theme)
    {
        using (ui.Row("stats-row", new Style
        {
            Direction = FlexDirection.Row,
            Height = 146,
            Gap = 16,
        }))
        {
            for (var i = 0; i < StatData.Length; i++)
            {
                var index = i;
                var stat = StatData[i];
                using (ui.Card($"stat-{i}", new Style
                {
                    Direction = FlexDirection.Column,
                    Grow = 1,
                    Padding = Thickness.All(16),
                    Gap = 5,
                    Background = new Brush(theme.Surface),
                    BorderColor = theme.Border,
                    BorderWidth = 1,
                    CornerRadius = CornerRadius.All(14),
                    Shadow = theme.CardShadow,
                }))
                {
                    using (ui.Row($"stat-heading-{i}", new Style
                    {
                        Direction = FlexDirection.Row,
                        AlignItems = AlignItems.Center,
                        Gap = 6,
                    }))
                    {
                        ui.Text(stat.Label, new Style { Grow = 1, MaxLines = 1 }, theme.SmallMedium, theme.TextMuted);
                        ui.Icon(stat.Icon, 15, i switch
                        {
                            0 => theme.Accent,
                            1 => theme.Success,
                            2 => theme.Info,
                            _ => theme.Warning,
                        });
                    }

                    ui.Text(stat.Value, new Style { MaxLines = 1, TextWrap = TextWrapMode.NoWrap }, new FontSpec(theme.Body.Family, 25f, FontWeight.SemiBold), theme.Text);

                    using (ui.Row($"stat-delta-{i}", new Style
                    {
                        Direction = FlexDirection.Row,
                        AlignItems = AlignItems.Center,
                        Gap = 7,
                    }))
                    {
                        ui.Badge(stat.Delta, stat.Tone);
                        ui.Caption("vs last week");
                    }

                    ui.Canvas($"spark-{i}", view => DemoCharts.Sparkline(view.DrawList, view.Bounds, SparkData[index], i == 3 ? theme.Warning : theme.Accent, theme), new Style { Height = 27 });
                }
            }
        }
    }

    private void BuildMiddle(Ui ui, Theme theme)
    {
        using (ui.Row("middle-row", new Style
        {
            Direction = FlexDirection.Row,
            Height = 326,
            Gap = 16,
        }))
        {
            using (ui.Card("revenue-card", new Style
            {
                Direction = FlexDirection.Column,
                Grow = 2,
                Padding = Thickness.All(20),
                Gap = 10,
                Background = new Brush(theme.Surface),
                BorderColor = theme.Border,
                BorderWidth = 1,
                CornerRadius = CornerRadius.All(16),
                Shadow = theme.CardShadow,
            }))
            {
                using (ui.Row("revenue-header", new Style
                {
                    Direction = FlexDirection.Row,
                    AlignItems = AlignItems.Center,
                    Gap = 10,
                    Height = 36,
                }))
                {
                    using (ui.Column("revenue-title", new Style { Direction = FlexDirection.Column, Gap = 0, Grow = 1 }))
                    {
                        ui.Text("Revenue", new Style { MaxLines = 1 }, theme.SmallMedium, theme.Text);
                        ui.Caption("Recurring revenue over time");
                    }

                    using (ui.Row("range-switcher", new Style
                    {
                        Direction = FlexDirection.Row,
                        Gap = 4,
                        AlignItems = AlignItems.Center,
                    }))
                    {
                        for (var i = 0; i < 3; i++)
                        {
                            var index = i;
                            var selected = _chartRange == i;
                            if (ui.Button(i == 0 ? "7D" : i == 1 ? "30D" : "90D", $"range-{i}", selected ? ButtonVariant.Secondary : ButtonVariant.Ghost, style: new Style
                                {
                                    Width = 40,
                                    Height = 26,
                                    Padding = Thickness.Zero,
                                    CornerRadius = CornerRadius.All(6),
                                }).Clicked)
                            {
                                _chartRange = index;
                            }
                        }
                    }
                }

                ui.Canvas("revenue-chart", view => DemoCharts.AreaChart(
                    view.DrawList,
                    view.Bounds,
                    RevenueSeries[_chartRange],
                    RevenueLabels[_chartRange],
                    theme,
                    view.Response.Hovered,
                    view.Input.PointerPosition), new Style { Height = 228, Grow = 1 }, interactive: true);
            }

            BuildActivityCard(ui, theme);
        }
    }

    private void BuildActivityCard(Ui ui, Theme theme)
    {
        using (ui.Card("activity-card", new Style
        {
            Direction = FlexDirection.Column,
            Grow = 1,
            Padding = Thickness.All(18),
            Gap = 9,
            Background = new Brush(theme.Surface),
            BorderColor = theme.Border,
            BorderWidth = 1,
            CornerRadius = CornerRadius.All(16),
            Shadow = theme.CardShadow,
        }))
        {
            using (ui.Row("activity-header", new Style
            {
                Direction = FlexDirection.Row,
                AlignItems = AlignItems.Center,
                Height = 40,
            }))
            {
                using (ui.Column("activity-title", new Style { Direction = FlexDirection.Column, Gap = 0, Grow = 1 }))
                {
                    ui.Text("Team activity", new Style { MaxLines = 1 }, theme.SmallMedium, theme.Text);
                    ui.Caption("Latest from your workspace");
                }

                ui.Badge("Live", BadgeTone.Success);
            }

            foreach (var activity in DemoData.Activity)
            {
                using (ui.Row($"activity-{activity.Initials}-{activity.Time}", new Style
                {
                    Direction = FlexDirection.Row,
                    Height = 43,
                    AlignItems = AlignItems.Center,
                    Gap = 10,
                }))
                {
                    ui.Avatar(activity.Initials, 28, activity.Accent.WithAlpha(0.22f));
                    using (ui.Column("activity-copy", new Style { Direction = FlexDirection.Column, Gap = 0, Grow = 1 }))
                    {
                        ui.Text(activity.Name, new Style { MaxLines = 1 }, theme.SmallMedium, theme.Text);
                        ui.Text($"{activity.Action} {activity.Target}", new Style { MaxLines = 1, TextWrap = TextWrapMode.NoWrap }, theme.Caption, theme.TextMuted);
                    }

                    ui.Caption(activity.Time, style: new Style
                    {
                        Width = 38,
                        MaxLines = 1,
                        TextWrap = TextWrapMode.NoWrap,
                        TextAlignment = TextAlignment.Right,
                        Shrink = 0,
                    });
                }
            }

            ui.Spacer(new Style { Grow = 1 });
            if (ui.Button("View all activity", "view-activity", ButtonVariant.Ghost, style: new Style
            {
                Height = 28,
                Padding = Thickness.Zero,
                Foreground = theme.TextMuted,
                TextAlignment = TextAlignment.Left,
            }).Clicked)
            {
                ShowToast(ui, "Opening the full activity feed.");
            }
        }
    }

    private void BuildProjectsTable(Ui ui, Theme theme, int count, string heading)
    {
        using (ui.Card("projects-card", new Style
        {
            Direction = FlexDirection.Column,
            Padding = Thickness.Zero,
            Gap = 0,
            Background = new Brush(theme.Surface),
            BorderColor = theme.Border,
            BorderWidth = 1,
            CornerRadius = CornerRadius.All(16),
            Shadow = theme.CardShadow,
        }))
        {
            using (ui.Row("projects-header", new Style
            {
                Direction = FlexDirection.Row,
                Height = 68,
                Padding = new Thickness(20, 0, 14, 0),
                AlignItems = AlignItems.Center,
                Gap = 10,
            }))
            {
                using (ui.Column("projects-title-block", new Style { Direction = FlexDirection.Column, Gap = 1, Grow = 1 }))
                {
                    ui.Text(heading, new Style { MaxLines = 1 }, theme.SmallMedium, theme.Text);
                    ui.Caption("Projects your team is actively shaping");
                }

                if (ui.Button("View all", "projects-view-all", ButtonVariant.Ghost, Icons.ArrowRight, style: new Style
                    {
                        Height = 30,
                        Padding = Thickness.Symmetric(8, 0),
                    }).Clicked)
                {
                    _page = DemoPage.Projects;
                }

                var filter = ui.IconButton(Icons.Filter, "Filter projects", "projects-filter", ButtonVariant.Ghost);
                ui.Tooltip("Filter projects", filter);
            }

            ui.Divider();
            BuildTableHeader(ui, theme);

            var shown = Math.Min(count, _projects.Count);
            for (var i = 0; i < shown; i++)
            {
                BuildProjectRow(ui, theme, _projects[i], i);
                if (i < shown - 1)
                {
                    ui.Divider(style: new Style { Margin = new Thickness(16, 0, 16, 0) });
                }
            }

            using (ui.Row("projects-footer", new Style
            {
                Direction = FlexDirection.Row,
                Height = 50,
                Padding = new Thickness(20, 0, 16, 0),
                AlignItems = AlignItems.Center,
                Gap = 8,
            }))
            {
                ui.Caption($"Showing {shown} of {_projects.Count} projects");
                ui.Spacer(new Style { Grow = 1 });
                if (ui.Button("Open all projects", "projects-open-all", ButtonVariant.Ghost, style: new Style
                    {
                        Height = 28,
                        Padding = Thickness.Zero,
                        Foreground = theme.Accent,
                    }).Clicked)
                {
                    _page = DemoPage.Projects;
                }
            }
        }
    }

    private static void BuildTableHeader(Ui ui, Theme theme)
    {
        using (ui.Row("table-header", new Style
        {
            Direction = FlexDirection.Row,
            Height = 34,
            Padding = new Thickness(20, 0, 20, 0),
            Gap = 12,
            AlignItems = AlignItems.Center,
        }))
        {
            ui.Caption("PROJECT", style: new Style { Grow = 2, MaxLines = 1 });
            ui.Caption("STATUS", style: new Style { Width = 104, MaxLines = 1 });
            ui.Caption("MEMBERS", style: new Style { Width = 116, MaxLines = 1 });
            ui.Caption("UPDATED", style: new Style { Width = 82, MaxLines = 1 });
            ui.Caption(string.Empty, style: new Style { Width = 30 });
        }
    }

    private void BuildProjectRow(Ui ui, Theme theme, DemoProject project, int index)
    {
        using (ui.Row($"project-{index}", new Style
        {
            Direction = FlexDirection.Row,
            Height = 61,
            Padding = new Thickness(20, 0, 16, 0),
            Gap = 12,
            AlignItems = AlignItems.Center,
        }))
        {
            using (ui.Row($"project-name-{index}", new Style
            {
                Direction = FlexDirection.Row,
                Grow = 2,
                AlignItems = AlignItems.Center,
                Gap = 10,
            }))
            {
                using (ui.Panel($"project-accent-{index}", new Style
                {
                    Width = 6,
                    Height = 32,
                    Background = new Brush(project.Accent),
                    CornerRadius = CornerRadius.All(3),
                }))
                {
                }

                using (ui.Column($"project-copy-{index}", new Style { Direction = FlexDirection.Column, Gap = 1, Grow = 1 }))
                {
                    ui.Text(project.Name, new Style { MaxLines = 1, TextWrap = TextWrapMode.NoWrap }, theme.SmallMedium, theme.Text);
                    ui.Caption(project.Description);
                }
            }

            using (ui.Column($"project-status-{index}", new Style
            {
                Direction = FlexDirection.Column,
                Width = 104,
                Gap = 3,
                AlignItems = AlignItems.Start,
            }))
            {
                ui.Badge(project.Status, project.StatusTone);
                ui.Text($"{project.Progress}%", new Style { Width = 44, MaxLines = 1, TextWrap = TextWrapMode.NoWrap }, theme.Caption, theme.TextSubtle);
            }

            using (ui.Row($"project-members-{index}", new Style
            {
                Direction = FlexDirection.Row,
                Width = 116,
                Gap = 3,
                AlignItems = AlignItems.Center,
            }))
            {
                for (var m = 0; m < Math.Min(3, project.Members.Length); m++)
                {
                    ui.Avatar(project.Members[m], 25, project.Accent.WithAlpha(0.16f + m * 0.05f));
                }

                if (project.Members.Length > 3)
                {
                    ui.Caption($"+{project.Members.Length - 3}");
                }
            }

            ui.Caption(project.Updated, style: new Style { Width = 82, MaxLines = 1 });

            var more = ui.IconButton(Icons.MoreHorizontal, "Project actions", $"project-more-{_page}-{index}", ButtonVariant.Ghost, style: new Style
            {
                Width = 30,
                Height = 30,
                Padding = Thickness.Zero,
            });
            ui.Tooltip("Project actions", more);
            if (more.Clicked)
            {
                ShowToast(ui, $"{project.Name} actions opened.");
            }
        }
    }

    private void BuildProjectsPage(Ui ui, Theme theme)
    {
        using (ui.Row("projects-summary", new Style
        {
            Direction = FlexDirection.Row,
            Height = 96,
            Gap = 16,
        }))
        {
            BuildMiniStat(ui, theme, "portfolio", "Active projects", "6", Icons.Folder, theme.Accent);
            BuildMiniStat(ui, theme, "on-track", "On track", "83%", Icons.CheckCircle, theme.Success);
            BuildMiniStat(ui, theme, "reviews", "Needs review", "2", Icons.Eye, theme.Warning);
        }

        using (ui.Row("projects-toolbar", new Style
        {
            Direction = FlexDirection.Row,
            AlignItems = AlignItems.Center,
            Gap = 10,
        }))
        {
            ui.TextField("project-search", ref _projectSearch, "Search projects…", style: new Style { Width = 290, Height = 36 });
            ui.Dropdown("project-filter", ["All projects", "In progress", "Review", "Planning"], ref _projectFilter, style: new Style { Width = 160, Height = 36 });
            ui.Spacer(new Style { Grow = 1 });
            if (ui.Button("New project", "projects-new", ButtonVariant.Primary, Icons.Plus).Clicked)
            {
                _newProjectOpen = true;
                _newProjectName = string.Empty;
                _newProjectDescription = string.Empty;
            }
        }

        var filtered = _projects
            .Where(p => string.IsNullOrWhiteSpace(_projectSearch) ||
                        p.Name.Contains(_projectSearch, StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains(_projectSearch, StringComparison.OrdinalIgnoreCase))
            .Where(p => _projectFilter switch
            {
                1 => p.Status == "In progress",
                2 => p.Status == "Review",
                3 => p.Status == "Planning",
                _ => true,
            })
            .ToList();

        if (filtered.Count == 0)
        {
            using (ui.Card("projects-empty", CardStyle(theme, Thickness.All(32))))
            {
                ui.Icon(Icons.Search, 24, theme.TextSubtle);
                ui.Text("No projects found", null, theme.Heading, theme.Text);
                ui.Caption("Try a different search term or filter.");
            }

            return;
        }

        BuildFilteredTable(ui, theme, filtered);
    }

    private static void BuildMiniStat(Ui ui, Theme theme, string id, string label, string value, IconData icon, Color color)
    {
        using (ui.Card($"mini-{id}", new Style
        {
            Direction = FlexDirection.Row,
            Grow = 1,
            Padding = Thickness.All(18),
            Gap = 14,
            AlignItems = AlignItems.Center,
            Background = new Brush(theme.Surface),
            BorderColor = theme.Border,
            BorderWidth = 1,
            CornerRadius = CornerRadius.All(14),
            Shadow = theme.CardShadow,
        }))
        {
            ui.Canvas($"mini-icon-{id}", view =>
            {
                view.DrawList.FillRect(view.Bounds, new Brush(color.WithAlpha(0.14f)), CornerRadius.All(10));
                view.DrawList.DrawIcon(icon, view.Bounds.Deflate(Thickness.All(9)), color);
            }, new Style { Width = 40, Height = 40 });

            using (ui.Column($"mini-copy-{id}", new Style { Direction = FlexDirection.Column, Gap = 1, Grow = 1 }))
            {
                ui.Caption(label);
                ui.Text(value, new Style { MaxLines = 1 }, new FontSpec(theme.Body.Family, 22f, FontWeight.SemiBold), theme.Text);
            }
        }
    }

    private void BuildFilteredTable(Ui ui, Theme theme, List<DemoProject> projects)
    {
        using (ui.Card("filtered-projects", new Style
        {
            Direction = FlexDirection.Column,
            Padding = Thickness.Zero,
            Gap = 0,
            Background = new Brush(theme.Surface),
            BorderColor = theme.Border,
            BorderWidth = 1,
            CornerRadius = CornerRadius.All(16),
            Shadow = theme.CardShadow,
        }))
        {
            using (ui.Row("filtered-header", new Style
            {
                Direction = FlexDirection.Row,
                Height = 54,
                Padding = new Thickness(20, 0, 16, 0),
                AlignItems = AlignItems.Center,
            }))
            {
                ui.Text($"{projects.Count} projects", new Style { Grow = 1 }, theme.SmallMedium, theme.Text);
                ui.Caption("Sorted by recently updated");
            }

            ui.Divider();
            BuildTableHeader(ui, theme);
            for (var i = 0; i < projects.Count; i++)
            {
                BuildProjectRow(ui, theme, projects[i], i);
                if (i < projects.Count - 1)
                {
                    ui.Divider(style: new Style { Margin = new Thickness(16, 0, 16, 0) });
                }
            }
        }
    }

    private static void BuildAnalyticsPage(Ui ui, Theme theme)
    {
        using (ui.Row("analytics-kpis", new Style
        {
            Direction = FlexDirection.Row,
            Height = 100,
            Gap = 16,
        }))
        {
            BuildMiniStat(ui, theme, "visitors", "Unique visitors", "84.3k", Icons.Users, theme.Info);
            BuildMiniStat(ui, theme, "conversion", "Conversion", "6.8%", Icons.Zap, theme.Success);
            BuildMiniStat(ui, theme, "session", "Avg. session", "4m 12s", Icons.Clock, theme.Accent);
        }

        using (ui.Card("audience-card", CardStyle(theme, Thickness.All(22))))
        {
            using (ui.Row("audience-heading", new Style
            {
                Direction = FlexDirection.Row,
                AlignItems = AlignItems.Center,
                Gap = 10,
            }))
            {
                using (ui.Column("audience-copy", new Style { Direction = FlexDirection.Column, Grow = 1, Gap = 2 }))
                {
                    ui.Text("Audience growth", null, theme.BodyMedium, theme.Text);
                    ui.Caption("Unique visitors across all channels");
                }

                ui.Badge("+18.2%", BadgeTone.Success);
            }

            ui.Canvas("audience-chart", view => DemoCharts.AreaChart(
                view.DrawList,
                view.Bounds,
                [30, 38, 34, 46, 42, 52, 48, 61, 57, 72, 68, 82, 79, 92],
                ["Sep", "Sep 15", "Oct", "Oct 15", "Nov", "Nov 15", "Nov 30"],
                theme,
                view.Response.Hovered,
                view.Input.PointerPosition), new Style { Height = 270, Grow = 1 }, interactive: true);
        }

        using (ui.Row("analytics-detail-row", new Style
        {
            Direction = FlexDirection.Row,
            Height = 300,
            Gap = 16,
        }))
        {
            using (ui.Card("donut-card", CardStyle(theme, Thickness.All(22))))
            {
                ui.Text("Traffic sources", null, theme.BodyMedium, theme.Text);
                ui.Caption("Where your visitors come from");
                ui.Canvas("traffic-donut", view => DemoCharts.Donut(view.DrawList, view.Bounds, theme), new Style { Height = 218, Grow = 1 });
            }

            using (ui.Card("channels-card", new Style
            {
                Direction = FlexDirection.Column,
                Grow = 1,
                Padding = Thickness.All(22),
                Gap = 12,
                Background = new Brush(theme.Surface),
                BorderColor = theme.Border,
                BorderWidth = 1,
                CornerRadius = CornerRadius.All(16),
                Shadow = theme.CardShadow,
            }))
            {
                ui.Text("Weekly sessions", null, theme.BodyMedium, theme.Text);
                ui.Caption("Sessions by day, last 7 days");
                ui.Canvas("sessions-chart", view => DemoCharts.Bars(
                    view.DrawList,
                    view.Bounds,
                    [38, 55, 44, 68, 58, 76, 64],
                    ["M", "T", "W", "T", "F", "S", "S"],
                    theme), new Style { Height = 218, Grow = 1 });
            }
        }
    }

    private void BuildComponentsPage(Ui ui, Theme theme)
    {
        using (ui.Row("component-row-1", new Style
        {
            Direction = FlexDirection.Row,
            Gap = 16,
            AlignItems = AlignItems.Stretch,
        }))
        {
            ComponentCard(ui, theme, "component-buttons", "Buttons", "Actions, with hierarchy and intent.", () =>
            {
                using (ui.Row("button-row-1", new Style { Direction = FlexDirection.Row, Gap = 8, AlignItems = AlignItems.Center }))
                {
                    if (ui.Button("New project", "demo-primary", ButtonVariant.Primary, Icons.Plus).Clicked)
                    {
                        ShowToast(ui, "Primary action triggered.");
                    }

                    ui.Button("Secondary", "demo-secondary", ButtonVariant.Secondary);
                    ui.Button("Ghost", "demo-ghost", ButtonVariant.Ghost);
                    ui.Button("Delete", "demo-danger", ButtonVariant.Danger, Icons.Trash);
                }

                using (ui.Row("button-row-2", new Style { Direction = FlexDirection.Row, Gap = 6, AlignItems = AlignItems.Center }))
                {
                    ui.IconButton(Icons.Bell, "Notifications", "demo-icon-bell", ButtonVariant.Ghost);
                    ui.IconButton(Icons.Settings, "Settings", "demo-icon-settings", ButtonVariant.Secondary);
                    ui.IconButton(Icons.MoreHorizontal, "More actions", "demo-icon-more", ButtonVariant.Ghost);
                    ui.Button("Disabled", "demo-disabled", ButtonVariant.Secondary, disabled: true);
                }

                ui.Divider();
                ui.Caption("Buttons are focusable and activate with Enter or Space.");
            });

            ComponentCard(ui, theme, "component-inputs", "Inputs", "Capture text and choices with validation-ready state.", () =>
            {
                ui.Text("Project name", null, theme.SmallMedium, theme.TextMuted);
                ui.TextField("demo-name", ref _demoName, "Your name", style: new Style { Height = 36 });

                ui.Text("Email", null, theme.SmallMedium, theme.TextMuted);
                ui.TextField("demo-email", ref _demoEmail, "you@company.com", style: new Style { Height = 36 });

                ui.Text("Search", null, theme.SmallMedium, theme.TextMuted);
                ui.TextField("demo-search", ref _demoSearch, "Search the gallery…", style: new Style { Height = 36 });

                ui.Text("Plan", null, theme.SmallMedium, theme.TextMuted);
                ui.Dropdown("demo-plan", ["Personal", "Team", "Enterprise"], ref _demoPlan, style: new Style { Height = 36 });
            });
        }

        using (ui.Row("component-row-2", new Style
        {
            Direction = FlexDirection.Row,
            Gap = 16,
            AlignItems = AlignItems.Stretch,
        }))
        {
            ComponentCard(ui, theme, "component-selection", "Selection", "Binary choices, sliders and radio groups.", () =>
            {
                ui.Checkbox("Email notifications", ref _demoNotifications, "demo-check-notifications");
                ui.Checkbox("Public profile", ref _demoPublicProfile, "demo-check-public");
                ui.Toggle("Automatically save drafts", ref _demoAutoSave, "demo-toggle-save");
                ui.Slider("demo-volume", ref _demoVolume, 0, 100, 1, "Interface volume", value => $"{value:0}%", style: new Style { Height = 54 });
                ui.Divider();
                using (ui.Column("radio-group", new Style { Direction = FlexDirection.Column, Gap = 4 }))
                {
                    ui.Caption("Default visibility");
                    ui.RadioButton("Workspace", ref _demoChoice, 0, "demo-radio-workspace");
                    ui.RadioButton("Private", ref _demoChoice, 1, "demo-radio-private");
                    ui.RadioButton("Public", ref _demoChoice, 2, "demo-radio-public");
                }
            });

            ComponentCard(ui, theme, "component-feedback", "Feedback", "Communicate status and keep people oriented.", () =>
            {
                using (ui.Row("badge-row", new Style { Direction = FlexDirection.Row, Gap = 6, AlignItems = AlignItems.Center }))
                {
                    ui.Badge("Neutral", BadgeTone.Neutral);
                    ui.Badge("Accent", BadgeTone.Accent);
                    ui.Badge("Success", BadgeTone.Success);
                    ui.Badge("Warning", BadgeTone.Warning);
                    ui.Badge("Danger", BadgeTone.Danger);
                }

                ui.Caption("Storage");
                ui.ProgressBar(0.68f, 8, theme.Accent);
                ui.Caption("67.8 GB of 100 GB used");

                using (ui.Row("avatar-row", new Style { Direction = FlexDirection.Row, Gap = 4, AlignItems = AlignItems.Center }))
                {
                    ui.Avatar("AL", 34);
                    ui.Avatar("MK", 34, theme.Info.WithAlpha(0.24f));
                    ui.Avatar("SR", 34, theme.Success.WithAlpha(0.22f));
                    ui.Avatar("TN", 34, theme.Warning.WithAlpha(0.22f));
                }

                using (ui.Row("feedback-actions", new Style { Direction = FlexDirection.Row, Gap = 8, AlignItems = AlignItems.Center }))
                {
                    if (ui.Button("Show toast", "demo-toast", ButtonVariant.Secondary, Icons.Bell).Clicked)
                    {
                        ShowToast(ui, "A toast is a lightweight, non-blocking confirmation.");
                    }

                    if (ui.Button("Open modal", "demo-modal", ButtonVariant.Ghost, Icons.ExternalLink).Clicked)
                    {
                        _demoModalOpen = true;
                    }
                }
            });
        }

        using (ui.Card("component-typography", CardStyle(theme, Thickness.All(22))))
        {
            ui.Text("Typography", null, theme.BodyMedium, theme.Text);
            ui.Caption("A quiet, legible scale designed for dense product interfaces.");
            ui.Space(6);
            ui.Text("The quick brown fox jumps over the lazy dog.", null, theme.Display, theme.Text);
            ui.Text("Ship with confidence", null, theme.Title, theme.Text);
            ui.Text("Project overview", null, theme.Heading, theme.Text);
            ui.Text("Body copy explains the next step with just enough context.", null, theme.Body, theme.TextMuted);
            ui.Text("SECONDARY LABEL", null, theme.CaptionMedium, theme.TextSubtle);
            ui.Text("const frame = ui.EndFrame();", null, theme.Mono, theme.Info);
        }
    }

    private static void ComponentCard(Ui ui, Theme theme, string id, string title, string subtitle, Action content)
    {
        using (ui.Card(id, new Style
        {
            Direction = FlexDirection.Column,
            Grow = 1,
            Padding = Thickness.All(22),
            Gap = 12,
            Background = new Brush(theme.Surface),
            BorderColor = theme.Border,
            BorderWidth = 1,
            CornerRadius = CornerRadius.All(16),
            Shadow = theme.CardShadow,
        }))
        {
            ui.Text(title, null, theme.BodyMedium, theme.Text);
            ui.Caption(subtitle);
            content();
        }
    }

    private void BuildSettingsPage(Ui ui, Theme theme)
    {
        using (ui.Row("settings-row", new Style
        {
            Direction = FlexDirection.Row,
            Gap = 16,
            AlignItems = AlignItems.Start,
        }))
        {
            ComponentCard(ui, theme, "settings-appearance", "Appearance", "Make the workspace feel like yours.", () =>
            {
                ui.Text("Theme", null, theme.SmallMedium, theme.TextMuted);
                using (ui.Row("theme-options", new Style { Direction = FlexDirection.Row, Gap = 8 }))
                {
                    if (ui.Button("Dark", "settings-dark", _dark ? ButtonVariant.Primary : ButtonVariant.Secondary, Icons.Moon).Clicked)
                    {
                        _dark = true;
                        ui.Theme = CreateTheme();
                    }

                    if (ui.Button("Light", "settings-light", !_dark ? ButtonVariant.Primary : ButtonVariant.Secondary, Icons.Sun).Clicked)
                    {
                        _dark = false;
                        ui.Theme = CreateTheme();
                    }
                }

                ui.Text("Accent color", null, theme.SmallMedium, theme.TextMuted);
                BuildAccentSwatches(ui, theme);
                ui.Divider();
                ui.Toggle("Reduce motion", ref _demoAnalytics, "settings-motion");
                ui.Toggle("Show online status", ref _demoPublicProfile, "settings-online");
            });

            ComponentCard(ui, theme, "settings-preferences", "Workspace preferences", "Tune defaults for your team.", () =>
            {
                ui.Toggle("Email notifications", ref _demoNotifications, "settings-notifications");
                ui.Toggle("Automatically save drafts", ref _demoAutoSave, "settings-autosave");
                ui.Text("Default visibility", null, theme.SmallMedium, theme.TextMuted);
                ui.Dropdown("settings-visibility", ["Workspace", "Private", "Public"], ref _demoVisibility, style: new Style { Height = 36 });
                ui.Slider("settings-opacity", ref _demoOpacity, 0.2f, 1f, 0.01f, "UI opacity", value => $"{value * 100:0}%", style: new Style { Height = 54 });
                if (ui.Button("Reset to defaults", "settings-reset", ButtonVariant.Ghost, Icons.Refresh).Clicked)
                {
                    _demoOpacity = 0.82f;
                    _demoVisibility = 0;
                    ShowToast(ui, "Preferences reset.");
                }
            });
        }

        using (ui.Card("settings-danger", new Style
        {
            Direction = FlexDirection.Column,
            Padding = Thickness.All(22),
            Gap = 12,
            Background = new Brush(theme.DangerSoft),
            BorderColor = theme.Danger.WithAlpha(0.25f),
            BorderWidth = 1,
            CornerRadius = CornerRadius.All(16),
        }))
        {
            ui.Text("Danger zone", null, theme.BodyMedium, theme.Danger);
            ui.Caption("These actions affect the entire workspace and cannot be undone.");
            using (ui.Row("danger-actions", new Style { Direction = FlexDirection.Row, Gap = 8 }))
            {
                if (ui.Button("Transfer ownership", "settings-transfer", ButtonVariant.Danger).Clicked)
                {
                    ShowToast(ui, "Ownership transfer requires an admin.");
                }

                if (ui.Button("Delete workspace", "settings-delete", ButtonVariant.Ghost, style: new Style { Foreground = theme.Danger }).Clicked)
                {
                    ShowToast(ui, "This is only a demo — your workspace is safe.");
                }
            }
        }
    }

    private void BuildAccentSwatches(Ui ui, Theme theme)
    {
        using (ui.Row("accent-swatches", new Style { Direction = FlexDirection.Row, Gap = 10, AlignItems = AlignItems.Center }))
        {
            for (var i = 0; i < DemoData.Accents.Length; i++)
            {
                var index = i;
                var response = ui.Canvas($"accent-swatch-{i}", view =>
                {
                    var center = view.Bounds.Center;
                    var radius = MathF.Min(view.Bounds.Width, view.Bounds.Height) * 0.5f - 2;
                    view.DrawList.FillCircle(center, radius, new Brush(DemoData.Accents[index]));
                    if (view.Response.Hovered || index == _accentIndex)
                    {
                        view.DrawList.StrokeCircle(center, radius + 3, index == _accentIndex ? theme.Text : theme.TextMuted, index == _accentIndex ? 2 : 1);
                    }

                    if (index == _accentIndex)
                    {
                        view.DrawList.DrawIcon(Icons.Check, Rect.Centered(center, 13, 13), Color.White);
                    }
                }, new Style { Width = 34, Height = 34 }, interactive: true);
                if (response.Clicked)
                {
                    _accentIndex = index;
                    ui.Theme = CreateTheme();
                    ShowToast(ui, $"{DemoData.AccentNames[index]} accent applied.");
                }
            }
        }
    }

    private void BuildComponentModal(Ui ui, Theme theme)
    {
        if (!_demoModalOpen)
        {
            return;
        }

        ui.Modal("demo-invite", ref _demoModalOpen, "Invite a teammate", () =>
        {
            ui.Text("Send an invitation to collaborate in Acme Design.", null, theme.Body, theme.TextMuted);
            ui.Text("Email address", null, theme.SmallMedium, theme.TextMuted);
            ui.TextField("invite-email", ref _inviteEmail, "name@company.com", style: new Style { Height = 38 });
            ui.Caption("They will receive an email with a link to join this workspace.");
            using (ui.Row("invite-actions", new Style
            {
                Direction = FlexDirection.Row,
                Gap = 8,
                JustifyContent = JustifyContent.End,
                Margin = new Thickness(0, 8, 0, 0),
            }))
            {
                if (ui.Button("Cancel", "invite-cancel", ButtonVariant.Ghost).Clicked)
                {
                    _demoModalOpen = false;
                }

                if (ui.Button("Send invite", "invite-send", ButtonVariant.Primary, Icons.Send, disabled: string.IsNullOrWhiteSpace(_inviteEmail)).Clicked)
                {
                    _demoModalOpen = false;
                    ShowToast(ui, "Invitation sent.");
                }
            }
        }, width: 480);
    }
}



public sealed partial class DemoApp
{
    private void BuildDataLabPage(Ui ui, Theme theme)
    {
        using (ui.Grid("lab-stats", [GridTrack.Star(), GridTrack.Star(), GridTrack.Star(), GridTrack.Star()],
                   style: new Style { Height = 104, Gap = 12 }))
        {
            LabStat(ui, theme, "Visible rows", "only what fits", "5,000 total", Icons.List, theme.Accent);
            LabStat(ui, theme, "Rows allocated", "virtualized", "≈ 18 per frame", Icons.Cpu, theme.Success);
            LabStat(ui, theme, "Split pane", "draggable", $"{_labSplit:0} px", Icons.PanelLeft, theme.Info);
            LabStat(ui, theme, "Text engine", "multiline + IME", "HarfBuzz shaping", Icons.File, theme.Warning);
        }

        using (ui.Row("lab-main-row", new Style { Height = 460, Gap = 16, Direction = FlexDirection.Row }))
        {
            ui.SplitPane("lab-split", Axis.Horizontal, ref _labSplit,
                () =>
                {
                    using (ui.Panel("lab-tree-panel", LabCardStyle(theme)))
                    {
                        using (ui.Row("lab-tree-head", new Style { AlignItems = AlignItems.Center, Gap = 8 }))
                        {
                            ui.Text("Workspace tree", new Style { Grow = 1 }, theme.SmallMedium, theme.Text);
                            ui.Badge("virtual", BadgeTone.Accent);
                        }

                        var treeRows = BuildLabTreeRows();
                        var result = ui.TreeView("lab-tree", treeRows, ref _labSelectedTree, new Style { Grow = 1 });
                        if (result.ToggleIndex >= 0 && result.ToggleIndex < treeRows.Count)
                        {
                            var tag = treeRows[result.ToggleIndex].Tag;
                            if (!string.IsNullOrEmpty(tag))
                            {
                                if (!_labExpanded.Add(tag))
                                {
                                    _labExpanded.Remove(tag);
                                }
                            }
                        }
                    }
                },
                () =>
                {
                    using (ui.Panel("lab-table-panel", LabCardStyle(theme)))
                    {
                        using (ui.Row("lab-table-head", new Style { AlignItems = AlignItems.Center, Gap = 8 }))
                        {
                            ui.Text("Projects", new Style { Grow = 1 }, theme.SmallMedium, theme.Text);
                            ui.Caption($"{_labRows.Count:N0} rows · selected #{_labSelectedRow + 1}");
                            if (ui.Button("Import files", "lab-import", ButtonVariant.Ghost, Icons.Upload).Clicked)
                            {
                                var files = Win32FileDialog.OpenFiles(
                                    default,
                                    "Import into Data Lab",
                                    "All files|*.*|Images|*.png;*.jpg;*.jpeg|Text|*.txt;*.md");
                                if (files.Count > 0)
                                {
                                    ShowToast(ui, $"Imported {files.Count} file(s).");
                                    _labSelectedRow = 0;
                                }
                            }
                        }

                        ui.DataTable(
                            "lab-table",
                            _labRows,
                            LabColumns,
                            ref _labSelectedRow,
                            rowHeight: 34,
                            headerHeight: 34,
                            style: new Style
                            {
                                Direction = FlexDirection.Column,
                                Grow = 1,
                                Background = new Brush(theme.Surface),
                                BorderColor = theme.Border,
                                BorderWidth = 1,
                                CornerRadius = CornerRadius.All(theme.RadiusMedium),
                                ClipContent = true,
                            });
                    }
                },
                minFirst: 240,
                minSecond: 440,
                style: new Style { Grow = 1, Height = 460 });
        }

        using (ui.Panel("lab-notes", LabCardStyle(theme)))
        {
            using (ui.Row("lab-notes-head", new Style { AlignItems = AlignItems.Center, Gap = 8 }))
            {
                ui.Text("Notes", new Style { Grow = 1 }, theme.SmallMedium, theme.Text);
                ui.Caption("multiline TextArea · selection · undo · clipboard");
            }

            ui.TextArea("lab-notes-text", ref _labNotes, "Write a note…", style: new Style { Height = 132 });
        }
    }

    private static readonly TableColumn<LabRow>[] LabColumns =
    [
        new("ID", 64, row => $"#{row.Id:0000}"),
        new("Project", 240, row => row.Name),
        new("Owner", 130, row => row.Owner),
        new("Status", 112, row => row.Status),
        new("Progress", 90, row => $"{row.Progress}%", row => row.Progress >= 80 ? Color.FromHex("#34C6A0") : Color.FromHex("#8E93A8")),
    ];

    private static Style LabCardStyle(Theme theme) => new()
    {
        Direction = FlexDirection.Column,
        Grow = 1,
        Padding = Thickness.All(14),
        Gap = 8,
        Background = new Brush(theme.Surface),
        BorderColor = theme.Border,
        BorderWidth = 1,
        CornerRadius = CornerRadius.All(theme.RadiusLarge),
        ClipContent = true,
    };

    private static void LabStat(Ui ui, Theme theme, string label, string detail, string value, IconData icon, Color accent)
    {
        using (ui.Panel(null, new Style
        {
            Direction = FlexDirection.Column,
            Padding = Thickness.All(14),
            Gap = 4,
            Background = new Brush(theme.Surface),
            BorderColor = theme.Border,
            BorderWidth = 1,
            CornerRadius = CornerRadius.All(theme.RadiusLarge),
        }))
        {
            using (ui.Row("lab-stat-head", new Style { AlignItems = AlignItems.Center, Gap = 8 }))
            {
                ui.Icon(icon, 16, accent);
                ui.Caption(label, new Style { Grow = 1 });
            }

            ui.Text(value, null, theme.Heading, theme.Text);
            ui.Caption(detail);
        }
    }

    private List<TreeRow> BuildLabTreeRows()
    {
        var rows = new List<TreeRow>();
        void Add(string tag, string label, int depth, bool children, IconData? icon = null)
        {
            var expanded = _labExpanded.Contains(tag);
            rows.Add(new TreeRow(label, depth, children, expanded, icon, tag));
            if (!children || !expanded)
            {
                return;
            }

            switch (tag)
            {
                case "workspace":
                    Add("design", "Design system", 1, true, Icons.Palette);
                    Add("engineering", "Engineering", 1, true, Icons.Code);
                    Add("docs", "Documentation", 1, false, Icons.File);
                    break;
                case "design":
                    Add("aurora", "Aurora", 2, false, Icons.Layers);
                    Add("ember", "Ember", 2, false, Icons.Heart);
                    break;
                case "engineering":
                    Add("atlas", "Atlas Mobile", 2, false, Icons.Folder);
                    Add("pulse", "Pulse Analytics", 2, false, Icons.BarChart);
                    break;
            }
        }

        Add("workspace", "Acme Design", 0, true, Icons.Database);
        return rows;
    }
}

