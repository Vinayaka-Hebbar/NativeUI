# CLAUDE.md — Claude Code Instructions for NativeUI

@AGENTS.md

## Claude Code Configuration

- All repository architecture rules, coding standards, and build conventions are maintained in [AGENTS.md](./AGENTS.md) (imported above).
- Reusable skills for this repository are available in `.claude/skills/` (linked directly to `.agents/skills/` to prevent duplication).
- Consult the appropriate skill in `.claude/skills/<skill-name>/SKILL.md` when writing C# code, documenting APIs, structuring assemblies, writing tests, authoring documentation, or preparing commit messages.

## Headless Testing & Platform Boundary Discipline

- **Never Mock Platform State in Production Classes**: Never add mock storage, fallback attribute/style dictionaries, or command logs to production platform wrappers or drawing contexts to accommodate headless testing.
- Platform wrappers must cleanly no-op or return `null`/default when underlying native or browser elements (`_element`) are absent.
- Tests in headless environments must assert against the C# control layer, observables, events, and layout directly rather than polling artificial mock state in production code.
