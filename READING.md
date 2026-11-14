# Vela 源码阅读与项目理解指南

> 面向第一次接触 Vela 的程序员。目标不是背 API，而是建立一张可以自己继续探索的地图：知道一个 UI 操作如何变成像素，知道该改哪个文件，知道哪些限制是设计选择。
>
> Core 运行时、布局、交互、文本、渲染、Win32/SDL 宿主和 UIA 的关键类都已经加入中文 XML/流程注释；本文负责给出阅读顺序。

仓库入口见 [README.md](README.md)。本文只保留项目阅读所需内容，不再拆分多份架构文档。

---

## 先看这里：第一遍只读 6 个文件

如果你现在就要开始读代码，不要从控件目录或 Skia 开始，按下面顺序：

1. `src/Vela.Core/Runtime/Widget.cs`
2. `src/Vela.Core/Runtime/Element.cs`
3. `src/Vela.Core/Ui.cs`
4. `src/Vela.Core/Runtime/Frame.cs`
5. `src/Vela.Core/Runtime/Frame.Rendering.cs`
6. `src/Vela.Desktop/DesktopApp.cs`

第一遍只看主路径：

- `Widget` 的四个生命周期；
- `Element` 的四类字段；
- `Ui.BeginFrame/EndFrame`；
- `Frame.AddElement/Complete`；
- `PerformLayout/PaintFrame`；
- `DesktopApp.Run` 的循环。

**先不要看**：`SkiaTextEngine` 的字体回退、`Win32UiaProvider` 的 COM、`Sdl3Window` 的事件 switch、`DemoApp.Pages.cs` 的所有页面、`GridWidget` 的 Auto/Star 细节。

读完这 6 个文件，应该能画出：

```text
app.Run
  → DesktopApp loop
    → Ui.BeginFrame
      → Frame / AddElement
        → build(ui)
      → Frame.Complete
        → Layout → Snapshot → Paint → Accessibility
    → Ui.EndFrame
    → Skia → Window
```

画不出来就回到 `Frame.cs` 和 `DesktopApp.cs`，不要继续读控件细节。

---
## 0. 阅读前需要知道什么

### 需要的基础

- C# 10+/.NET 基础：record、pattern matching、`using`、`ref`、span、partial class；
- 对 UI 框架的基本认识：布局、绘制、输入、焦点、滚动；
- 了解 Skia 或任意 2D 绘制 API 会更容易，但不是必需；
- 不需要 XAML、WPF、Avalonia 或游戏引擎经验。

### 不需要先知道的

- 不需要先懂 HarfBuzz、UIA、SDL3、GDI；
- 不需要先读完全部控件；
- 不需要先理解所有平台细节。

先理解 **Vela 的核心循环**，平台和高级能力都是围绕它加的外围。

---

## 1. 这个项目到底是什么

Vela 是一个 **即时模式 UI 框架**：

```text
UI = f(application state, input)
```

每一帧都重新声明界面。没有持久控件对象，也没有 ViewModel 和绑定。

但它不是简单 debug overlay：

- 有完整的临时元素树；
- 有真正的约束布局；
- 有稳定 ID 和跨帧状态；
- 有焦点、文本编辑、滚动、浮层；
- 输出与渲染器无关的 DrawList；
- 有 CPU/GPU 后端、可访问性和桌面宿主。

核心设计取舍：

| 选择 | 收益 | 代价 |
|---|---|---|
| 每帧重建元素树 | 没有 UI 状态同步问题 | 每帧有构建和布局成本 |
| 用临时元素而不是持久控件 | 写法简单、组合自然 | 不能持有控件对象、不能用传统控件事件 |
| 用上一帧快照命中 | 点击与屏幕几何一致 | 新元素有一帧交互延迟 |
| 绘制输出 DrawList | 渲染器可替换 | 需要维护命令协议 |
| 应用持有业务状态 | 框架不侵入应用 | 需要自己管理 `ref` 和 ID |

---

## 2. 跑起来并观察它工作

### 2.1 最短运行路径

```powershell
dotnet run --project src/Vela.Demo
```

Vela Studio 会打开。切换页面、拖动滚动条、悬停按钮、按 Tab、打开命令面板（Ctrl+K），这些都在走同一套核心循环。

无窗口截图：

```powershell
dotnet run --project src/Vela.Demo -- \
  --snapshot artifacts/check.png \
  --page data-lab \
  --width 1440 --height 1100
```

这个命令非常适合阅读代码时验证布局：不依赖窗口、DPI 和显卡。

### 2.2 最小应用

```csharp
using Vela;
using Vela.Desktop;

using var app = new DesktopApp(new DesktopAppOptions
{
    Title = "Hello",
    Width = 900,
    Height = 620,
    Theme = Theme.Dark(),
});

app.Run(ui =>
{
    using (ui.Column("app", new Style
    {
        Padding = Thickness.All(32),
        Gap = 12,
    }))
    {
        ui.Heading("Hello");
        ui.Button("Continue", "continue", ButtonVariant.Primary);
    }
});
```

`app.Run` 每帧调用这个 lambda。lambda 里做的是“描述”，不是“创建持久对象”。

### 2.3 跑测试

```powershell
dotnet test Vela.slnx
```

测试用 `UiTestHost` 和 `FakeTextEngine`，不依赖真实窗口或本机字体。这是理解框架行为最快的方式之一。

---

## 3. 先记住三条规则

### 规则一：每一帧从零声明

```csharp
app.Run(ui =>
{
    using (ui.Row("toolbar", new Style { Gap = 8 }))
    {
        ui.Button("Save", "save");
        ui.Button("Cancel", "cancel");
    }
});
```

- `ui.Row` 创建的是本帧的元素；
- lambda 结束后没有控件对象可保留；
- 所有业务状态放在 lambda 外部。

### 规则二：容器必须 `using`

