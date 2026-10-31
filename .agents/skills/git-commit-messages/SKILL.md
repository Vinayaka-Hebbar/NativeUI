---
name: git-commit-messages
description: >-
  Guides the creation of structured, atomic Git commits and Conventional Commit
  messages. Use this skill whenever the user asks to commit changes, draft a
  commit message, or prepare a pull request summary.
---

# Git Commit Messages

Follow these rules when staging changes and writing Git commit messages.

## 1. Commit Discipline

- **Never commit automatically**: Only create a Git commit when explicitly asked by the user.
- **Stage cleanly**: Before committing, verify `git status` and `git diff` to ensure build outputs, temporary directories, and user-specific files are excluded.
- **Atomic commits**: Group logically cohesive changes into a single commit.

## 2. Commit Message Format

Use the **Conventional Commits** specification:

```text
<type>(<scope>): <imperative summary>

- <concise key change>
- <concise key change>
```

### Length & Style Rules

1. **Title max 70 characters**: The entire first line (`<type>(<scope>): <summary>`) MUST be **70 characters or fewer**.
2. **Imperative mood**: Start the summary and each bullet with an imperative verb (`add`, `fix`, `update`, `remove` — never past tense or `-ing`).
3. **No trailing period** on the title or bullets.
4. **Concise bullets**:
   - Keep each bullet on a **single line under 72 characters**.
   - Summarize *what* changed clearly without dumping long parenthetical lists of class or file names.
   - Limit the body to **3–5 bullets**.

### Allowed Types

- `feat`: New feature or capability
- `fix`: Bug fix
- `refactor`: Code restructuring without behavior changes
- `test`: Adding or updating tests
- `docs`: Documentation or instruction updates
- `build`: Build system, project, or dependency changes
- `chore`: Repository maintenance or tooling configuration

### Scope Selection

- Derive `<scope>` dynamically from the primary module, directory, or subsystem modified by the commit (lowercase noun). Omit `(<scope>)` only when changes span the entire repository evenly.

## Verification

1. Verify the header line is **<= 70 characters**.
2. Verify every bullet line is **<= 72 characters** and free of file/type dumps.
