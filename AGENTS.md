# AGENTS.md — NativeUI Agent Instructions

This file defines the canonical instructions, architectural boundaries, and development workflows for AI coding agents (Gemini / Antigravity, OpenAI Codex, and Claude Code) working in the **NativeUI** repository.

## Project Overview

**NativeUI** is a modular, open-source, cross-platform .NET UI framework written in C# (`net10.0`) targeting:

- **Desktop (Windows, macOS, Linux)** — Shared **SkiaSharp** rendering engine (`NativeUI.Graphics.Skia`) and shared CSS layout engine (`NativeUI.Layout`), paired with native OS windowing, input, clipboard, accessibility, and native text input controls (`NativeUI.Controls.Windows`, `NativeUI.Controls.MacOS`, `NativeUI.Controls.Linux`).
- **Web** — Real browser **HTML DOM elements** and native CSS via .NET WebAssembly (`System.Runtime.InteropServices.JavaScript` in `NativeUI.Controls.Web`).

NativeUI supports declarative XAML, C# components, observable ViewModels (MVVM), compiled CSS styling, reactive CSS theme variables, Flexbox, CSS Grid, and responsive layouts.

## Non-Negotiable Architectural Rules

1. **SkiaSharp on Desktop, Real HTML DOM on Web**:
   - On **Desktop (Windows, macOS, Linux)**, use `NativeUI.Graphics.Skia` and `NativeUI.Layout` for unified rendering, CSS styling, and layout, while keeping native platform hosts (`Window`, `MacOSHost`, `WindowsHost`, `LinuxHost`) for OS window management, DPI, input, accessibility, and native text input (`Input`) controls (avoiding reimplementing text selection/IME from scratch). Never use Electron, Tauri, or WebView-based desktop rendering.
   - On **Web**, create real HTML DOM elements (`<div>`, `<span>`, `<input>`, `<button>`) styled with native browser CSS. Never render the Web UI into a Skia `<canvas>`.
2. **No Separate Renderer Hierarchy**: Do not introduce `TextRenderer`, `InputRenderer`, `ButtonRenderer`, `IDomElement`, or separate wrapper hierarchies solely for rendering. Shared `UIElement` and control definitions participate directly in desktop Skia drawing/layout and Web DOM management.
3. **Web Interop in C#**: For the Web target, keep the Virtual DOM, reconciliation, binding engine, and event dispatching in C#. Use `JSObject` and `[JSImport]` only for minimal browser DOM operations inside `NativeUI.Controls.Web`. Never write a JavaScript Virtual DOM or reconciler.
4. **Honest Interop Boundaries & No Headless Mock State**: Acknowledge unavoidable native and browser interop boundaries explicitly. Do not claim C# classes directly inherit from JavaScript DOM objects or that identically named types in separate assemblies share CLR type identity. Never introduce fallback dictionaries, recorded command histories, or test-only tracking state into production platform classes to support headless test execution. When native or browser elements are absent (`_element is null`), operations must cleanly no-op or return `null`/default. Tests in headless environments must assert against C# control state, properties, events, and layout directly rather than polling artificial mock state in production code.
5. **Compile-Time XAML and CSS**: Parse and validate XAML (`XamlGenerator`) and CSS (`CssGenerator`) at compile time into generated C#. Do not parse full XAML views or CSS stylesheets at runtime on desktop, and do not mix CSS styling with WPF-style `Style`/`Setter` systems.
6. **Incremental Updates**: Observable property bindings (`INotifyPropertyChanged`, `INotifyCollectionChanged`, `ICommand`) must update only affected properties and controls (triggering targeted DOM updates on Web or targeted layout/repaint invalidation on desktop) without recreating the view tree.
7. **Phased Delivery**: Build incrementally in verified vertical slices. Avoid speculative placeholder implementations presented as complete.

## Solution Layout

