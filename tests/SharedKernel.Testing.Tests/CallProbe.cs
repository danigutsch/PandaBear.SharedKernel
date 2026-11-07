namespace PandaBear.Testing.Tests;

/// <summary>The real <see cref="ICallProbe"/> that records what was called.</summary>
internal sealed class CallProbe : ICallProbe
{
    /// <summary>Gets the names of the methods called, in order.</summary>
    public List<string> Calls { get; } = [];

    /// <inheritdoc />
    public Task<int> Twice(int value)
    {
        Calls.Add(nameof(Twice));
        return Task.FromResult(value * 2);
    }

    /// <inheritdoc />
    public Task Run()
    {
        Calls.Add(nameof(Run));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> Fail()
    {
        Calls.Add(nameof(Fail));
        throw new InvalidOperationException("inner failure");
    }

    /// <inheritdoc />
    public ValueTask<int> Thrice(int value)
    {
        Calls.Add(nameof(Thrice));
        return ValueTask.FromResult(value * 3);
    }

    /// <inheritdoc />
    public ValueTask Pause()
    {
        Calls.Add(nameof(Pause));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public int Add(int left, int right)
    {
        Calls.Add(nameof(Add));
        return left + right;
    }

    /// <inheritdoc />
    public void Touch() => Calls.Add(nameof(Touch));

    /// <inheritdoc />
    public bool TryGet(string key, out string? value)
    {
        Calls.Add(nameof(TryGet));
        value = key == "known" ? "found" : null;
        return value is not null;
    }

    /// <inheritdoc />
    public void Increment(ref int value)
    {
        Calls.Add(nameof(Increment));
        value++;
    }

    /// <inheritdoc />
    public Task<string?> FindName()
    {
        Calls.Add(nameof(FindName));
        return Task.FromResult<string?>("name");
    }

    /// <inheritdoc />
    public Task<int?> FindNumber()
    {
        Calls.Add(nameof(FindNumber));
        return Task.FromResult<int?>(7);
    }
}
