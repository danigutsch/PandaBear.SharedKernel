namespace PandaBear.Repository.Tests;

/// <param name="FileName">The requested executable.</param>
/// <param name="Arguments">The requested arguments.</param>
/// <param name="WorkingDirectory">The requested working directory.</param>
/// <param name="Token">The token that cancels the process.</param>
/// <param name="Environment">The environment after the request's start information was configured, or <see langword="null"/> for a request that configures none.</param>
internal sealed record ProcessCall(
    string FileName,
    string[] Arguments,
    string WorkingDirectory,
    CancellationToken Token,
    IReadOnlyDictionary<string, string?>? Environment = null);
