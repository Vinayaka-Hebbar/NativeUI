---
name: code-comments-xmldoc
description: >-
  Defines standards for XML documentation comments and inline code comments in C#.
  Use this skill when authoring public or internal APIs, writing complex layout/binding/interop
  logic, or cleaning up comments in C# source files.
---

# Code Comments and XML Documentation

Maintain clear, accurate, and high-signal documentation across all NativeUI C# files.

## 1. XML Documentation (`///`)

- Provide XML documentation (`/// <summary>`) on all `public` and `protected` types, properties, methods, events, and enum members.
- Write summaries that explain **what** the API represents and any **platform behavior or thread-affinity expectations**, rather than restating the member name.
- Include `<param>` and `<returns>` tags when parameters or return values have non-obvious constraints (such as units, ownership, or nullability).
- Use `<exception cref="...">` when a method throws specific exceptions as part of its contract.

```csharp
/// <summary>
/// Represents a single-line editable text input backed by the target platform's native input control.
/// </summary>
public partial class Input : UIElement
{
    /// <summary>
    /// Gets or sets the current text content of the input control.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
```

## 2. No Banner Comments

- **Never** use decorative banner comments, section dividers, or ASCII art headers:
  ```csharp
  // ==========================================
  // Properties
  // ==========================================
  // ---------------- Helpers -----------------
  // ******************************************
  ```
- Do not use `#region` / `#endregion` blocks; organize files cleanly using member ordering or `partial` classes when pairing with source generators.

## 3. Inline Comments (`//`)

- Do **not** narrate obvious code (`// Increment counter`, `// Set text property`).
- **Never** use numbered step comments (`// 1. ...`, `// 2. ...`, `// Step 1: ...`) in any source files, tests, or conversational code examples and explanations.
- Use concise single-line `//` comments **only** to explain:
  - **Why** a non-obvious algorithm or order of operations is required.
  - Native platform quirks or browser DOM synchronization invariants (e.g., avoiding caret reset during two-way `input` events).
  - Memory lifetime ownership across managed/unmanaged or `JSObject` boundaries.
- Preserve existing valid comments and XML docs when editing unrelated code.

## Verification

1. Inspect modified `.cs` files to ensure no banner comments or `#region` directives exist.
2. Verify all newly added `public` types and members have valid `/// <summary>` XML documentation.