```csharp
using (ui.Column("page", new Style { Gap = 10 }))
{
    ui.Text("A");
    ui.Text("B");
}
```

`UiScope.Dispose` 会结束当前作用域。忘记 `using`，后面的控件会挂到错误的父级，甚至触发 ID/作用域错误。这不是 C# 风格问题，而是树结构协议。

### 规则三：重复控件必须有稳定 ID

```csharp
ui.Button("Delete", "delete-project");
```

`Button` 默认用 label 作为 ID，所以：

- label 改变时 ID 改变；
- 两个同名按钮会冲突；
- 列表循环必须显式 ID 或 `PushId`。

```csharp
ui.PushId("project-list");
for (var i = 0; i < projects.Count; i++)
{
    ui.PushId(projects[i].Id);
    ui.Button("Open", "open");
    ui.PopId();
}
ui.PopId();
```

稳定 ID 是滚动位置、文本选区、焦点和动画能跨帧存在的前提。

---

## 4. 一次完整帧

核心入口：

```text
src/Vela.Core/Ui.cs
src/Vela.Core/Runtime/Frame.cs
src/Vela.Core/Runtime/Frame.Rendering.cs
```

### 4.1 应用循环

`DesktopApp.Run` 做：

1. 创建 `Win32Window`；
2. 每帧 `InputBuilder.BeginFrame`；
3. `window.PumpMessages()` 泵原生事件；
4. `_input.Build(viewport)` 生成不可变 `FrameInput`；
5. `_ui.BeginFrame(frameInput)`；
6. 调用应用的 `build(_ui)`；
7. `_ui.EndFrame()` 得到 `UiFrameResult`；
8. 渲染 DrawList 到像素；
9. 呈现并处理 damage/cursor；
10. 帧率 pacing。

`Vela.Desktop.Portable/PortableApp.cs` 是同样的循环，只是窗口和呈现换成 SDL3。

### 4.2 `Ui.BeginFrame`

`Ui.BeginFrame`：

- 检查是否重复开帧；
- 保存 `Input` 和 `ViewportSize`；
- 通知 `StateStore` 新帧；
- 调用 `InteractionManager.BeginFrame`，基于上一帧快照解析命中、焦点、滚动、键盘；
- 创建新的 `Frame`；
- 清空 `DrawList`。

注意：交互在 **构建之前** 解析，因为控件的 `Response` 需要在本帧构建时就能拿到。

### 4.3 `Frame` 的职责

`Frame` 是内部类，管理：

- 当前元素树；
- 当前 ID 作用域；
- 所有元素列表；
- 浮层列表；
- 布局/绘制/快照；
- 自动 ID 和重复 ID 检查；
- 最终结果。

关键方法：

| 方法 | 作用 |
|---|---|
| `AddElement` | 创建元素，连接父级，分配 ID，读取 Response |
| `BeginContainer` | 创建容器并压入作用域 |
| `PushScope` / `EndScope` | 容器作用域栈 |
| `UpdateResponse` | 用交互结果刷新本帧 Response |
| `Complete` | 收尾构建 + 布局 + 快照 + 绘制 + 可访问性 |
| `BuildSnapshot` | 生成下一帧可用的几何快照 |
| `PaintFrame` | 遍历元素树输出 DrawList |

### 4.4 `Frame.Complete`

顺序很重要：

```text
CompleteBuild
  → 关闭未结束的作用域，检查构建完整性
PerformLayout
  → root 布局 + 浮层布局
BuildSnapshot
  → 几何/裁剪/焦点/滚动信息
PaintFrame
  → 背景、内容、子级、浮层写入 DrawList
BuildAccessibility
  → 语义树
返回 UiFrameResult
```

`Ui.EndFrame` 随后保存 `_previousSnapshot`，清空当前 `Frame`。

### 4.5 `UiFrameResult`

```csharp
public sealed class UiFrameResult
{
    public required DrawList DrawList { get; init; }
    public required CursorKind Cursor { get; init; }
    public required FrameDiagnostics? Diagnostics { get; init; }
    public required AccessibilitySnapshot Accessibility { get; init; }
    public required long FrameNumber { get; init; }
    public required double Time { get; init; }
}
```

宿主只需要消费这个结果，不需要知道 Vela 的控件。

---

### 4.6 详细阅读路线：把这一帧落到文件

#### 第一遍（45–60 分钟）

按下面顺序，每读一个文件只回答表中问题：

| 文件 | 只看这些成员 | 读完后回答 |
|---|---|---|
| `Runtime/Widget.cs` | `Widget`、`LayoutContext.Measure/Arrange`、`PaintContext` | 控件有哪四个生命周期？为什么 Measure 和 Arrange 分开？ |
| `Runtime/Element.cs` | 字段分组 | 哪些数据本帧有效？哪些必须进 StateStore？ |
| `Ui.cs` | `BeginFrame`、`EndFrame`、`RequireFrame` | 为什么 UI 方法只能在帧内调用？ |
| `Runtime/Frame.cs` | 字段、`AddElement`、`BeginContainer`、`EndScope`、`Complete` | ID 怎样由父级派生？容器作用域如何维护？ |
| `Runtime/Frame.Rendering.cs` | `PerformLayout`、`PaintFrame`、`ArrangeOverlay` | 普通树和浮层分别怎样布局/绘制？ |
| `DesktopApp.cs` | `Run` | 一帧从输入到 Present 的准确顺序是什么？ |

#### 第二遍（60–90 分钟）：追一个按钮

1. `Ui.Controls.cs` → `Button`
2. `Frame.cs` → `AddElement`
3. `ButtonWidget.cs` → `Measure`、`PaintContent`
4. `InteractionFrame.cs` → `Clicked` 的产生位置
5. `Frame.Rendering.cs` → `PaintElement`
6. `SkiaRenderer.cs` → `DrawCommands`

要能回答：

