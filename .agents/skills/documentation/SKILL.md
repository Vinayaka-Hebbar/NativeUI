---
name: documentation
description: >-
  Guides the writing of technical documentation, README files, architectural explanations,
  and platform compatibility matrices for NativeUI. Use this skill when creating or
  updating Markdown documentation, sample guides, or phase deliverables.
---

# Documentation Standards

Follow these rules when writing or updating documentation across the NativeUI repository.

## 1. Technical Honesty & Accuracy

- **Never overclaim**: Document only what is actually implemented and verified in the repository at the current phase, clearly distinguishing current capabilities from planned phases.
- **Explicit Interop & Platform Boundaries**:
  - Clearly explain how `NativeUI.Controls.Web` bridges C# controls to browser DOM elements via `JSObject` and `[JSImport]` without claiming C# classes directly inherit from JavaScript prototypes.
  - Clearly explain how shared C# contracts relate to WinUI 3, AppKit, and GTK 4 native controls.
  - Document platform-specific styling differences and unsupported CSS properties rather than claiming pixel-identical rendering across native OS widgets.

## 2. Structure & Formatting

- Use concise, well-structured GitHub-Flavored Markdown.
- Include runnable CLI commands for building, testing, and running samples on each supported OS.
- Use Markdown tables for control mappings (e.g., NativeUI Control -> HTML / WinUI 3 / AppKit / GTK 4) and feature support matrices.
- Keep documentation close to the code it describes (`README.md` at root for project overview and quickstart; `docs/` or project-level `README.md` for deep dives).

## 3. Phase Deliverable Documentation

When completing an implementation phase or proof of concept, document:
1. **Architecture & Trade-offs**: How the vertical slice is structured and why key design choices were made.
2. **Interop Boundary**: How managed C# state synchronizes with the native platform or browser DOM.
3. **Supported APIs & Limitations**: Which controls, properties, and bindings work today and what is deferred to subsequent phases.
4. **Build & Run Instructions**: Exact commands to build and verify the deliverable without paid tooling.

## Verification

1. Verify all file paths, project names, and CLI commands referenced in documentation match the actual repository state.
2. Check that no placeholder or unimplemented features are described as complete.
