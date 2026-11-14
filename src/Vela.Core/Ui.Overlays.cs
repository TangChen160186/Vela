namespace Vela;

public sealed partial class Ui
{
    private const int PopupLayer = 100;
    private const int TooltipLayer = 300;
    private readonly List<string> _popupStack = new(4);

    private static Id PopupKey(string id) => Id.From("vela.popup:" + id);

    public bool IsPopupOpen(string id) => State.Get<PopupState>(PopupKey(id))?.IsOpen == true;

    /// <summary>
    /// 打开一个锚定到某个元素的 popup。首次打开时记录当前焦点，
    /// 关闭时由 ClosePopup 恢复；重复打开不会覆盖已保存的焦点。
    /// </summary>
    public void OpenPopup(string id, Id anchor)
    {
        var state = State.GetOrCreate<PopupState>(PopupKey(id));
        if (!state.IsOpen)
        {
            state.PreviousFocus = _interaction.FocusedId;
        }

        state.IsOpen = true;
        state.AnchorId = anchor.Value;
        state.AtPoint = false;
        state.OpenedAt = Input.Time;
    }

    /// <summary>在屏幕坐标打开 popup（例如右键菜单），没有锚点元素。</summary>
    public void OpenPopupAt(string id, Vec2 position)
    {
        var state = State.GetOrCreate<PopupState>(PopupKey(id));
        if (!state.IsOpen)
        {
            state.PreviousFocus = _interaction.FocusedId;
        }

        state.IsOpen = true;
        state.AnchorId = 0;
        state.AtPoint = true;
        state.Point = position;
        state.OpenedAt = Input.Time;
    }

    public bool TogglePopup(string id, Id anchor)
    {
        if (IsPopupOpen(id))
        {
            ClosePopup(id);
            return false;
        }

        OpenPopup(id, anchor);
        return true;
    }

    /// <summary>
    /// 关闭 popup 并恢复打开前的焦点。所有关闭路径（Escape/外部点击/MenuItem）
    /// 都应走这里，确保焦点和状态一致。
    /// </summary>
    public void ClosePopup(string id)
    {
        var state = State.Get<PopupState>(PopupKey(id));
        if (state is not null && state.IsOpen)
        {
            state.IsOpen = false;
            if (state.PreviousFocus != 0)
            {
                Focus(new Id(state.PreviousFocus), keyboardVisible: true);
                state.PreviousFocus = 0;
            }
        }
    }

    /// <summary>
    /// 渲染命名 popup（仅在打开时）。结构：
    /// 全屏 backdrop（处理外部点击）→ 锚定面板（TrapFocus）→ 内容。
    ///
    /// 必须在可见期间每帧调用；打开/关闭状态在 PopupState，锚点在上一帧快照中。
    /// </summary>
    public bool Popup(
        string id,
        Action build,
        float width = 260,
        bool closeOnOutside = true,
        Style? style = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        var state = State.Get<PopupState>(PopupKey(id));
        if (state is null || !state.IsOpen)
        {
            return false;
        }

        var frame = RequireFrame();
        if (Input.WasKeyPressed(Key.Escape))
        {
            state.IsOpen = false;
            return false;
        }

        var backdrop = frame.AddOverlay(
            id + ":backdrop",
            new Style { Direction = FlexDirection.Column },
            "popup-backdrop",
            new OverlayPlacement { Alignment = OverlayAlignment.FullScreen },
            passThrough: false,
            trapFocus: false,
            layer: PopupLayer);
        backdrop.Interactive = true;
        backdrop.Cursor = CursorKind.Default;
        frame.UpdateResponse(backdrop);
        if (closeOnOutside && backdrop.Response.Clicked)
        {
            ClosePopup(id);
            return false;
        }

        var placement = new OverlayPlacement
        {
            Alignment = state.AtPoint ? OverlayAlignment.AtPoint : OverlayAlignment.AnchorBottomStart,
            AnchorId = state.AnchorId,
            Point = state.Point,
            Margin = Thickness.All(6),
            MaxWidth = MathF.Max(120, Input.ViewportSize.Width - 12),
            MaxHeight = MathF.Max(120, Input.ViewportSize.Height - 12),
        };

        var panelStyle = style ?? new Style
        {
            Direction = FlexDirection.Column,
            Padding = Thickness.All(6),
            Background = new Brush(Theme.SurfaceRaised),
            BorderColor = Theme.Border,
            BorderWidth = 1,
            CornerRadius = CornerRadius.All(Theme.RadiusLarge),
            Shadow = Theme.PopupShadow,
        };

        if (panelStyle.Width.IsAuto && width > 0)
        {
            panelStyle = panelStyle with { Width = Dimension.Px(width) };
        }

        var panel = frame.AddOverlay(
            id + ":panel",
            panelStyle,
            "popup",
            placement,
            passThrough: false,
            trapFocus: true,
            layer: PopupLayer);

        _popupStack.Add(id);
        frame.PushScope(panel);
        try
        {
            build();
        }
        finally
        {
            frame.EndScope();
            _popupStack.RemoveAt(_popupStack.Count - 1);
        }

        return state.IsOpen;
    }

