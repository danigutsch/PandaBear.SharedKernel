namespace SharedKernel.Aspire.Analyzers.Tests;

public sealed class SharedKernelAspireAnalyzerTests
{
    [Fact]
    public async Task With_image_tag_without_with_image_sha256_reports_ska_spire001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AppHost
            {
                public void Configure(dynamic builder)
                {
                    builder.AddPostgres("database").WithImageTag("18.4");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == AspireDiagnosticIds.ImageTagAndDigest);
        Assert.Contains("WithImageTag", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_image_tag_with_with_image_sha256_does_not_report_ska_spire001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AppHost
            {
                public void Configure(dynamic builder)
                {
                    builder.AddPostgres("database")
                        .WithImageTag("18.4")
                        .WithImageSHA256("4aabea78cf39b90e834caf3af7d602a18565f6fe2508705c8d01aa63245c2e20");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == AspireDiagnosticIds.ImageTagAndDigest);
    }

    [Fact]
    public async Task With_image_sha256_without_with_image_tag_reports_ska_spire001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AppHost
            {
                public void Configure(dynamic builder)
                {
                    builder.AddRedis("cache")
                        .WithImageSHA256("2838d5524559494f6f1cd66e97e76b200d64a633a8614200620755ed395daf32");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == AspireDiagnosticIds.ImageTagAndDigest);
        Assert.Contains("WithImageSHA256", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_image_sha256_value_with_sha256_prefix_reports_ska_spire001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AppHost
            {
                public void Configure(dynamic builder)
                {
                    builder.AddRedis("cache")
                        .WithImageTag("8.8")
                        .WithImageSHA256("sha256:2838d5524559494f6f1cd66e97e76b200d64a633a8614200620755ed395daf32");
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == AspireDiagnosticIds.ImageTagAndDigest);
        Assert.Contains("bare 64-character digest", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Companion_resource_image_pins_are_analyzed_independently()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AppHost
            {
                public void Configure(dynamic builder)
                {
                    builder.AddPostgres("database")
                        .WithImageTag("18.4")
                        .WithImageSHA256("4aabea78cf39b90e834caf3af7d602a18565f6fe2508705c8d01aa63245c2e20")
                        .WithPgWeb(pgweb => pgweb.WithImageSHA256("a5256d416e2e8b92d69a4459058e3eca33a9f075d8325491644411d0bc3bd70b"));
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        var diagnostic = Assert.Single(diagnostics, static candidate => candidate.Id == AspireDiagnosticIds.ImageTagAndDigest);
        Assert.Contains("WithImageSHA256", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Postgre_sql_redis_and_companion_image_pins_with_tags_do_not_report_ska_spire001()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AppHost
            {
                public void Configure(dynamic builder)
                {
                    builder.AddPostgres("database")
                        .WithImageTag("18.4")
                        .WithImageSHA256("4aabea78cf39b90e834caf3af7d602a18565f6fe2508705c8d01aa63245c2e20")
                        .WithPgWeb(pgweb => pgweb
                            .WithImageTag("0.17.0")
                            .WithImageSHA256("a5256d416e2e8b92d69a4459058e3eca33a9f075d8325491644411d0bc3bd70b"));

                    builder.AddRedis("cache")
                        .WithImageTag("8.8")
                        .WithImageSHA256("2838d5524559494f6f1cd66e97e76b200d64a633a8614200620755ed395daf32")
                        .WithRedisInsight(redisInsight => redisInsight
                            .WithImageTag("3.6")
                            .WithImageSHA256("aa21bbd198455b4ad964f76782db951155aa0d712321f599972d1525f031f0e6"));
                }
            }
            """;

        // Act
        var diagnostics = await AnalyzerTestHarness.GetAnalyzerDiagnostics(source);

        // Assert
        Assert.DoesNotContain(diagnostics, static candidate => candidate.Id == AspireDiagnosticIds.ImageTagAndDigest);
    }
}