- `Button` 的 ID 从哪里来？label 变化会怎样？
- `Clicked` 是按下还是释放产生？为什么新按钮第一帧点不到？
- `PaintContent` 输出了哪些 DrawCommand？
- hover 动画状态存在哪里？

#### 第三遍（按主题）

- **布局**：`Constraints` → `Style` → `Dimension` → `FlexWidget` → `GridWidget` → `WrapWidget` → `DockWidget` → `Ui.LayoutExtras.cs`/`SplitterWidget`
- **交互**：`Response` → `InteractionFrame.cs` → `ScrollState` → `ScrollWidget` → `Ui.Overlays.cs`
- **文本**：`ITextEngine` → `TextFormat` → `TextFieldWidget` → `TextBlockLayout` → `TextAreaWidget` → `SkiaTextEngine`
- **渲染**：`DrawCommand` → `Frame.Rendering.cs` → `SkiaRenderer` → `DrawDamageTracker` → `SkiaGpuRenderer`
- **平台**：`DesktopApp` → `Win32Window` → `PortableApp` → `Sdl3Window`
- **无障碍**：`Accessibility.cs` → `Ui.Accessibility.cs` → `Win32UiaProvider.cs`

#### 按任务查文件

| 你想做的事 | 先打开 |
|---|---|
| 加控件 | `Ui.Controls.cs`、`Widgets/ButtonWidget.cs` |
| 改布局 | `Widgets/FlexWidget.cs`、`Ui.LayoutExtras.cs` |
| 改命中/焦点 | `Interaction/InteractionFrame.cs` |
| 改滚动 | `Widgets/ScrollWidget.cs`、`Interaction/ScrollState.cs` |
| 改文本 | `Widgets/TextFieldWidget.cs`、`Widgets/TextAreaWidget.cs` |
| 改多行布局 | `Text/TextBlockLayout.cs` |
| 改浮层 | `Ui.Overlays.cs`、`Frame.Rendering.cs` 的 `ArrangeOverlay` |
| 改渲染 | `Rendering/DrawCommand.cs`、`SkiaRenderer.cs` |
| 加平台 | `DesktopApp.cs`、`Sdl3Window.cs` |
| 加 UIA | `Win32UiaProvider.cs`、`Accessibility.cs` |

#### 阅读检查点

每完成一遍，用 `UiTestHost` 写一个小测试验证理解：

```csharp
using var host = new UiTestHost(new Size(400, 200));
Id id = default;
host.Frame(ui =>
{
    using (ui.Row("row", new Style { Gap = 8 }))
    {
        ui.Button("A", "a");
        id = ui.Button("B", "b").Id;
    }
});

Assert.Equal(..., host.Bounds(id));
```

不要只“看懂”而不验证；Vela 的布局/交互是可以用固定 viewport 精确断言的。
## 5. 核心数据模型

### 5.1 `Widget`：行为

```csharp
internal abstract class Widget
{
    public virtual bool Interactive => false;
    public virtual bool Focusable => false;
    public virtual bool Activatable => false;

    public abstract Size Measure(Element element, LayoutContext context, Constraints constraints);
    public virtual void Arrange(Element element, LayoutContext context, Rect bounds) { }
    public virtual void PaintContent(Element element, PaintContext context) { }
    public virtual void PaintOverlay(Element element, PaintContext context) { }
}
```

`Widget` 是 **无状态单例**：例如 `ButtonWidget.Instance` 被所有按钮共享。每个控件的差异放在 `Element` 的字段里。

### 5.2 `Element`：一帧的数据

`Element` 包含：

- `Widget`、`Id`、`Style`、`Parent`、`Children`；
- `Text`、`Icon`、`Payload`；
- `Interactive`、`Focusable`、`Activatable`、`Disabled`；
- `IsScrollable`、`TextInput`、`HandlesArrowKeys`；
- `AccessibilityRole/Name/Value/Help`；
- `Bounds`、`ContentBounds`、`MeasuredSize`；
- `Response`；
- 滚动偏移和滚动轨道；
- 浮层标记。

它是“本帧的实例”，不是跨帧对象。跨帧数据在 `StateStore`。

### 5.3 `StateStore`：跨帧状态

```csharp
var state = State.GetOrCreate<ScrollState>(id);
```

键是 `(Id, typeof(T))`。因此：

```csharp
State.GetOrCreate<ScrollState>(id);
State.GetOrCreate<TextAreaState>(id);
```

可以共存，不会互相覆盖。

### 5.4 `Response`：交互结果

`Response` 是值类型，包含：

- `Hovered`
- `Pressed`
- `Held`
- `Released`
- `Clicked`
- `DoubleClicked`
- `RightClicked`
- `Active`
- `Focused`
- `FocusVisible`
- `Disabled`

应用通常只写：

```csharp
if (ui.Button("Save", "save").Clicked) { ... }
```

这正是即时模式的核心 API 风格：控件方法返回结果，不注册事件。

---

## 6. 写一个控件：从 `Button` 追踪

以 `src/Vela.Core/Ui.Controls.cs` 的 `Button` 为例。

### 6.1 Ui 方法创建元素

```csharp
var element = frame.AddElement(
    ButtonWidget.Instance,
    id ?? label,
    style,
    "button",
    interactive: true,
    focusable: true,
    activatable: true);

element.Text = label;
element.Payload = variant;
element.Cursor = disabled ? CursorKind.Default : CursorKind.Hand;
element.Font = style?.Font ?? Theme.Button;
element.TextFormat = new TextFormat(...);
frame.UpdateResponse(element, disabled);
return element.Response;
```

它做了四件事：

1. 创建 `Element` 并挂到当前父级；
2. 写入本帧数据；
3. 从上一帧交互结果更新 `Response`；
4. 返回结果给应用。

### 6.2 `ButtonWidget.Measure`

按钮测量文本、图标、padding、最小高度，并调用：

```csharp
LayoutUtil.ResolveLeaf(element, constraints, contentSize)
```

