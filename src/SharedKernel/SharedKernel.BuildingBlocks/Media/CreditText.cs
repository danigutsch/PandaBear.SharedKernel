using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>The link rule of image credits: an optional https link whose errors never echo the input.</summary>
internal static class CreditText
{
    /// <summary>Validates an optional https link; blank is <see langword="null"/>.</summary>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="value">The link.</param>
    /// <param name="field">The field path.</param>
    /// <param name="label">The label in messages.</param>
    /// <returns>The link, or <see langword="null"/>.</returns>
    public static WebUrl? HttpsUrl(ValidationErrors errors, string? value, string field, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var url = WebUrl.CreateHttps(value, field, label);
        errors.AddIfFailed(url);
        return url.IsSuccess ? url.Value : null;
    }
}