```text
NativeUI.slnx
├── src/
│   ├── NativeUI.Core/               # Shared contracts, observables, bindings, CSS, XAML, Layout & Text
│   ├── NativeUI.Css.Compiler/       # Compile-time CSS parser and CssGenerator
│   ├── NativeUI.Xaml.Compiler/      # Compile-time XAML parser and XamlGenerator
│   ├── NativeUI.Controls/           # Shared UIElement and control definitions (View, Text, Input, etc.)
│   ├── NativeUI.Graphics/           # Platform-agnostic drawing & visual tree rendering abstractions
│   ├── NativeUI.Graphics.Skia/      # Shared SkiaSharp + HarfBuzz desktop drawing & text shaping engine
│   ├── NativeUI.Graphics3D/         # 3D scene types (Mesh, lights, GradientSky) and the polygon scene renderer
│   ├── NativeUI.DevTools/           # Skia overlay tools (FPS, layout bounds); browser dev tools cover Web
│   ├── NativeUI.Controls.Windows/   # Windows native window hosting, OS services & native text input
│   ├── NativeUI.Controls.MacOS/     # macOS (AppKit) native Window, MacOSHost & native text input
│   ├── NativeUI.Controls.Linux/     # Linux (GTK 4) native window hosting, OS services & native text input
│   ├── NativeUI.Controls.Web/       # WebAssembly HTML DOM implementation, WebHost & reconciler
│   └── NativeUI.Build/              # MSBuild targets and build integration
├── samples/
│   ├── NativeUI.Sample.Shared/      # Shared XAML, CSS, and ViewModels across platforms
│   ├── NativeUI.Sample.Windows/
│   ├── NativeUI.Sample.MacOS/
│   ├── NativeUI.Sample.Linux/
│   └── NativeUI.Sample.Web/
└── tests/
```

Future mobile targets follow the same platform naming pattern: `NativeUI.Controls.iOS` and `NativeUI.Controls.Android`.

## Core Coding & Workflow Conventions

- **Target Framework**: `.NET 10` (`net10.0`), `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`.
- **Code Style**: Use file-scoped namespaces, Allman braces, explicit types (never use `var`), `PascalCase` for public members/types, `camelCase` for locals/parameters, and `_camelCase` for private instance fields.
- **Type Naming**: Avoid redundant prefixes or implementation details in type names (e.g., use `IDispatcher` instead of `IUIDispatcher`, `WebHost` instead of `WebUIHost`, `MacOSHost` instead of `MacOSUIHost`, `Window` instead of `MacOSWindow`, `CssVariables` instead of `CssVariableMap`, `CssGenerator` and `XamlGenerator` instead of `*IncrementalGenerator`).
- **Comments & Explanations**: Document all public APIs with XML documentation (`/// <summary>`). Never use decorative banner comments (e.g., `// ====` or `// ----`) or numbered step comments (e.g., `// 1.`, `// 2.`, `// Step 1:`) in source code, tests, or conversational code examples and answers. Use concise inline `//` comments only for non-obvious logic or interop invariants.
- **Build & Test Commands**:
  - Build solution: `dotnet build NativeUI.slnx`
  - Run tests: `dotnet test NativeUI.slnx`
- **Git Discipline**: Do not create commits unless explicitly requested by the user.

## Reusable Agent Skills

Detailed domain workflows and checklists are organized as modular skills under `.agents/skills/` (and mirrored in `.claude/skills/`). Load the relevant `SKILL.md` before performing tasks in its domain:

- [`csharp-code-style`](.agents/skills/csharp-code-style/SKILL.md) — C# code style, naming conventions, nullability, and readability standards.
- [`code-comments-xmldoc`](.agents/skills/code-comments-xmldoc/SKILL.md) — XML documentation rules and inline comment guidelines.
- [`git-commit-messages`](.agents/skills/git-commit-messages/SKILL.md) — Commit message formatting and scope conventions.
- [`architecture-organization`](.agents/skills/architecture-organization/SKILL.md) — Project boundaries, dependency rules, Skia desktop rendering, and native/Web interop design.
- [`testing`](.agents/skills/testing/SKILL.md) — Unit, compiler, and platform testing conventions.
- [`documentation`](.agents/skills/documentation/SKILL.md) — Technical documentation, architecture notes, and platform limitation reporting.