`ResolveLeaf` 负责：

- 处理紧约束；
- 解析固定/百分比/自动宽高；
- 应用 min/max；
- 应用 `AspectRatio`；
- 把 padding/border 加进 border-box。

### 6.3 `ButtonWidget.PaintContent`

绘制顺序通常是：

1. 背景；
2. 边框；
3. 图标；
4. 文本；
5. focus ring。

按钮背景通过 `Ui.AnimateColor` 做 hover 过渡：

```csharp
background = context.Ui.AnimateColor(
    Id.Combine(element.Id, "button-background"),
    background,
    hovered ? 22f : 16f);
```

动画状态也存进 `StateStore`，所以它会跨帧保留。

### 6.4 为什么没有 `OnClick`

即时模式没有持久控件对象可以承载事件。点击结果是一个 **本帧返回值**：

```csharp
if (ui.Button("Save", "save").Clicked)
{
    Save();
}
```

这也意味着：注册回调、在 lambda 中保存控件引用，都不是 Vela 的模式。

---

## 7. 布局系统

核心文件：

```text
src/Vela.Core/Widgets/FlexWidget.cs
src/Vela.Core/Widgets/GridWidget.cs
src/Vela.Core/Widgets/WrapWidget.cs
src/Vela.Core/Widgets/DockWidget.cs
src/Vela.Core/Widgets/SplitterWidget.cs
src/Vela.Core/Ui.LayoutExtras.cs
src/Vela.Core/Runtime/Widget.cs   (LayoutUtil)
```

### 7.1 约束模型

`Constraints` 有四个值：

- `MinWidth`
- `MaxWidth`
- `MinHeight`
- `MaxHeight`

`IsTightWidth` 表示 `MinWidth == MaxWidth`。

布局不是“设置 x/y/w/h”，而是：

```text
父级给出约束 → 子级返回尺寸 → 父级安排位置
```

对应：

```csharp
context.Measure(child, constraints)   // 子级测量
context.Arrange(child, bounds)        // 父级安排
```

### 7.2 Flex 算法

`FlexWidget.Measure` 的关键步骤：

1. 解析自身宽高；
2. 计算主轴/交叉轴；
3. 测量每个子项的 base size、margin、grow/shrink；
4. 计算 gap；
5. 主轴有剩余时按 grow 分配；不足时按 shrink 压缩；
6. 在最终主轴尺寸下再次测量子项，让文本/nested container 正确换行；
7. 返回自身最终尺寸。

`FlexWidget.Arrange`：

1. 按同样规则分配主轴；
2. 计算 justify 剩余空间；
3. 按 `AlignItems` 计算交叉轴位置；
4. 支持 reverse；
5. 递归 `context.Arrange`；
6. 处理 absolute 子项。

关键细节：

- child 的 `Style.Width`/`Height` 是 **不是**布局指令，最终以父级约束和自身测量为准；
- `Grow` 只在主轴有剩余时生效；
- `Shrink` 只在主轴不足时生效；
- `AlignItems.Stretch` 只在交叉轴 auto 且没有 Max 限制时拉伸。

### 7.3 Grid

`GridWidget` 的步骤：

1. 解析子项放置（显式 `GridColumn/GridRow` 或自动 row-major）；
2. 解析列轨道：Points/Percent/Auto/Star；
3. 用子项自然尺寸求 Auto 列；
4. 有确定宽度时把剩余分给 Star 列；
5. 用最终列宽测量子项，求行高；
6. 有确定高度时分配 Star 行；
7. 安排每个单元格。

当前 Grid 是一个“实用优先”的实现：支持常见 dashboard 布局，但不是完整 CSS Grid（没有 min-content、auto-fit、container query）。

### 7.4 Wrap / Dock / SplitPane

- `WrapWidget`：按行流式放置，宽度超限换行；行高取该行最大子项高度。
- `DockWidget`：子项用 `Style.Dock` 声明 Top/Bottom/Left/Right/Fill；按声明顺序逐步占用剩余矩形。
- `SplitPane`：不是单独的复杂 Widget，而是 `Ui.SplitPane` 组合 Row/Column + 两个 Panel + `SplitterWidget`；第一面板尺寸由应用通过 `ref` 保存。阅读时同时看：
  - `Ui.LayoutExtras.cs`
  - `SplitterWidget.cs`
  - `Frame.cs` 的 `AddElement/Response`

### 7.5 布局调试

打开 `CaptureDiagnostics` 后，`FrameDiagnostics.Elements` 会有每个元素的：

- `Id`
- `Name`
- `Bounds`
- `ContentBounds`
- `Interactive`
- `Focused`

在测试里通过 `UiTestHost.Bounds(id)` 直接断言几何，这是理解布局行为最快的方式。

---

## 8. 交互模型

核心文件：

```text
src/Vela.Core/Interaction/InteractionFrame.cs
src/Vela.Core/Interaction/Response.cs
src/Vela.Core/Interaction/ScrollState.cs
```

### 8.1 为什么用上一帧快照

本帧布局还没完成时，不可能知道点击应该落在哪个元素。所以交互使用上一帧：

```text
上一帧 EndFrame → FrameSnapshot
下一帧 BeginFrame → InteractionManager.HitTest(snapshot, input)
```

`FrameSnapshot` 记录：

- 节点几何；
- 裁剪矩形；
- 父索引；
- 浮层组；
- `NodeFlags`；
- 光标；
- 滚动轨道/滑块；
- 可聚焦列表。

### 8.2 命中测试

`InteractionManager.HitTest` 的大致逻辑：

1. 从最上层浮层组开始；
2. 跳过 pass-through 组；
3. 在组内从后往前找 `Interactive` 且未 disabled 的节点；
4. 基础树同样从后往前；
5. 返回目标 ID + top node index。

浮层的 `TrapFocus`、`PassThrough` 会影响结果。

### 8.3 点击流程

