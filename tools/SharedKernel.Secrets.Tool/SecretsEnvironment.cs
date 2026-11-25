using SharedKernel.Processes;

namespace SharedKernel.Secrets.Tool;

/// <summary>What the commands need from the outside world, injected so tests can fake it.</summary>
/// <param name="WorkingDirectory">The directory that a relative path and the default root resolve from.</param>
/// <param name="ExecutableResolver">Finds age, age-keygen, sops, and git in the absolute directories of <c>PATH</c>.</param>
/// <param name="Runner">Runs the programs.</param>
/// <param name="OpenStandardInput">Opens standard input, which <c>encrypt</c> reads the plaintext from.</param>
/// <param name="OpenStandardOutput">Opens standard output, which <c>decrypt</c> writes the plaintext to.</param>
/// <param name="GetEnvironmentVariable">Reads an environment variable: <c>SOPS_AGE_KEY_FILE</c>, a path, and for <c>edit</c> the editor, the temporary root, and the terminal variables.</param>
/// <param name="IsTerminal">Tells whether standard input and output are a terminal, which <c>edit</c> needs.</param>
/// <param name="FileReadTimeout">The longest a file read may take before the file counts as not readable (a FIFO or device would block forever); five seconds by default.</param>
internal sealed record SecretsEnvironment(
    string WorkingDirectory,
    IExecutableResolver ExecutableResolver,
    IToolRunner Runner,
    Func<Stream> OpenStandardInput,
    Func<Stream> OpenStandardOutput,
    Func<string, string?> GetEnvironmentVariable,
    Func<bool> IsTerminal,
    TimeSpan? FileReadTimeout = null);
