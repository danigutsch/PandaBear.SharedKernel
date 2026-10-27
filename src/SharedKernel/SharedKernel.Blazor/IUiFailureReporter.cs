namespace PandaBear.Blazor;

/// <summary>Reports handled UI failures without exposing the exception message or stack.</summary>
public interface IUiFailureReporter
{
    /// <summary>Records that <paramref name="exception" /> was handled during <paramref name="operation" />.</summary>
    /// <param name="exception">The handled exception; only its type name is recorded.</param>
    /// <param name="operation">A short stable operation name chosen by code, never user input.</param>
    void Report(Exception exception, string operation);
}
