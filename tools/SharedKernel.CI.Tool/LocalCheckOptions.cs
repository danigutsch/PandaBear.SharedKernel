namespace PandaBear.CI.Tool;

/// <summary>What <c>local-check</c> runs and how.</summary>
/// <param name="Base">The revision to compare with <c>HEAD</c>, or <see langword="null"/> to select every test project.</param>
/// <param name="Configuration">The build configuration.</param>
/// <param name="Strict">Whether a lane that could not run fails the check.</param>
/// <param name="TestArguments">The arguments that every selected test executable receives after <c>--progress off</c>, such as a trait filter.</param>
/// <param name="Timeout">How long each child process may run before its process tree is killed and its lane fails.</param>
internal sealed record LocalCheckOptions(string? Base, string Configuration, bool Strict, string[] TestArguments, TimeSpan Timeout);
