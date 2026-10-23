namespace PandaBear.Repository.Tests;

internal sealed record ProcessCall(string FileName, string[] Arguments, string WorkingDirectory, CancellationToken Token);
