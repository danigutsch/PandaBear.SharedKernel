using Microsoft.Extensions.Logging;
using Xunit;

namespace SharedKernel.Testing;

/// <summary>A logger of <see cref="TestOutputLoggerProvider"/>: it writes an entry to the output of the test the entry belongs to.</summary>
/// <param name="category">The category of the logger.</param>
/// <param name="owner">The output of the test that entries logged outside any test belong to, or <see langword="null"/> for none.</param>
internal sealed class TestOutputLogger(string category, ITestOutputHelper? owner) : ILogger
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <summary>
    /// Writes the entry, as <c>level: category[event id] message</c> followed by its exception, to the output of the running test, else to
    /// the owner's while that test runs, else to the diagnostic messages of the current context. It never throws because a test has
    /// finished.
    /// </summary>
    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var entry = $"{Abbreviation(logLevel)}: {category}[{eventId.Id}] {formatter(state, exception)}";
        if (exception is not null)
        {
            entry += Environment.NewLine + exception;
        }

        var context = TestContext.Current;
        var current = context.TestOutputHelper;
        if (TryWrite(current, entry) || (!ReferenceEquals(owner, current) && TryWrite(owner, entry)))
        {
            return;
        }

        context.SendDiagnosticMessage(entry);
    }

    private static bool TryWrite(ITestOutputHelper? output, string entry)
    {
        if (output is null)
        {
            return false;
        }

        try
        {
            output.WriteLine(entry);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string Abbreviation(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => "trce",
        LogLevel.Debug => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "fail",
        LogLevel.Critical => "crit",
        _ => logLevel.ToString(),
    };
}