    /// <summary>A popup intended to contain <see cref="MenuItem"/>s.</summary>
    public bool Menu(string id, Action build, float width = 220, bool closeOnOutside = true) =>
        Popup(id, build, width, closeOnOutside);

    public Response MenuItem(
        string label,
        string? id = null,
        IconData? icon = null,
        string? shortcut = null,
        bool danger = false,
        bool disabled = false,
        Style? style = null)
    {
        var frame = RequireFrame();
        var element = frame.AddElement(
            MenuItemWidget.Instance,
            id ?? label,
            style,
            "menu-item",
            interactive: true,
            focusable: true,
            activatable: true);
        element.Text = label;
        element.AccessibilityRole = AccessibilityRole.MenuItem;
        element.AccessibilityName = label;
        element.AccessibilityHelp = shortcut;
        element.Icon = icon;
        element.Payload = new MenuItemPayload(shortcut, danger);
        element.Font = style?.Font ?? Theme.Body;
        element.TextFormat = new TextFormat(element.Font, TextAlignment.Left, VerticalAlignment.Center, TextWrapMode.NoWrap, TextOverflowMode.Ellipsis);
        element.Cursor = disabled ? CursorKind.Default : CursorKind.Hand;
        frame.UpdateResponse(element, disabled);
        var response = element.Response;
        if (!disabled && response.Clicked && _popupStack.Count > 0)
        {
            ClosePopup(_popupStack[^1]);
        }

        return response;
    }

