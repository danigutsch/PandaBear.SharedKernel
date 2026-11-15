using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// The queues of every target. The targets are created on first use, after the host checked the settings at start, and each page view
/// is put on every queue, so one target's full queue never holds back another.
/// </summary>
internal sealed class PageViewQueues(IEnumerable<PageViewTargetRegistration> registrations, IServiceProvider services, IOptions<PageViewOptions> options, PageViewMetrics metrics)
{
    private readonly Lazy<IReadOnlyList<PageViewTargetQueue>> _all = new(() => Create(registrations, services, options.Value.QueueCapacity, metrics));

    /// <summary>Gets the queue of every target.</summary>
    /// <exception cref="InvalidOperationException">A target has a blank name, or two targets have the same name.</exception>
    public IReadOnlyList<PageViewTargetQueue> All => _all.Value;

    /// <summary>Puts a page view on the queue of every target without waiting.</summary>
    /// <param name="pageView">The page view.</param>
    public void Enqueue(PageView pageView)
    {
        foreach (var queue in All)
        {
            queue.Enqueue(pageView);
        }
    }

    /// <summary>Closes every queue.</summary>
    public void Close()
    {
        foreach (var queue in All)
        {
            queue.Close();
        }
    }

    private static PageViewTargetQueue[] Create(IEnumerable<PageViewTargetRegistration> registrations, IServiceProvider services, int capacity, PageViewMetrics metrics)
    {
        var queues = registrations.Select(registration => new PageViewTargetQueue(registration.Create(services), capacity, metrics)).ToArray();
        if (queues.FirstOrDefault(static queue => string.IsNullOrWhiteSpace(queue.Name)) is { } unnamed)
        {
            throw new InvalidOperationException($"The page-view target {unnamed.Target.GetType().Name} has a blank name; give every target a name.");
        }

        if (queues.GroupBy(static queue => queue.Name, StringComparer.Ordinal).FirstOrDefault(static names => names.Count() > 1) is { } duplicate)
        {
            throw new InvalidOperationException($"Two page-view targets are named {duplicate.Key}; give every target a name of its own.");
        }

        return queues;
    }
}
