namespace PandaBear.Testing.CodeFixRunner;

/// <summary>Arguments of one code fix run.</summary>
/// <param name="TargetPath">Project or solution file to fix.</param>
/// <param name="DiagnosticId">Id of the diagnostic whose fix is applied.</param>
internal sealed record CodeFixRunnerOptions(string TargetPath, string DiagnosticId);
