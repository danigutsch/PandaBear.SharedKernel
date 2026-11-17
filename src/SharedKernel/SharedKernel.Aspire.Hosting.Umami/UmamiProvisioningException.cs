using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting;

/// <summary>
/// Setting up a website in Umami failed at <see cref="Step"/>. The message names the step and the status code or the error's type name
/// only, never a password, a token or a response body.
/// </summary>
[SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "It never leaves this package: the provisioner catches it and reports the step and status on the website resource.")]
internal sealed class UmamiProvisioningException : Exception
{
    /// <summary>Initializes a failure without a step.</summary>
    public UmamiProvisioningException()
    {
    }

    /// <summary>Initializes a failure without a step.</summary>
    /// <param name="message">The message.</param>
    public UmamiProvisioningException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a failure without a step.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public UmamiProvisioningException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a failure that Umami answered with a status other than success.</summary>
    /// <param name="step">The request.</param>
    /// <param name="statusCode">The status code of the answer.</param>
    public UmamiProvisioningException(UmamiProvisioningStep step, int statusCode)
        : base($"Umami answered {step} with status {statusCode}.")
    {
        Step = step;
        StatusCode = statusCode;
    }

    /// <summary>Initializes a failure for another reason.</summary>
    /// <param name="step">The request.</param>
    /// <param name="reason">Why it failed, without any value Umami returned.</param>
    public UmamiProvisioningException(UmamiProvisioningStep step, string reason)
        : base($"{step} failed: {reason}.")
    {
        Step = step;
        Reason = reason;
    }

    /// <summary>Gets the request that failed.</summary>
    public UmamiProvisioningStep Step { get; }

    /// <summary>Gets the status code of Umami's answer, or <see langword="null"/> when the request failed otherwise.</summary>
    public int? StatusCode { get; }

    /// <summary>Gets why the request failed when Umami did not refuse it, or <see langword="null"/>.</summary>
    public string? Reason { get; }
}
