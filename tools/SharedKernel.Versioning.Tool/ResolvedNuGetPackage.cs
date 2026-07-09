namespace SharedKernel.Versioning.Tool;

internal sealed record ResolvedNuGetPackage(string Id, string Version, string[] LockFiles);
