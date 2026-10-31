---
name: architecture-organization
description: >-
  Defines the architectural rules, assembly boundaries, dependency graph, SkiaSharp
  desktop rendering model, and native/Web interop patterns for NativeUI. Use this skill
  when adding or modifying projects, designing UI controls, implementing bindings, or
  working on layout, Skia graphics, platform hosts, XAML, or CSS compilers.
---

# Architecture and Project Organization

Use this guide whenever designing or modifying components, projects, or platform implementations in NativeUI.

## 1. Core Architectural Mandates

1. **Hybrid Rendering Model (SkiaSharp Desktop + Real HTML DOM Web)**:
   - **Desktop (`Windows`, `MacOS`, `Linux`)**: Use `NativeUI.Layout` for shared CSS layout calculation and `NativeUI.Graphics.Skia` (SkiaSharp) for drawing, CSS styling, and animation. Keep native platform assemblies (`NativeUI.Controls.Windows`, `NativeUI.Controls.MacOS`, `NativeUI.Controls.Linux`) responsible for native `Window` management, Skia surface presentation, input, clipboard, accessibility, and hosting native text input (`Input`) controls so complex text editing/selection/IME works natively.
   - **Web (`NativeUI.Controls.Web`)**: Use real browser HTML DOM elements (`<div>`, `<span>`, `<input>`, `<button>`) via .NET WebAssembly (`JSObject` / `[JSImport]`) and the browser's native CSS layout/styling engine. Never render the Web UI into a Skia canvas.
2. **No Separate Renderer Abstraction**: Do **not** create `TextRenderer`, `InputRenderer`, `ButtonRenderer`, `IDomElement`, or a parallel wrapper hierarchy solely for rendering. Shared `UIElement` and control definitions participate directly in layout/drawing on desktop and DOM reconciliation on Web.
3. **No Per-Control DI Registration**: Use `Microsoft.Extensions.DependencyInjection` for application services, ViewModels, navigation, and theme/platform services—never for per-control renderers.
4. **Desktop Window Separation**: Keep reusable UI views (`UIElement`, `View`, `Page`) decoupled from desktop `Window` lifecycle so Web views never require a desktop window.
5. **Canonical Naming**:
   - Platform assemblies: `NativeUI.Controls.Windows`, `NativeUI.Controls.MacOS`, `NativeUI.Controls.Linux`, `NativeUI.Controls.Web` (and future `NativeUI.Controls.iOS`, `NativeUI.Controls.Android`).
   - Core types: `IDispatcher`, `WebHost`, `MacOSHost`, `Window` (scoped by platform namespace), `CssVariables`, `CssGenerator`, `XamlGenerator`.

## 2. Project Responsibilities & Dependency Rules

Maintain a strict directed acyclic dependency graph (no circular project references):

- `NativeUI.Core`: Observable primitives, `BindableObject`, `BindableProperty`, binding engine (`OneWay`, `TwoWay`, `OneTime`, `ICommand`), and `IDispatcher`. Zero platform or graphics dependencies.
- `NativeUI.Css`: CSS style runtime, `CssStyleSheet`, `CssVariables`, and `ComputedStyle`. References `NativeUI.Core`.
- `NativeUI.Css.Compiler`: Compile-time CSS parser and Roslyn `CssGenerator` (`IIncrementalGenerator`).
- `NativeUI.Xaml`: XAML runtime attributes and markup contracts. References `NativeUI.Core`.
- `NativeUI.Xaml.Compiler`: Compile-time XAML parser and Roslyn `XamlGenerator` (`IIncrementalGenerator`).
- `NativeUI.Layout`: Shared desktop CSS box model, Flexbox, CSS Grid, media query, and container query layout engine. References `NativeUI.Core` and `NativeUI.Css`.
- `NativeUI.Controls`: Shared `UIElement` and control definitions (`View`, `Page`, `Text`, `Input`, `Button`, `Flex`, `Grid`, `Canvas`, `Border`, `ScrollView`, `Image`, `CheckBox`, `Select`). References `NativeUI.Core`, `NativeUI.Css`, `NativeUI.Xaml`, and `NativeUI.Layout`.
- `NativeUI.Graphics.Skia`: Shared SkiaSharp desktop drawing engine (`SkiaCanvasRenderer` / element painting, backgrounds, borders, typography measurement and rendering). References `NativeUI.Core`, `NativeUI.Css`, `NativeUI.Layout`, and `NativeUI.Controls`.
- `NativeUI.Controls.Windows` / `NativeUI.Controls.MacOS` / `NativeUI.Controls.Linux`: Desktop OS integration (`Window`, `*Host`, Skia surface blitting, DPI/appearance events, and native text input overlay). Reference `NativeUI.Core`, `NativeUI.Controls`, `NativeUI.Layout`, and `NativeUI.Graphics.Skia`.
- `NativeUI.Controls.Web`: WebAssembly `WebHost`, C# Virtual DOM / reconciler, event dispatcher, and `[JSImport]` DOM bridge. References `NativeUI.Core`, `NativeUI.Css`, and `NativeUI.Controls` (does **not** reference `NativeUI.Graphics.Skia`).
- `NativeUI.Build`: Shared MSBuild `.props` and `.targets` wiring `.xaml` and `.css` files into `AdditionalFiles` for `CssGenerator` and `XamlGenerator`.

## 3. WebAssembly DOM Interop Boundary (`NativeUI.Controls.Web`)

- Represent browser DOM nodes in C# using `System.Runtime.InteropServices.JavaScript.JSObject`.
- Declare minimal browser interop entry points with `[JSImport]`.
- Keep component lifecycle, Virtual DOM diffing, keyed reconciliation, event routing, and binding updates entirely in C#.
- Dispose `JSObject` references deterministically when controls unmount.

## Verification

1. Check all `.csproj` files to confirm no circular `<ProjectReference>` links exist and that `NativeUI.Controls.Web` has no dependency on `SkiaSharp` or `NativeUI.Graphics.Skia`.
2. Verify platform-specific interop code resides strictly inside its corresponding `NativeUI.Controls.<Platform>` assembly.