```text
平台鼠标按下
  → InputBuilder.SetButton
  → FrameInput.PressedButtons
  → InteractionManager 设置 active/focus
  → 本帧 Response.Pressed
控件逻辑
  → 平台释放
  → FrameInput.ReleasedButtons
  → InteractionManager 产生 Released/Clicked/DoubleClicked
  → 下一帧 Response.Clicked
```

双击由时间和距离判定。

### 8.4 焦点

- 鼠标按下可聚焦控件时，焦点移到该控件；
- Tab/Shift+Tab 在 `Focusables` 中循环；
- Popup/Modal 的 `TrapFocus` 限制焦点在浮层内；
- 浮层关闭时恢复打开前的焦点；
- 方向键做空间导航，文本/滑块/分割条/虚拟列表可标记 `HandlesArrowKeys` 阻止全局导航；
- `Ui.Invoke(id)` 和 UIA Invoke 通过程序化点击队列触发下一次点击。

### 8.5 滚动

`ScrollWidget` / `ScrollState`：

- 内容高度；
- 视口高度；
- offset/target；
- 平滑滚动；
- wheel step；
- 滚动条拖动状态；
- 嵌套滚动选择最近的 `Scrollable` 祖先。

阅读顺序：

1. `Ui.Scroll.cs`
2. `ScrollWidget.cs`
3. `ScrollState.cs`
4. `InteractionManager.ApplyWheel`

---

## 9. 文本系统

核心文件：

```text
src/Vela.Core/Text/ITextEngine.cs
src/Vela.Core/Text/TextFormat.cs
src/Vela.Core/Text/TextEditing.cs
src/Vela.Core/Text/TextBlockLayout.cs
src/Vela.Core/Widgets/TextFieldWidget.cs
src/Vela.Core/Widgets/TextAreaWidget.cs
src/Vela.Rendering.Skia/SkiaTextEngine.cs
```

### 9.1 分层

```text
TextFieldWidget / TextAreaWidget
  → 编辑状态、光标、选区、剪贴板、撤销
  → ITextEngine 负责测量/命中/绘制
  → SkiaTextEngine 负责字体回退和 HarfBuzz shaping
```

`ITextEngine` 不知道控件，也不知道编辑状态；它只回答：

- 这行多高？
- 这段文字多宽？
- 某个 x 坐标对应哪个字符？
- 怎么绘制？

### 9.2 HarfBuzz shaping

`SkiaTextEngine` 缓存 `SKShaper`：

```csharp
private readonly Dictionary<SKTypeface, SKShaper> _shapers = new();
```

测量和绘制都使用 shaped width 和 `DrawShapedText`。这保证连字、复杂脚本和 kerning 在测量与绘制时一致。

### 9.3 多行布局

`TextBlockLayout`：

- 按 `\n` 分段；
- 在可用宽度内按空格/字符换行；
- 保留 UTF-16 索引；
- 提供 `LineIndexForCaret`、`CaretX`、`IndexAt`。

`TextAreaWidget` 使用它处理：

- 光标上下移动；
- 点击定位；
- 选区绘制；
- 垂直滚动；
- IME preedit 叠加。

### 9.4 文本编辑状态

`TextFieldState` / `TextAreaState` 保存：

- caret；
- anchor/selection；
- preferred X（垂直移动保持列）；
- scroll；
- undo/redo 快照；
- `WasFocused`；
- 最后活动时间（光标闪烁）。

所有状态按 `(Id, Type)` 存储，因此 TextArea 同时拥有自己的 `ScrollState` 和编辑状态。

### 9.5 IME

`FrameInput.CompositionText` / `CompositionCursor` 是未提交 preedit：

- Win32：`WM_IME_COMPOSITION` 读取 `GCS_COMPSTR` / `GCS_RESULTSTR`；
- SDL3：`SDL_EVENT_TEXT_EDITING`；
- 文本控件在光标处绘制 composition，提交文本仍走 `TextInput`。

限制：候选窗位置目前按鼠标近似，不是精确的文本光标位置。

---

## 10. 浮层与焦点陷阱

核心文件：

```text
src/Vela.Core/Ui.Overlays.cs
src/Vela.Core/Interaction/PopupState.cs
src/Vela.Core/Runtime/Frame.Rendering.cs   (ArrangeOverlay)
```

### 10.1 Popup

```csharp
var trigger = ui.Button("Actions", "actions");
if (trigger.Clicked) ui.OpenPopup("actions-menu", trigger.Id);

ui.Popup("actions-menu", () =>
{
    if (ui.MenuItem("Rename").Clicked) { }
});
```

`Popup` 会：

- 打开时记录当前焦点；
- 添加全屏或锚定浮层；
- 阻止点击穿透；
- 支持 Escape/外部点击关闭；
- 关闭时恢复焦点。

### 10.2 Modal

```csharp
ui.Modal("new-project", ref _modalOpen, "New project", () => { ... });
```

Modal 使用 `TrapFocus` 浮层组，有：

- 遮罩；
- 居中；
- 焦点陷阱；
- 关闭按钮；
- 打开/关闭焦点恢复。

`Frame.ArrangeOverlay` 负责：

- FullScreen；
- AnchorBottom/Top；
- AtPoint；
- Center；
- BottomEnd/TopEnd；
- viewport 限制。

### 10.3 浮层顺序

浮层按 `OverlayLayer` 排序，后绘制的在上层。命中测试从最上层浮层开始；`PassThrough` 的 Toast/Tooltip 不阻止底层输入。

### 10.4 重要限制

- 没有公开的任意自定义 overlay API；
- Popup/Modal/Toast/Tooltip 都需要在可见期间每帧调用；
- 浮层关闭后的焦点恢复依赖打开的 `PreviousFocus`；
- 浮层节点也是临时元素，不要持有。

---

## 11. 渲染系统

核心文件：

