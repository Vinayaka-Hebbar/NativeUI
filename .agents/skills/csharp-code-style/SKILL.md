---
name: csharp-code-style
description: >-
  Enforces C# code style, naming conventions, modern .NET 10 idioms, nullability,
  and performance practices for NativeUI. Use this skill whenever creating, editing,
  or reviewing C# source files (.cs) across any NativeUI project.
---

# C# Code Style and Readability

Follow these rules when writing or refactoring C# code in the NativeUI repository.

## 1. Language & Framework Baseline

- Target `.NET 10` (`net10.0`) with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.
- Use file-scoped namespaces in every source file:
  ```csharp
  namespace NativeUI.Controls;
  ```
- Place one primary public type per file, matching the filename (`Button.cs` -> `public class Button`).

## 2. Naming Conventions

| Symbol Kind | Convention | Example |
|---|---|---|
| Namespace, Class, Struct, Record, Enum, Interface | `PascalCase` | `NativeUI.Core`, `UIElement`, `FlexDirection` |
| Interface | `I` + `PascalCase` | `IBindableProperty`, `IStyleSheet` |
| Public / Protected Property, Method, Event | `PascalCase` | `Text`, `MeasureLayout()`, `ValueChanged` |
| Private Instance Field | `_camelCase` | `_nativeHandle`, `_bindings` |
| Constant / Static Readonly Value | `PascalCase` | `DefaultGap`, `EmptyStyle` |
| Parameter / Local Variable | `camelCase` | `propertyName`, `targetElement` |
| Type Parameter | `T` + `PascalCase` | `TView`, `TValue` |

## 3. Formatting & Structure

- Use **Allman style** braces (opening and closing braces on their own lines) for types, methods, and control blocks.
- Keep methods focused and concise. Prefer guard clauses and early returns over deeply nested `if`/`else` blocks.
- Order type members consistently:
  1. Constants and static fields
  2. Private instance fields
  3. Constructors
  4. Public and internal properties / events
  5. Public and internal methods
  6. Protected methods
  7. Private helper methods
  8. Nested types

## 4. Type Safety & Performance

- **Explicit Types (Never Use `var`)**: Always write explicit type declarations for local variables and loop variables. Never use `var`.
- **Strict Nullability**: Never suppress nullable warnings with the null-forgiving operator (`!`) unless the invariant is proven and documented with a brief inline comment.
- **Avoid Reflection in Hot Paths**: Use strongly typed delegates or compile-time generated property accessors for observable bindings and style updates.
- **Resource Cleanup**: Implement `IDisposable` deterministically whenever a type holds event subscriptions, native handles, or `JSObject` references.
- **Modern C# Expressions**: Prefer pattern matching (`is not null`, `switch` expressions), collection expressions (`[]`), and `readonly record struct` for small immutable value types (e.g., `Thickness`, `Size`, `CssValue`).

## Verification

1. Run `dotnet build NativeUI.slnx` and verify zero compiler or nullability warnings.
2. Confirm every new `.cs` file uses a file-scoped namespace matching its folder path.
