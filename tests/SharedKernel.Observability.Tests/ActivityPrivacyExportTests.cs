using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace PandaBear.Observability.Tests;

[Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
public sealed class ActivityPrivacyExportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Trace_export_removes_sensitive_http_database_object_storage_and_messaging_fields(bool upperCase)
    {
        // Arrange
        string[] sensitiveAttributes =
        [
            "authorization", "commandText", "cookie", "email", "enduser.id",
            "error.message", "exception.message", "exception.stacktrace",
            "http.request.body", "http.response.body", "http.target", "http.url", "http.user_agent",
            "db.connection_string", "db.npgsql.connection_id", "db.npgsql.data_source", "db.parameters",
            "db.query.parameter", "db.query.parameters", "db.query.text", "db.statement",
            "media.object.key", "media.object_key", "object.key", "ObjectKey", "parameters",
            "Path", "PathBase", "QueryString", "RequestBody", "RequestPath", "RequestUri", "ResponseBody",
            "set-cookie", "Uri", "url.full", "url.path", "url.query", "user_agent.original", "user.id",
            "aws.extended_request_id", "aws.request_id", "aws.s3.bucket", "aws.s3.bucket.name",
            "aws.s3.copy_source", "aws.s3.delete", "aws.s3.key", "aws.s3.part_number", "aws.s3.upload_id",
            "aws.sns.topic.arn", "aws.sns.topic.name", "aws.sqs.queue.name", "aws.sqs.queue.url",
            "messaging.destination.name", "messaging.destination.subscription.name", "messaging.message.id",
            "db.query.parameter.contact", "http.request.header.authorization", "http.response.header.set-cookie"
        ];
        var attributeNames = sensitiveAttributes.Select(name => upperCase ? name.ToUpperInvariant() : name).ToArray();
        var exported = new ConcurrentQueue<Activity>();
        using var source = new ActivitySource($"privacy-test-{Guid.NewGuid():N}");
        using var exporter = new PrivacyActivityExporter(exported);
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddProcessor(new ActivityPrivacyProcessor())
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        // Act
        using (var activity = source.StartActivity("operation").ShouldNotBeNull())
        {
            foreach (var name in attributeNames)
            {
                activity.SetTag(name, "private@example.test");
            }

            activity.SetTag("http.route", "/records/{id}");
            activity.SetTag("http.request.method", "GET");
            activity.SetTag("http.response.status_code", 200);
            activity.SetTag("server.address", "example.test");
            activity.SetTag("aws.service", "S3");
            activity.SetTag("aws.operation", "PutObject");
        }

        provider.ForceFlush();

        // Assert
        var result = exported.ShouldHaveSingleItem();
        foreach (var name in attributeNames)
        {
            result.GetTagItem(name).ShouldBeNull();
        }

        result.GetTagItem("http.route").ShouldBe("/records/{id}");
        result.GetTagItem("http.request.method").ShouldBe("GET");
        result.GetTagItem("http.response.status_code").ShouldBe(200);
        result.GetTagItem("server.address").ShouldBe("example.test");
        result.GetTagItem("aws.service").ShouldBe("S3");
        result.GetTagItem("aws.operation").ShouldBe("PutObject");
    }

    [Fact]
    public void Trace_export_scrubs_exception_objects_and_status_description_while_bounding_error_types()
    {
        // Arrange
        var exported = new ConcurrentQueue<Activity>();
        using var source = new ActivitySource($"privacy-test-{Guid.NewGuid():N}");
        using var exporter = new PrivacyActivityExporter(exported);
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddProcessor(new ActivityPrivacyProcessor())
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        // Act
        using (var activity = source.StartActivity("operation").ShouldNotBeNull())
        {
            activity.SetTag("diagnostic.failure", new InvalidOperationException("private@example.test"));
            activity.SetTag("ERROR.TYPE", new string('e', 257));
            activity.SetTag("EXCEPTION.TYPE", new string('x', 257));
            activity.SetTag("operation.result", "failed");
            activity.SetStatus(ActivityStatusCode.Error, "private@example.test");
        }

        provider.ForceFlush();

        // Assert
        var result = exported.ShouldHaveSingleItem();
        result.Status.ShouldBe(ActivityStatusCode.Error);
        result.StatusDescription.ShouldBeNull();
        result.GetTagItem("diagnostic.failure").ShouldBeNull();
        result.GetTagItem("ERROR.TYPE").ShouldBe(new string('e', 256));
        result.GetTagItem("EXCEPTION.TYPE").ShouldBe(new string('x', 256));
        result.GetTagItem("operation.result").ShouldBe("failed");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Trace_export_redacts_urls_without_requiring_aws_or_bucket_attributes(bool awsAttributes)
    {
        // Arrange
        var exported = new ConcurrentQueue<Activity>();
        using var source = new ActivitySource($"privacy-test-{Guid.NewGuid():N}");
        using var exporter = new PrivacyActivityExporter(exported);
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddProcessor(new ActivityPrivacyProcessor())
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        // Act
        using (var activity = source.StartActivity("credentials").ShouldNotBeNull())
        {
            if (awsAttributes)
            {
                activity.SetTag("aws.request_id", "private-request");
                activity.SetTag("aws.extended_request_id", "private-extended-request");
            }

            activity.SetTag("url.full", "https://storage.example.test/private");
            activity.SetTag("operation.result", "completed");
        }

        provider.ForceFlush();

        // Assert
        var result = exported.ShouldHaveSingleItem();
        result.GetTagItem("aws.request_id").ShouldBeNull();
        result.GetTagItem("aws.extended_request_id").ShouldBeNull();
        result.GetTagItem("url.full").ShouldBeNull();
        result.GetTagItem("operation.result").ShouldBe("completed");
    }
}
