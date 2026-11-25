# SharedKernel.CodeFixes.MSBuild

`MSBuildCodeFixRunner` loads a C# project or solution through Roslyn. It runs a caller-supplied analyzer,
applies fixes for one diagnostic ID in place, and formats the changed documents. The caller also supplies
the code fix provider. Its code actions must apply one set of workspace changes per action.

```csharp
using SharedKernel.CodeFixes.MSBuild;

var runner = new MSBuildCodeFixRunner(analyzer, codeFixProvider);
int? fixedCount = await runner.Run(targetPath, diagnosticId, Console.Error, cancellationToken);
```

`Run` accepts an existing, restored `.csproj`, `.sln`, or `.slnx` path. It returns the number of applied fixes
or `null` when MSBuild or the target could not load. Its error writer receives load failures and diagnostics
without a fix. Cancellation leaves any fixes already written on disk.

Run only on trusted projects. MSBuild uses the installed SDK and evaluates the target's build configuration.
The executable host needs a compatible .NET SDK and a direct reference to
`Microsoft.CodeAnalysis.Workspaces.MSBuild` so Roslyn's BuildHost content files are copied into its output.
The host must use the SDK's MSBuild assemblies at run time. In the host's package references, set
`ExcludeAssets="runtime"` and `PrivateAssets="all"` on `Microsoft.Build.Framework` and `Microsoft.NET.StringTools`.
This library uses dynamic SDK and workspace loading and requires a JIT host.

The [testing code-fix tool](../../../tools/SharedKernel.Testing.CodeFixRunner/README.md) is a complete host
that configures the testing analyzer and its code fix provider.

Package ID is `PandaBear.SharedKernel.CodeFixes.MSBuild`. Use a validated preview from a configured
candidate feed.
