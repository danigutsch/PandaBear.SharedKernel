using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>The lines that setting up a website writes to the website resource's log. None carries a password or a token.</summary>
internal static partial class UmamiWebsiteLog
{
    /// <summary>Logs that provisioning starts.</summary>
    /// <param name="logger">The resource logger.</param>
    /// <param name="endpoint">Umami's base address.</param>
    /// <param name="username">The user to sign in as.</param>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Setting up the website in Umami at {Endpoint}, signing in as {Username}.")]
    public static partial void SettingUp(ILogger logger, Uri endpoint, string username);

    /// <summary>Logs that the website was created.</summary>
    /// <param name="logger">The resource logger.</param>
    /// <param name="website">The website name.</param>
    /// <param name="domain">The website's domain.</param>
    /// <param name="websiteId">The website id.</param>
    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Created the website {Website} for {Domain} with the id {WebsiteId}.")]
    public static partial void Created(ILogger logger, string website, string domain, Guid websiteId);

    /// <summary>Logs that the website existed.</summary>
    /// <param name="logger">The resource logger.</param>
    /// <param name="website">The website name.</param>
    /// <param name="websiteId">The website id.</param>
    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "The website {Website} with the id {WebsiteId} exists already; nothing was changed.")]
    public static partial void AlreadyExists(ILogger logger, string website, Guid websiteId);

    /// <summary>Logs that Umami refused a request.</summary>
    /// <param name="logger">The resource logger.</param>
    /// <param name="step">The request.</param>
    /// <param name="statusCode">The status code of Umami's answer.</param>
    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Setting up the website failed at {Step}: Umami answered with status {StatusCode}.")]
    public static partial void Refused(ILogger logger, UmamiProvisioningStep step, int statusCode);

    /// <summary>Logs that a request failed for another reason.</summary>
    /// <param name="logger">The resource logger.</param>
    /// <param name="step">The request.</param>
    /// <param name="reason">Why it failed, without any value Umami returned.</param>
    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Setting up the website failed at {Step}: {Reason}.")]
    public static partial void Failed(ILogger logger, UmamiProvisioningStep step, string reason);

    /// <summary>Logs how to fix a refused sign-in.</summary>
    /// <param name="logger">The resource logger.</param>
    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Umami refused the admin password. When the password of a kept Umami was changed, pass it with WithAdminPassword.")]
    public static partial void PasswordRefused(ILogger logger);

    /// <summary>Logs that provisioning failed with an unexpected error.</summary>
    /// <param name="logger">The resource logger.</param>
    /// <param name="errorType">The type name of the exception, never its message.</param>
    [LoggerMessage(EventId = 7, Level = LogLevel.Error, Message = "Setting up the website failed with {ErrorType}.")]
    public static partial void Unexpected(ILogger logger, string errorType);
}
