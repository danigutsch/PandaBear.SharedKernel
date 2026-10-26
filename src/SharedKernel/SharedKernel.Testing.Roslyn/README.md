# SharedKernel.Testing.Roslyn

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable Roslyn test helpers for SharedKernel analyzer and generator test suites.

## Usage

Use `AnalyzerTestHarness.CreateCompilation` to build in-memory C# compilations with explicit references. `TestAnalyzerConfigOptionsProvider` supplies editorconfig inputs and `DiagnosticLocationKey.Create` gives stable diagnostic location keys. Test source and expected diagnostics remain caller-owned.

## Generator tests

`GeneratorTestHarness` holds the mechanical setup of an incremental generator test suite. Parse the test sources in the test project, then:

- `CreateCompilation(syntaxTrees, assemblyName, additionalReferences)` builds a library compilation with the trusted platform references.
- `Run(generator, compilation, globalOptions)` runs one generator with a fresh driver; `RunAndUpdate` also returns the compilation that contains the generated sources and the reported diagnostics.
- `CreateDriver(generators, parseOptions, globalOptions, trackIncrementalGeneratorSteps: true)` returns a driver to run repeatedly and inspect step reasons for caching assertions.
- `GetGeneratedSource(runResult, hintName)` returns one generated source and `GetDiagnostics(runResult)` the reported diagnostics.

Keep default usings, referenced assemblies, wrapper methods named after the generator, and every assertion on generated output in the generator's own test project.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Roslyn`. C# namespace: `PandaBear.Testing.Roslyn`. Assembly/project identity: `SharedKernel.Testing.Roslyn`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