```text
src/Vela.Core/Rendering/DrawCommand.cs
src/Vela.Core/Runtime/Frame.Rendering.cs
src/Vela.Rendering.Skia/SkiaRenderer.cs
src/Vela.Rendering.Skia/DrawDamageTracker.cs
src/Vela.Rendering.Skia/SkiaGpuRenderer.cs
src/Vela.Rendering.Skia/SkiaTextEngine.cs
```

### 11.1 DrawList

`DrawList` 是扁平的 `List<DrawCommand>`，包含：

- FillRect
- StrokeRect
- BoxShadow
- Text
- Line
- FillPath
- StrokePath
- Image
- PushClip / PopClip
- PushTransform / PopTransform
- PushOpacity / PopOpacity

它不包含元素、控件或布局信息。

### 11.2 Paint 顺序

`Frame.PaintFrame` 大致：

1. 绘制根节点背景；
2. 绘制元素背景/边框/阴影；
3. 应用裁剪；
4. 绘制元素内容（`PaintContent`）；
5. 绘制子级；
6. 绘制 overlay（focus ring、scrollbar）；
7. 按层绘制浮层。

具体在 `Frame.Rendering.cs` 和 `Widget.PaintOverlay`。

### 11.3 CPU 渲染

`SkiaRenderer.RenderToCanvas`：

- 清屏；
- 保存/缩放；
- 遍历 DrawList；
- 将命令转成 Skia 绘制。

`RenderToPixels` 把同一个 canvas 画到调用方提供的 BGRA 缓冲区（Win32 DIB / SDL 纹理）。

### 11.4 脏区域

`DrawDamageTracker` 比较前后两帧命令：

- 首帧/尺寸变化/变换/透明度变化 → 全屏；
- 其他变化 → 变化命令的联合包围矩形；
- 无变化 → 空 damage。

`SkiaRenderer.RenderToPixels(..., damage)` 只在 damage 内清屏并重放 DrawList；宿主只有 damage 非空时才 Present。

阅读时注意：

- 变换/透明度变化被保守处理为全屏；
- 目前合并成一个矩形，不是多矩形最优；
- GPU 默认 framebuffer 不保留，当前 GPU 路径整帧绘制。

### 11.5 GPU

`SkiaGpuRenderer`：

1. SDL3 OpenGL 上下文；
2. `GRGlInterface.Create`；
3. `GRContext.CreateGl`；
4. `GRBackendRenderTarget` 绑定 framebuffer 0；
5. 同一个 `DrawList` 画到 `SKSurface`；
6. `SDL_GL_SwapWindow`。

它不在 Core 里，`Vela.Core` 不依赖 OpenGL。

---

## 12. 平台宿主

### 12.1 Win32

核心文件：

```text
src/Vela.Platform.Win32/Win32Window.cs
src/Vela.Platform.Win32/Win32Surface.cs
src/Vela.Platform.Win32/NativeMethods.cs
src/Vela.Platform.Win32/Win32Clipboard.cs
src/Vela.Platform.Win32/Win32FileDialog.cs
src/Vela.Platform.Win32/Win32UiaProvider.cs
src/Vela.Desktop/DesktopApp.cs
src/Vela.Desktop/MultiWindowApp.cs
```

`Win32Window`：

- 注册窗口类；
- 创建窗口；
- 维护 DPI 和客户区像素；
- 把 `WM_*` 转成 `InputBuilder` 事件；
- 维护鼠标离开、滚轮、IME、拖放；
- 用 DIB 呈现 BGRA 像素；
- 提供最小/最大化/主题/光标；
- 静态窗口字典支持多个窗口。

`DesktopApp` 组合：

```text
Win32Window + InputBuilder + SkiaTextEngine + SkiaRenderer + Ui
```

`MultiWindowApp` 为每个窗口创建独立的 `Ui`/renderer。

### 12.2 SDL3

核心文件：

```text
src/Vela.Platform.Sdl/Sdl3Window.cs
src/Vela.Platform.Sdl/SdlClipboard.cs
src/Vela.Desktop.Portable/PortableApp.cs
```

`Sdl3Window` 负责：

- 创建窗口；
- 事件循环；
- 鼠标/键盘/文本编辑/拖放；
- CPU 流式纹理；
- 可选 OpenGL 上下文；
- 剪贴板。

`PortableApp`：

- 默认 CPU Skia；
- `UseGpu = true` 时创建 `SkiaGpuRenderer`；
- 同一套 `Ui`。

### 12.3 文件对话框与拖放

Win32：

```csharp
var path = Win32FileDialog.OpenFile(owner, "Open", "All|*.*");
```

拖放文件在每帧 `FrameInput.DroppedFiles` 中。

SDL3：

- 拖放已接入；
- 原生文件对话框尚未封装。

---

## 13. 可访问性

核心文件：

```text
src/Vela.Core/Accessibility/Accessibility.cs
src/Vela.Core/Ui.Accessibility.cs
src/Vela.Core/Runtime/Frame.cs  (BuildAccessibility)
src/Vela.Platform.Win32/Win32UiaProvider.cs
```

### 13.1 语义树

`AccessibilitySnapshot` 包含：

- Role
- Name
- Value
- Help
- Bounds（客户区 DIP）
- IsEnabled
- IsFocused
- IsFocusable
- Parent/Children

内置控件自动设置角色和名称。自定义控件：

```csharp
var canvas = ui.Canvas("volume", view => { ... }, new Style { Height = 40 }, interactive: true);
ui.Semantic(canvas.Id, "Volume", AccessibilityRole.Slider, value: "64");
```

### 13.2 Win32 UIA

`Win32UiaProvider` 实现：

- `IRawElementProviderSimple`
- `IRawElementProviderFragment`
- `IRawElementProviderFragmentRoot`

提供：

