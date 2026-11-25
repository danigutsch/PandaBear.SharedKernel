namespace SharedKernel.Testing.Tests;

/// <summary>A small service the <see cref="CallProxy"/> tests wrap.</summary>
public interface ICallProbe : ICallProbeBase
{
    /// <summary>Doubles a number.</summary>
    /// <param name="value">The number.</param>
    /// <returns>Twice the number.</returns>
    Task<int> Twice(int value);

    /// <summary>Does something without a result.</summary>
    /// <returns>A task that completes when done.</returns>
    Task Run();

    /// <summary>Fails with an invalid-operation exception.</summary>
    /// <returns>Never completes normally.</returns>
    Task<int> Fail();

    /// <summary>Triples a number with a value task.</summary>
    /// <param name="value">The number.</param>
    /// <returns>Three times the number.</returns>
    ValueTask<int> Thrice(int value);

    /// <summary>Does something without a result, with a value task.</summary>
    /// <returns>A value task that completes when done.</returns>
    ValueTask Pause();

    /// <summary>Adds two numbers synchronously.</summary>
    /// <param name="left">The first number.</param>
    /// <param name="right">The second number.</param>
    /// <returns>The sum.</returns>
    int Add(int left, int right);

    /// <summary>Counts a synchronous call.</summary>
    void Touch();

    /// <summary>Fails synchronously with an invalid-operation exception.</summary>
    void Break();

    /// <summary>Finds a name, or nothing.</summary>
    /// <returns>A task with the name, or null.</returns>
    Task<string?> FindName();

    /// <summary>Finds a number, or nothing.</summary>
    /// <returns>A task with the number, or null.</returns>
    Task<int?> FindNumber();

    /// <summary>Counts up to a number and honours cancellation.</summary>
    /// <param name="value">The number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task with the number.</returns>
    Task<int> Count(int value, CancellationToken cancellationToken);

    /// <summary>Saves nothing, without a result.</summary>
    /// <returns>A task that completes when done.</returns>
    Task Save();

    /// <summary>Saves a number and returns it, an overload of <see cref="Save()"/> that has a result.</summary>
    /// <param name="value">The number.</param>
    /// <returns>A task with the number.</returns>
    Task<int> Save(int value);
}
