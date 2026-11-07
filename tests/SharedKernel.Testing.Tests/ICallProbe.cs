namespace PandaBear.Testing.Tests;

/// <summary>A small service the <see cref="CallProxy"/> tests wrap; public because proxies can only implement public interfaces.</summary>
public interface ICallProbe
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

    /// <summary>Looks a key up and hands the value back through an out parameter.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value found, or null.</param>
    /// <returns>True when the key is known.</returns>
    bool TryGet(string key, out string? value);

    /// <summary>Increments a number passed by reference.</summary>
    /// <param name="value">The number to increment in place.</param>
    void Increment(ref int value);

    /// <summary>Finds a name, or nothing.</summary>
    /// <returns>A task with the name, or null.</returns>
    Task<string?> FindName();

    /// <summary>Finds a number, or nothing.</summary>
    /// <returns>A task with the number, or null.</returns>
    Task<int?> FindNumber();
}
