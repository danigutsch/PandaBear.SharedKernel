using PandaBear.Results;

namespace PandaBear.Blazor.Tests;

/// <summary>Records what a page's reload and local removal callbacks were asked to do.</summary>
internal sealed class ListDeleteProbe
{
    /// <summary>Gets the keys passed to the local removal.</summary>
    public List<Guid> Removed { get; } = [];

    /// <summary>Gets how many times the list was reloaded.</summary>
    public int Reloads
    {
        get; private set;
    }

    /// <summary>Reads the list again.</summary>
    /// <returns>A completed task.</returns>
    public Task Reload()
    {
        Reloads++;
        return Task.CompletedTask;
    }

    /// <summary>Stands in for a mediator send that returns a <see cref="ValueTask{TResult}"/>.</summary>
    /// <param name="id">The row key.</param>
    /// <returns>A successful result.</returns>
    public static ValueTask<Result> Send(Guid id) => ValueTask.FromResult(Result.Ok());
}
