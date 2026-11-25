namespace SharedKernel.Secrets.Tool.Tests;

/// <summary>What a run of the tool produced.</summary>
/// <param name="ExitCode">The exit code.</param>
/// <param name="Output">The standard output text written by the tool.</param>
/// <param name="Error">The standard error text.</param>
/// <param name="Plaintext">The bytes written to the standard output stream, which <c>decrypt</c> uses.</param>
internal sealed record ToolRun(int ExitCode, string Output, string Error, byte[] Plaintext);
