namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>One target added through <see cref="PageViewBuilder"/>, created when the page views first need it, after the settings were checked.</summary>
/// <param name="create">Creates the target from the host's services.</param>
internal sealed class PageViewTargetRegistration(Func<IServiceProvider, IPageViewTarget> create)
{
    /// <summary>Creates the target.</summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The target.</returns>
    public IPageViewTarget Create(IServiceProvider services) => create(services);
}
