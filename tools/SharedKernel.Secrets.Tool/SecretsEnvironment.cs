namespace PandaBear.Secrets.Tool;

/// <summary>What the commands need from the outside world, injected so tests can fake it.</summary>
/// <param name="WorkingDirectory">The directory that a relative path and the default root resolve from.</param>
/// <param name="ResolveExecutable">Finds a program by name and returns its absolute path, or <see langword="null"/>.</param>
/// <param name="Runner">Runs age, age-keygen, and sops.</param>
/// <param name="OpenStandardInput">Opens standard input, which <c>encrypt</c> reads the plaintext from.</param>
/// <param name="OpenStandardOutput">Opens standard output, which <c>decrypt</c> writes the plaintext to.</param>
/// <param name="GetEnvironmentVariable">Reads an environment variable; only <c>SOPS_AGE_KEY_FILE</c>, a path, is read.</param>
/// <param name="FileReadTimeout">The longest a file read may take before the file counts as not readable (a FIFO or device would block forever); five seconds by default.</param>
internal sealed record SecretsEnvironment(
    string WorkingDirectory,
    Func<string, string?> ResolveExecutable,
    IToolRunner Runner,
    Func<Stream> OpenStandardInput,
    Func<Stream> OpenStandardOutput,
    Func<string, string?> GetEnvironmentVariable,
    TimeSpan? FileReadTimeout = null);
