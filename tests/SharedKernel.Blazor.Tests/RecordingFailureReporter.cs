namespace PandaBear.Blazor.Tests;

/// <summary>Records reported failures.</summary>
internal sealed class RecordingFailureReporter : IUiFailureReporter
{
    /// <summary>Gets the reports in order.</summary>
    public List<(Exception Exception, string Operation)> Reports { get; } = [];

    /// <inheritdoc />
    public void Report(Exception exception, string operation) => Reports.Add((exception, operation));
}