- Name、ControlType、BoundingRectangle；
- IsEnabled、Focusable、HasFocus；
- 父子/兄弟导航；
- `ElementProviderFromPoint`、`GetFocus`；
- Invoke / Toggle / SelectionItem；
- 焦点变化事件。

`HandleGetObject` 在 `WM_GETOBJECT` 时通过 `UiaReturnRawElementProvider` 返回 COM 对象。

### 13.3 阅读注意

- 这是 **只读数据 + 动作** provider；没有 ValuePattern.SetValue；
- UIA 调用可能来自其他线程，`Win32Window.Post` 把动作排回 UI 线程；
- `AccessibilitySnapshot` 每帧生成，UIA 查询读取最新快照；
- 无障碍和键盘导航是两套并行能力。

---

## 14. Demo 应用怎么组织

`src/Vela.Demo` 不是框架核心，但它是“如何用 Vela 写应用”的最好示例。

```text
DemoApp.cs           状态、shell、侧栏、topbar、overlay 菜单
DemoApp.Pages.cs     Overview / Projects / Analytics / Components / DataLab / Settings
DemoApp.Canvas.cs    图表等自定义绘制
DemoCharts.cs        图表辅助
DemoData.cs          示例数据
Program.cs           CLI、截图、smoke、benchmark
```

阅读顺序：

1. `Program.cs`：入口和截图模式；
2. `DemoApp.Build`：整个 shell；
3. `BuildShell`：Sidebar + ScrollArea + Page；
4. `DemoApp.Pages.cs: BuildDataLabPage`：Grid + SplitPane + DataTable + TreeView + TextArea；
5. `DemoApp.Canvas.cs`：Canvas 图表；
6. overlay 方法：Popup/Modal/Toast/Tooltip。

Demo 中值得注意的模式：

- 页面只由状态决定；
- 重复项目用稳定 ID；
- 浮层在 `Build` 末尾每帧调用；
- `--snapshot` 走同一套构建代码，只是不打开窗口；
- `--smoke` 打开 Win32 窗口跑固定帧数。

---

## 15. 测试怎么读、怎么写

核心测试文件：

```text
tests/Vela.Core.Tests/UiTestHost.cs
tests/Vela.Core.Tests/FakeTextEngine.cs
tests/Vela.Core.Tests/LayoutTests.cs
tests/Vela.Core.Tests/InteractionTests.cs
tests/Vela.Core.Tests/VirtualizationTests.cs
tests/Vela.Core.Tests/TextAreaTests.cs
tests/Vela.Core.Tests/AnimationTests.cs
tests/Vela.Core.Tests/KeyboardAccessibilityTests.cs
tests/Vela.Rendering.Tests/*
```

`UiTestHost`：

```csharp
host.Frame(ui => { ... }, input => { ... });
```

它封装了：

- `InputBuilder.BeginFrame`；
- 可选的输入事件；
- `Ui.BeginFrame`；
- 构建；
- `Ui.EndFrame`；
- 保存 diagnostics。

测试断言：

```csharp
Assert.Equal(new Rect(0, 0, 195, 50), host.Bounds(id));
Assert.Equal(5, selected);
Assert.Contains(host.Result.Accessibility.Nodes, n => n.Name == "Save");
```

`FakeTextEngine` 使用固定字符宽度，让布局测试稳定。

### 写一个新测试

1. 用 `UiTestHost` 创建固定 viewport；
2. 第一个 Frame 构建控件，拿到 `Id` 或 `Bounds`；
3. 第二个 Frame 注入输入（点击/键盘/滚轮）；
4. 断言状态、几何、焦点、响应或 accessibility；
5. 对渲染输出，使用 `SkiaRenderer` 或 PNG/golden 测试。

---

## 16. 推荐阅读计划

### 第 1 小时：建立直觉

- 运行 Demo；
- 读 `README.md`；
- 读 `Ui.cs` 的 `BeginFrame/EndFrame`；
- 读 `Frame.cs` 的字段和 `Complete`；
- 用 `--snapshot --page data-lab` 看无窗口渲染。

### 第 2 小时：布局

- 读 `FlexWidget.Measure/Arrange`；
- 读 `Constraints`、`Style`、`Dimension`、`LayoutUtil`；
- 看 `LayoutTests`；
- 自己写一个 3 个子项的 Row，预测宽度，再用测试验证。

### 第 3 小时：交互

- 读 `InteractionFrame.cs`；
- 读 `Response`、`ScrollState`；
- 看 `InteractionTests`；
- 追踪一次点击/一次滚轮。

### 第 4 小时：控件

- 读 `ButtonWidget`；
- 读 `Ui.Controls.cs`；
- 读 `TextWidget`、`TextFieldWidget`；
- 自己加一个简单控件并用测试验证。

### 第 5 小时：文本

- 读 `ITextEngine`、`TextFormat`、`TextEditing`；
- 读 `TextBlockLayout`；
- 读 `TextAreaWidget`；
- 读 `SkiaTextEngine` 的 shaping 和 fallback。

### 第 6 小时：渲染与平台

- 读 `DrawList`；
- 读 `SkiaRenderer`；
- 读 `DesktopApp`；
- 再读 `Sdl3Window`/`PortableApp`。

### 后续

- Grid/Wrap/Dock；
- VirtualRows/DataTable/TreeView；
- Overlay；
- UIA；
- damage/GPU。

---

## 17. 调试技巧

### 17.1 元素几何不对

- 打开 `CaptureDiagnostics`；
- 打印 `FrameDiagnostics.Elements`；
- 用 `UiTestHost.Bounds(id)` 做最小复现；
- 检查父级是 `Column` 还是 `Row`；
- 检查 `Width/Height` 是 auto/fixed/percent；
- 检查 `Grow/Shrink`；
- 检查 `MaxWidth/MaxHeight`；
- 检查 `AlignItems.Stretch` 是否因为 Max 限制而没有拉伸。

### 17.2 点击没反应