    public Response Dropdown(
        string id,
        IReadOnlyList<string> items,
        ref int selectedIndex,
        bool disabled = false,
        Style? style = null)
    {
        var frame = RequireFrame();
        var resolvedWidth = style is not null && style.Width.Kind == DimensionKind.Points ? style.Width.Value : 220f;
        var element = frame.AddElement(
            DropdownWidget.Instance,
            id,
            style,
            "dropdown",
            interactive: true,
            focusable: true,
            activatable: true);
        element.AccessibilityRole = AccessibilityRole.ComboBox;
        element.AccessibilityName = id;
        element.Font = style?.Font ?? Theme.Body;
        element.TextFormat = new TextFormat(element.Font, TextAlignment.Left, VerticalAlignment.Center, TextWrapMode.NoWrap, TextOverflowMode.Ellipsis);
        element.Cursor = disabled ? CursorKind.Default : CursorKind.Hand;

        if (items.Count == 0)
        {
            selectedIndex = -1;
        }
        else
        {
            selectedIndex = Math.Clamp(selectedIndex, 0, items.Count - 1);
        }

        var popupId = id + ":popup";
        frame.UpdateResponse(element, disabled || items.Count == 0);
        var response = element.Response;

        if (!disabled && items.Count > 0 && response.Clicked)
        {
            OpenPopup(popupId, response.Id);
        }

        var pendingSelection = selectedIndex;
        Popup(popupId, () =>
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (MenuItem(items[i], id: $"{id}:item:{i}").Clicked)
                {
                    pendingSelection = i;
                }
            }
        }, width: resolvedWidth);

        if (pendingSelection != selectedIndex)
        {
            selectedIndex = pendingSelection;
        }

        var selectedText = selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : "Select…";
        element.AccessibilityValue = selectedText;
        element.Payload = new DropdownPayload(selectedText, IsPopupOpen(popupId));
        return response;
    }

    /// <summary>
    /// 渲染模态对话框；open 由应用持有，Vela 不替应用保存这个 bool。
    /// 打开时记录焦点并 TrapFocus，关闭时恢复；Escape/背景点击/关闭按钮都走同一路径。
    /// </summary>
    public void Modal(
        string id,
        ref bool open,
        string? title,
        Action build,
        float width = 520,
        bool closeOnBackdrop = true,
        Style? bodyStyle = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        var focusState = State.GetOrCreate<OverlayFocusState>(Id.From("vela.modal-focus:" + id));
        if (!open)
        {
            if (focusState.WasOpen)
            {
                focusState.WasOpen = false;
                if (focusState.PreviousFocus != 0)
                {
                    Focus(new Id(focusState.PreviousFocus), keyboardVisible: true);
                    focusState.PreviousFocus = 0;
                }
            }

            return;
        }

        if (!focusState.WasOpen)
        {
            focusState.WasOpen = true;
            focusState.PreviousFocus = _interaction.FocusedId;
        }

        var frame = RequireFrame();
        var backdrop = frame.AddOverlay(
            id + ":backdrop",
            new Style
            {
                Direction = FlexDirection.Column,
                Background = new Brush(Theme.Overlay),
            },
            "modal-backdrop",
            new OverlayPlacement { Alignment = OverlayAlignment.FullScreen },
            passThrough: false,
            trapFocus: false,
            layer: PopupLayer);
        backdrop.Interactive = true;
        frame.UpdateResponse(backdrop);

        if ((closeOnBackdrop && backdrop.Response.Clicked) || Input.WasKeyPressed(Key.Escape))
        {
            open = false;
            focusState.WasOpen = false;
            if (focusState.PreviousFocus != 0)
            {
                Focus(new Id(focusState.PreviousFocus), keyboardVisible: true);
                focusState.PreviousFocus = 0;
            }

            return;
        }

        var placement = new OverlayPlacement
        {
            Alignment = OverlayAlignment.Center,
            Margin = Thickness.All(24),
            MaxWidth = MathF.Max(240, Input.ViewportSize.Width - 48),
            MaxHeight = MathF.Max(200, Input.ViewportSize.Height - 48),
        };

        var panelStyle = new Style
        {
            Direction = FlexDirection.Column,
            Width = width,
            Background = new Brush(Theme.SurfaceRaised),
            BorderColor = Theme.Border,
            BorderWidth = 1,
            CornerRadius = CornerRadius.All(Theme.RadiusExtraLarge),
            Shadow = Theme.PopupShadow,
            ClipContent = true,
        };

        var panel = frame.AddOverlay(id + ":panel", panelStyle, "modal", placement, passThrough: false, trapFocus: true, layer: PopupLayer);
        panel.AccessibilityRole = AccessibilityRole.Dialog;
        panel.AccessibilityName = title ?? id;
        frame.PushScope(panel);
        try
        {
            if (!string.IsNullOrEmpty(title))
            {
                using (Row($"{id}:header", new Style
                {
                    Height = 62,
                    Padding = new Thickness(22, 0, 14, 0),
                    AlignItems = AlignItems.Center,
                    Gap = 8,
                }))
                {
                    Text(title, new Style { Grow = 1 }, Theme.Heading);
                    if (IconButton(Icons.X, "Close", $"{id}:close", ButtonVariant.Ghost, style: new Style
                        {
                            Width = 32,
                            Height = 32,
                            Padding = Thickness.Zero,
                        }).Clicked)
                    {
                        open = false;
                    }
                }

                Divider();
            }

            using (Panel($"{id}:body", bodyStyle ?? new Style
            {
                Direction = FlexDirection.Column,
                Padding = Thickness.All(22),
                Gap = 12,
            }))
            {
                build();
            }
        }
        finally
        {
            frame.EndScope();
        }

        if (!open && focusState.WasOpen)
        {
            focusState.WasOpen = false;
            if (focusState.PreviousFocus != 0)
            {
                Focus(new Id(focusState.PreviousFocus), keyboardVisible: true);
                focusState.PreviousFocus = 0;
            }
        }
    }

    /// <summary>Shows a non-blocking floating message at the bottom-right of the viewport.</summary>
    public void Toast(string message, IconData? icon = null, BadgeTone tone = BadgeTone.Success)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        var frame = RequireFrame();
        var color = tone switch
        {
            BadgeTone.Danger => Theme.Danger,
            BadgeTone.Warning => Theme.Warning,
            BadgeTone.Accent => Theme.Accent,
            _ => Theme.Success,
        };

        var panel = frame.AddOverlay(
            "toast:" + message,
            new Style
            {
                Direction = FlexDirection.Row,
                Padding = new Thickness(14, 11, 16, 11),
                Background = new Brush(Theme.TooltipBackground),
                CornerRadius = CornerRadius.All(Theme.RadiusLarge),
                Shadow = Theme.PopupShadow,
                AlignItems = AlignItems.Center,
                Gap = 10,
                MaxWidth = 420,
            },
            "toast",
            new OverlayPlacement
            {
                Alignment = OverlayAlignment.BottomEnd,
                Margin = Thickness.All(24),
                MaxWidth = 440,
            },
            passThrough: true,
            trapFocus: false,
            layer: 400);
        panel.Foreground = Theme.TooltipText;
        frame.PushScope(panel);
        try
        {
            if (icon is not null)
            {
                Icon(icon, 18, color);
            }

            Text(message, new Style { MaxWidth = 340 }, Theme.Small, Theme.TooltipText);
        }
        finally
        {
            frame.EndScope();
        }
    }

    public void Tooltip(string text, in Response target, float delay = 0.45f)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var state = State.GetOrCreate<TooltipState>(Id.Combine(target.Id, "tooltip"));
        if (!target.Hovered || target.Disabled || Input.PointerInside == false)
        {
            state.Visible = false;
            state.HoverStarted = 0;
            return;
        }

        if (state.HoverStarted <= 0)
        {
            state.HoverStarted = Input.Time;
        }

        if (Input.Time - state.HoverStarted < delay)
        {
            state.Visible = false;
            return;
        }

        state.Visible = true;
        var frame = RequireFrame();
        var placement = new OverlayPlacement
        {
            Alignment = OverlayAlignment.AnchorTopStart,
            AnchorId = target.Id.Value,
            Margin = Thickness.All(8),
            MaxWidth = 300,
        };

        var panel = frame.AddOverlay(
            target.Id.Value + ":tooltip",
            new Style
            {
                Direction = FlexDirection.Column,
                Padding = new Thickness(9, 6, 9, 6),
                Background = new Brush(Theme.TooltipBackground),
                CornerRadius = CornerRadius.All(Theme.RadiusSmall),
                Shadow = Theme.PopupShadow,
            },
            "tooltip",
            placement,
            passThrough: true,
            trapFocus: false,
            layer: TooltipLayer);
        panel.Foreground = Theme.TooltipText;
        frame.PushScope(panel);
        try
        {
            Text(text, new Style { MaxWidth = 280 }, Theme.Small, Theme.TooltipText);
        }
        finally
        {
            frame.EndScope();
        }
    }
}



