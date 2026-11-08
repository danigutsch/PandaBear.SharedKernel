namespace PandaBear.Versioning.Tool;

/// <summary>One planned <c>dotnet pack</c> call of the <c>pack-dev</c> command.</summary>
/// <param name="Step">The planned step.</param>
/// <param name="Arguments">The <c>dotnet</c> arguments.</param>
/// <param name="WorkingDirectory">The absolute working directory.</param>
internal sealed record DevPackInvocation(DevPackStep Step, IReadOnlyList<string> Arguments, string WorkingDirectory);
