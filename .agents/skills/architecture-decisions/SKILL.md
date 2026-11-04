---
name: architecture-decisions
description: >-
  Ground NativeUI architectural changes in existing repository patterns before
  implementation. Use when planning new controls, public APIs, folders, namespaces,
  assembly dependencies, compiler emission, or resource ownership, and when reviewing
  architectural departures. Routine fixes within an approved design need no new review.
---

# Architecture Decisions

Use alongside [architecture-organization](../architecture-organization/SKILL.md).
That skill defines assembly boundaries; this skill governs how to choose and verify
changes within them.

## Inspect before designing

Before writing implementation code, read the nearest existing implementation,
its callers, and its relevant tests. For cross-cutting work, inspect both sides of
the boundary. Cite the concrete files and patterns that support the proposed choice.
Do not choose an architecture from familiarity with another framework or from what
is quickest to generate.

For compiler work, inspect both the existing emitter and its output tests. NativeUI's
XAML and CSS emitters construct Roslyn syntax nodes and add imports with
`UsingDirective(ParseName(...))` at the compilation-unit level. Follow that pattern:
return typed syntax nodes from expression builders, use imported type names, and
keep strings as literal data or identifier names. Do not construct C# expressions or
declarations through namespace-prefix constants, string interpolation, or
`StringBuilder`, then parse the result. Use qualification only when an actual symbol
ambiguity requires it; explain that exception.

## Make structural decisions reviewable

For a new structure that the user has not already approved, present a concise
proposal before creating or moving implementation files. Include:

- The affected folder tree and namespaces, including intentional differences between them.
- Which assembly owns each public type and the resulting dependency direction.
- Existing implementations being followed and any necessary departure.
- Ownership, disposal, mutation/invalidation, and verification where relevant.

Wait for the user's approval of those structural choices. Approval already given
in the conversation remains valid: implement that design without asking again.
Routine implementation fixes within it do not require another approval. If a new
finding requires changing the approved architecture, explain the finding and get
approval for that change before implementing it.

## Preserve responsibilities

- Place a type according to its dependencies and responsibility, not its feature label.
  Geometry and brushes are shared data; shapes are controls. A factory returning a
  control belongs in Controls, even when its base source type belongs in Core.
- Reuse the existing binding, layout, drawing, DOM, and lifetime mechanisms. A new
  helper, interface, cache, or registry needs a concrete requirement that the existing
  mechanism cannot meet. State that reason before introducing it.
- Do not add speculative public types, generic utility layers, or runtime fallbacks
  merely to make an incomplete slice compile. Do not expand a folder correction into
  unrelated feature work.
- Keep immutable definitions separate from per-instance visual/backend state. Specify
  what invalidates caches and who releases resources; do not leave those implicit.
- Verify dependency compatibility through the repository's actual build and behavior;
  a package's minimum version declaration alone is not proof.

## Verify before extending the slice

Implement one usable vertical slice, review its diff against the approved structure,
and run the relevant checks before extending it. Inspect generated C# and compile it
against the actual public types; string assertions alone cannot establish correctness.
Test observable behavior, lifetime, and invalidation rather than mirroring helper code.

Report what was verified, what failed, and what remains incomplete. If the user
challenges a decision, answer why it was made first, inspect the relevant precedent,
and make the smallest justified correction. Do not respond with another broad rewrite.
