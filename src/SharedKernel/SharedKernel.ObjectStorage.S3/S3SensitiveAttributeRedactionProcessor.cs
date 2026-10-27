using System.Diagnostics;
using OpenTelemetry;

namespace PandaBear.ObjectStorage.S3;

/// <summary>
/// Removes bucket, key, request-id and URL attributes from AWS SDK spans.
/// </summary>
internal sealed class S3SensitiveAttributeRedactionProcessor : BaseProcessor<Activity>
{
    private static string[] SensitiveAttributes
    {
        get;
    } =
    [
        "aws.s3.bucket",
        "aws.s3.bucket.name",
        "aws.s3.key",
        "aws.s3.copy_source",
        "aws.s3.delete",
        "aws.s3.upload_id",
        "aws.s3.part_number",
        "aws.request_id",
        "aws.extended_request_id",
        "url.full",
        "http.url"
    ];

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!data.TagObjects.Any(static attribute =>
                attribute.Key.StartsWith("aws.", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        foreach (var attribute in SensitiveAttributes)
        {
            data.SetTag(attribute, null);
        }
    }
}
