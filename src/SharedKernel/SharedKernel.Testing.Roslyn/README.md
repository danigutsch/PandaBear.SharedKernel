# SharedKernel.Testing.Roslyn

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable Roslyn test helpers for SharedKernel analyzer and generator test suites.

## Usage

Use `AnalyzerTestHarness.CreateCompilation` to build in-memory C# compilations with explicit references. `TestAnalyzerConfigOptionsProvider` supplies editorconfig inputs and `DiagnosticLocationKey.Create` gives stable diagnostic location keys. Test source and expected diagnostics remain caller-owned.

## Governed constants

Pass the production compilations and the same catalog text used by SKSTYLE012 to
`GovernedConstantGuard.Validate(compilations, catalogText)`. Assert that it returns no errors. The guard checks that
each catalogued constant has its intended value and that other non-generated C# declarations do not duplicate or
redirect it. Owners whose source assembly is outside the supplied compilations can be resolved from references.
The [catalog format](../SharedKernel.Style.Analyzers/README.md#skstyle012) also supports documented compatibility
exceptions. Check one set of target-framework compilations at a time.

`GovernedLiteralManifest.Compile(path)` reconstructs a semantic compilation from a build's captured inputs. Write a
manifest per project and target framework with one `assembly|name` row, one `defines|comma,separated,symbols` row,
and a `source|absolute-path` or `reference|absolute-path` row for each compiler input. The build configuration and
list of projects to check belong to the consumer.

## Generator tests

`GeneratorTestHarness` holds the mechanical setup of an incremental generator test suite. Parse the test sources in the test project, then:

- `CreateCompilation(syntaxTrees, assemblyName, additionalReferences)` builds a library compilation with the trusted platform references.
- `Run(generator, compilation, globalOptions)` runs one generator with a fresh driver; `RunAndUpdate` also returns the compilation that contains the generated sources and the reported diagnostics.
- `CreateDriver(generators, parseOptions, globalOptions, trackIncrementalGeneratorSteps: true)` returns a driver to run repeatedly and inspect step reasons for caching assertions.
- `GetGeneratedSource(runResult, hintName)` returns one generated source and `GetDiagnostics(runResult)` the reported diagnostics.

Keep default usings, referenced assemblies, wrapper methods named after the generator, and every assertion on generated output in the generator's own test project.

## Analyzer runs

`AnalyzerRun.GetDiagnostics(compilation, analyzer, globalOptions)` runs one analyzer over a compilation and returns what it
reports, without the compiler's own diagnostics. `globalOptions` are the `.editorconfig` options the analyzer reads; pass an
`AnalyzerConfigOptionsProvider` instead for per-file options. Wrap it in the test project with the analyzer under test and the
default usings of the suite.

## Code fix tests

`CodeFixWorkspace` is an in-memory Roslyn workspace with a C# project and a main document `Test0.cs`, for code fix providers:

```csharp
var workspace = CodeFixWorkspace.Create(source);
var diagnostic = await workspace.CreateDocumentDiagnostic("MY001", "markerText");
var action = (await workspace.GetCodeActions(new MyCodeFixProvider(), diagnostic)).Single();
await workspace.ApplyCodeAction(action);
var fixedText = await workspace.GetDocumentText();
```

- `Create(source, options)` builds the project from `CodeFixWorkspaceOptions`: `AssemblyName`, `DefaultUsings` (put before every
  document; `CommonUsings` by default, empty for none), `AdditionalReferences` besides the trusted platform assemblies,
  `DocumentPath`, `DocumentationMode`, and `NullableContextOptions`.
- `CreateWithGlobalUsings(source, globalUsingsSource, options)` adds a `GlobalUsings.cs` and takes both sources as they are;
  `CreateWithProjectReference(primarySource, moduleSource, moduleAssemblyName, options)` builds a main project that references a
  module project.
- `AddDocument(source, name)` adds a document; `CreateDocumentDiagnostic(id, markerText)` places a warning on the first
  occurrence of the marker in the main document, or in a named document of any project, with optional properties.
- `GetCodeActions` collects what a provider registers for the diagnostic's document; `ApplyCodeAction` applies an action that
  must change the solution exactly once; `GetAnalyzerDiagnostics(analyzer, enabledDiagnosticId)` runs an analyzer through
  `AnalyzerRun`, switching on a rule that is off by default; `GetCompilationDiagnostics` returns the compiler's diagnostics.
- `GetDocumentText()` reads the main document, and the overloads read a document by identifier or by unique file name.

The package depends on `Microsoft.CodeAnalysis.CSharp.Workspaces`, at the same version as `Microsoft.CodeAnalysis.CSharp`.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Roslyn`. C# namespace: `PandaBear.Testing.Roslyn`. Assembly/project identity: `SharedKernel.Testing.Roslyn`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
