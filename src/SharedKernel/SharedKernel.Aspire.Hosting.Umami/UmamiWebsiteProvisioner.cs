using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Sets up a website in a running Umami and reports it on the website resource: <c>Starting</c>, then <c>Running</c>, or
/// <c>FailedToStart</c> with a log line that names the request and Umami's status code. A failure is reported, never thrown, so it stops
/// no other resource; the password and the token reach no log or state.
/// </summary>
/// <param name="notifications">The resource state service.</param>
/// <param name="loggers">The resource log service.</param>
/// <param name="clients">The factory of the HTTP client named <see cref="HttpClientName"/>.</param>
internal sealed class UmamiWebsiteProvisioner(ResourceNotificationService notifications, ResourceLoggerService loggers, IHttpClientFactory clients)
{
    /// <summary>The name of the HTTP client that talks to Umami, whose logged header values are redacted.</summary>
    public const string HttpClientName = "PandaBear.Aspire.Hosting.Umami";

    /// <summary>The password Umami gives its <c>admin</c> user on its first start.</summary>
    public const string DefaultAdminPassword = "umami";

    /// <summary>The resource property that shows the website id.</summary>
    public const string WebsiteIdProperty = "umami.website.id";

    /// <summary>The resource property that shows the website's domain.</summary>
    public const string DomainProperty = "umami.website.domain";

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(1);

    /// <summary>Sets up <paramref name="website"/> and reports the outcome; it never throws.</summary>
    /// <param name="website">The website.</param>
    /// <param name="ct">The token that is cancelled when Umami or the AppHost stops; provisioning then ends without a report.</param>
    /// <returns>A task that completes when the website is set up or the failure is reported.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failed setup must not stop other resources; it is reported with the exception's type name.")]
    public async Task Provision(UmamiWebsiteResource website, CancellationToken ct)
    {
        var logger = loggers.GetLogger(website);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        try
        {
            await Publish(website, KnownResourceStates.Starting).ConfigureAwait(false);
            var url = await website.Umami.PrimaryEndpoint.GetValueAsync(timeout.Token).ConfigureAwait(false);
            var endpoint = new Uri(url!, UriKind.Absolute);
            var password = website.Umami.AdminPasswordParameter is { } parameter
                ? await parameter.GetValueAsync(timeout.Token).ConfigureAwait(false)
                : DefaultAdminPassword;
            UmamiWebsiteLog.SettingUp(logger, endpoint, UmamiApiClient.AdminUsername);
            using var client = clients.CreateClient(HttpClientName);
            var outcome = await new UmamiApiClient(client, endpoint)
                .EnsureWebsite(password ?? string.Empty, website.WebsiteId, website.Name, website.Domain, timeout.Token)
                .ConfigureAwait(false);
            if (outcome == UmamiWebsiteOutcome.Created)
            {
                UmamiWebsiteLog.Created(logger, website.Name, website.Domain, website.WebsiteId);
            }
            else
            {
                UmamiWebsiteLog.AlreadyExists(logger, website.Name, website.WebsiteId);
            }

            await Publish(website, KnownResourceStates.Running).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (UmamiProvisioningException exception)
        {
            if (exception.StatusCode is { } statusCode)
            {
                UmamiWebsiteLog.Refused(logger, exception.Step, statusCode);
            }
            else
            {
                UmamiWebsiteLog.Failed(logger, exception.Step, exception.Reason ?? exception.GetType().Name);
            }

            if (exception is { Step: UmamiProvisioningStep.SignIn, StatusCode: 401 })
            {
                UmamiWebsiteLog.PasswordRefused(logger);
            }

            await Publish(website, KnownResourceStates.FailedToStart).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or AccessViolationException))
        {
            UmamiWebsiteLog.Unexpected(logger, exception.GetType().FullName ?? exception.GetType().Name);
            await Publish(website, KnownResourceStates.FailedToStart).ConfigureAwait(false);
        }
    }

    private Task Publish(UmamiWebsiteResource website, string state) =>
        notifications.PublishUpdateAsync(website, snapshot => snapshot with
        {
            State = state,
            Properties = WithWebsite(snapshot.Properties, website),
        });

    private static ImmutableArray<ResourcePropertySnapshot> WithWebsite(ImmutableArray<ResourcePropertySnapshot> properties, UmamiWebsiteResource website) =>
    [
        .. properties.Where(static property => property.Name is not (WebsiteIdProperty or DomainProperty)),
        new ResourcePropertySnapshot(WebsiteIdProperty, website.WebsiteId.ToString("D", CultureInfo.InvariantCulture)),
        new ResourcePropertySnapshot(DomainProperty, website.Domain),
    ];
}
