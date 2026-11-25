namespace SharedKernel.TestServices.Host;

/// <summary>The shared test services that the host started, the values it hands over for them, and the way to stop them.</summary>
/// <param name="values">Each environment variable that the tests read, with its value, in the order of <see cref="SharedTestServices.Variables"/>.</param>
/// <param name="stop">Stops the services by disposing the application that owns their containers.</param>
internal sealed class StartedTestServices(IReadOnlyList<(string Variable, string Value)> values, Func<ValueTask> stop) : IAsyncDisposable
{
    /// <summary>Gets each environment variable that the tests read, with its value; the values can carry passwords.</summary>
    public IReadOnlyList<(string Variable, string Value)> Values { get; } = values;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => stop();
}
