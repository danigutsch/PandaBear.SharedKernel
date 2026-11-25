using System.Diagnostics;

namespace SharedKernel.LoadTesting.K6.Tests;

/// <summary>Answers for <see cref="ScriptedProcessRunner.AnswersInOrder"/>.</summary>
internal static class ProcessAnswers
{
    /// <summary>Creates an answer that returns <paramref name="exitCode"/>.</summary>
    public static Func<ProcessStartInfo, CancellationToken, ProcessOutput> Exit(int exitCode) =>
        (_, _) => new ProcessOutput(exitCode, string.Empty);

    /// <summary>Creates an answer that throws <paramref name="exception"/>.</summary>
    public static Func<ProcessStartInfo, CancellationToken, ProcessOutput> Throw(Exception exception) =>
        (_, _) => throw exception;
}