- 是否在新元素的第一帧就点击？下一帧才可交互；
- 是否有浮层遮罩/焦点陷阱；
- ID 是否稳定；
- 元素是否 `Interactive`/未 disabled；
- 是否被裁剪；
- 是否被 active capture 到别的元素。

### 17.3 滚动/滚动条不动

- `ScrollContentHeight` 是否大于 viewport；
- `ScrollWidget` 是否拿到确定高度；
- 是否嵌套滚动被内层先消费；
- 滚动条几何是否在上一帧被记录；
- 测试可直接读 `ScrollState`。

### 17.4 文本/输入异常

- 字体是否有字形；
- `TextFormat` 的 Wrap/Overflow；
- 光标索引是否按 grapheme 移动；
- TextArea 的 layout 宽度是否为零；
- IME composition 是否已清空。

### 17.5 截图与窗口不一致

- DPI/scale；
- 字体在不同机器上的度量差异；
- GPU path 与 CPU path；
- 截图前是否只跑了两帧；
- damage 是否跳过了第一帧呈现。

---

## 18. 常见坑

1. **忘记 `using` 容器**：后续控件挂错父级。
2. **用 label 当 ID**：文本变化后状态丢失。
3. **在循环里复用同一个 ID**：滚动/选区冲突。
4. **持有控件对象**：`Element` 只活一帧。
5. **在 `PaintContent` 里修改业务状态**：绘制应只读；动画/滚动状态除外。
6. **在 `Build` 外调用 UI 方法**：会抛“只能 BeginFrame/EndFrame 之间使用”。
7. **误以为 `Clicked` 在平台线程触发**：它在帧构建时是上一帧交互的结果。
8. **用 `Visible` 当布局隐藏**：`Visible=false` 不参与测量；需要占位时用透明度/占位尺寸。
9. **忽略 content clip**：子级超出父级时，除非父级 `ClipContent`，否则会画出去。
10. **在 GPU path 期待 damage**：当前 GPU 整帧。
11. **把 UIA 当完整可写控件协议**：目前只读数据 + 动作。

---

## 19. 扩展路线

### 加一个 Widget

最小骨架：

```csharp
internal sealed class MyWidget : Widget
{
    public static MyWidget Instance { get; } = new();

    public override Size Measure(Element element, LayoutContext context, Constraints constraints)
    {
        var content = new Size(120, 32);
        return LayoutUtil.ResolveLeaf(element, constraints, content);
    }

    public override void PaintContent(Element element, PaintContext context)
    {
        context.DrawList.FillRect(
            element.Bounds,
            new Brush(context.Theme.Accent),
            CornerRadius.All(context.Theme.RadiusSmall));
    }
}
```

然后：

```csharp
var element = frame.AddElement(
    MyWidget.Instance,
    id,
    style,
    "my-widget",
    interactive: true);
element.AccessibilityRole = AccessibilityRole.Button;
element.AccessibilityName = "My widget";
frame.UpdateResponse(element);
return element.Response;
```

### 加一个自定义绘制

优先用 `Canvas`，而不是新 Widget：

```csharp
ui.Canvas("sparkline", view =>
{
    view.DrawList.Line(...);
}, new Style { Height = 80 });
```

`Canvas` 不参与子元素布局，但能访问 `Bounds/Clip/Theme/Input`。

### 加一个渲染后端

消费 `DrawList`，实现：

- 所有 `DrawCommandKind`；
- clip/transform/opacity 的 push/pop；
- `ITextEngine`；
- `ITexture`；
- 可能的 GPU surface 生命周期。

### 加一个平台宿主

实现：

- 窗口创建/DPI；
- `InputBuilder` 事件；
- BGRA 像素或纹理呈现；
- 剪贴板；
- 关闭和尺寸变化。

Win32 的 IME/文件对话框/UIA/多窗口是平台增强，不是 Core 必需。

---

## 20. 术语表

| 术语 | 含义 |
|---|---|
| Immediate mode | 每帧重新描述 UI，不保留控件对象树 |
| Element | 一帧内的控件实例，持有本帧数据和几何 |
| Widget | 控件行为，无状态单例，负责 measure/arrange/paint |
| Frame | 一帧的构建上下文和管线 |
| FrameSnapshot | 上一帧解析后的几何/交互快照 |
| Response | 当前控件本帧的交互结果 |
| StateStore | 按 `(Id, Type)` 存储跨帧状态 |
| LayoutContext | 布局阶段的测量/安排入口 |
| PaintContext | 绘制阶段上下文 |
| DrawList | 渲染器无关的扁平绘制命令 |
| Damage | 需要重绘的矩形 |
| Shaping | HarfBuzz 的字形选择/定位 |
| Preedit | IME 尚未提交的组合文本 |
| UIA | Windows UI Automation |
| PassThrough | 浮层不阻止底层输入 |
| TrapFocus | 浮层把焦点限制在内部 |

---

## 21. 最后：理解的标志

如果你能回答下面这些问题，说明已经理解了这个项目：

1. 为什么 `Element` 不能跨帧保存？
2. 为什么命中测试要用上一帧的 `FrameSnapshot`？
3. 为什么 `StateStore` 要按 `(Id, Type)` 而不是只按 `Id`？
4. 一个 Row 中 `Grow=1` 的子项在什么情况下不会占满剩余空间？
5. `Clicked` 是在哪一帧产生、哪一帧被应用读取？
6. `Popup` 关闭后焦点如何恢复？
7. `TextArea` 的 caret 如何从 `TextBlockLayout` 映射到屏幕坐标？
8. CPU 的 damage 区域和 GPU 的整帧绘制分别在哪里实现？
9. `Ui.Semantic` 和 UIA 之间的关系是什么？
10. 如果新增一个平台宿主，最小需要实现哪些接口？

能沿着 `app.Run → Frame → Layout → Snapshot → Paint → DrawList → Skia → Window` 走一遍，就已经具备继续开发这个项目的基础。


