# SharedKernel.Testing.Roslyn

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable Roslyn test helpers for SharedKernel analyzer and generator test suites.

## Usage

Use `AnalyzerTestHarness.CreateCompilation` to build in-memory C# compilations with explicit references. `TestAnalyzerConfigOptionsProvider` supplies editorconfig inputs and `DiagnosticLocationKey.Create` gives stable diagnostic location keys. Test source and expected diagnostics remain caller-owned.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Roslyn`. C# namespace: `PandaBear.Testing.Roslyn`. Assembly/project identity: `SharedKernel.Testing.Roslyn`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
