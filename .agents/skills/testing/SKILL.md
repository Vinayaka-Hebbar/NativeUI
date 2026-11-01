---
name: testing
description: >-
  Establishes testing standards, test project structure, and verification workflows
  for NativeUI. Use this skill when adding unit tests, testing observable bindings,
  verifying XAML/CSS compilers, or validating platform implementations.
---

# Testing Guidelines

Every vertical slice in NativeUI must include automated tests covering its core behavior.

## 1. Test Organization

Place test projects under `tests/` mirroring the source assemblies they verify.

## 2. Test Authoring Standards

- **External Fixture Files**: Never embed multi-line XAML, CSS, HTML, or JS test inputs as inline raw string literals in C# test files. Store test inputs as dedicated files under a `Fixtures/` directory in the test project (`CopyToOutputDirectory="PreserveNewest"`) and load them at runtime.
- **Arrange-Act-Assert**: Structure every test into clear Arrange, Act, and Assert phases.
- **Verify Incremental Behavior**: When testing bindings or style changes, assert not only the final value, but also that unaffected properties and sibling controls were **not** recreated or redundantly updated.
- **Verify Disposal**: Include tests asserting that disposing a binding or unmounting a control unsubscribes from change notifications and prevents memory leaks.
- **Isolate Browser / Native Handles Without Mock State**: Test C# reconciliation, binding, and control state directly when running headless on the standard .NET test host, while keeping actual browser/native verification in platform samples. Never add artificial mock storage, fallback attribute/style dictionaries, or recorded command logs to production platform wrappers or drawing contexts to support headless testing. Platform wrappers must cleanly no-op or return default/null values when underlying native or browser elements are absent.

## 3. Verification

1. Ensure all test projects in `NativeUI.slnx` build and pass with zero failures.
2. Confirm every new feature or bug fix includes automated tests for both the happy path and edge cases.
