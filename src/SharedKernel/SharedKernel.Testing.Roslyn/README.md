# SharedKernel.Testing.Roslyn

Reusable Roslyn test helpers for SharedKernel analyzer and generator test suites.

## Usage

Use `AnalyzerTestHarness.CreateCompilation` to build in-memory C# compilations with explicit references. `TestAnalyzerConfigOptionsProvider` supplies editorconfig inputs and `DiagnosticLocationKey.Create` gives stable diagnostic location keys. Test source and expected diagnostics remain caller-owned.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Roslyn`. C# namespace: `PandaBear.Testing.Roslyn`. Assembly/project identity: `SharedKernel.Testing.Roslyn`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
