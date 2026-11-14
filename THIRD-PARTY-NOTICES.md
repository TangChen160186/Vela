# 第三方声明

> 为满足许可证的署名要求，下面保留的许可证原文使用英文；其余说明均为中文。

## SkiaSharp

`Vela.Rendering.Skia` 使用 [SkiaSharp](https://github.com/mono/SkiaSharp)，其许可证为 MIT。Skia 本身使用 BSD 风格许可证。完整许可证与声明见 SkiaSharp 包。

## HarfBuzzSharp / SkiaSharp.HarfBuzz

`Vela.Rendering.Skia` 使用 [SkiaSharp.HarfBuzz](https://github.com/mono/SkiaSharp) 和 HarfBuzzSharp 进行 OpenType 文字整形。HarfBuzz 使用 Old MIT 许可证；.NET 绑定跟随 SkiaSharp 的 MIT 许可证。完整声明见相应 NuGet 包。

## SDL3 / SDL3-CS

便携宿主使用 [ppy.SDL3-CS](https://github.com/ppy/SDL3-CS)，其中包含 SDL3 原生二进制。SDL3 使用 zlib 许可证；C# 绑定使用 MIT 许可证。完整声明见相应 NuGet 包。

## Interop.UIAutomationCore

Win32 宿主使用 [Interop.UIAutomationCore](https://github.com/roemer/Interop.UIAutomationCore) 提供的 UI Automation provider 接口。许可证与声明见相应 NuGet 包。

## Lucide 图标

`src/Vela.Core/Rendering/Icons.cs` 中的内置图标几何改编自 [Lucide](https://lucide.dev)，其许可证为 ISC：

```
ISC License

Copyright (c) for portions of Lucide are held by Cole Bemis 2013-2022 as part of
Feather (MIT). All other copyright (c) for Lucide are held by Lucide Contributors
2022.

Permission to use, copy, modify, and/or distribute this software for any purpose
with or without fee is hereby granted, provided that the above copyright notice
and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS
OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER
TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF
THIS SOFTWARE.
```
