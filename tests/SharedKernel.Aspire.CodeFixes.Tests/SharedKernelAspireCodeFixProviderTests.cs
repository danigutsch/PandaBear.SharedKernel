using SharedKernel.Aspire.Analyzers;
using SharedKernel.Style.CodeFixes.Tests;

namespace SharedKernel.Aspire.CodeFixes.Tests;

public sealed class SharedKernelAspireCodeFixProviderTests
{
    [Fact]
    public async Task Aspire_image_tag_fix_adds_uncompilable_digest_placeholder()
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
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelAspireCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            AspireDiagnosticIds.ImageTagAndDigest,
            "WithImageTag(\"18.4\")");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains("WithImageSHA256(REPLACE_WITH_VERIFIED_SHA256_DIGEST)", updatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("WithImageSHA256(\"REPLACE_WITH_VERIFIED_SHA256_DIGEST\")", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aspire_image_digest_fix_adds_uncompilable_tag_placeholder()
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
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelAspireCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            AspireDiagnosticIds.ImageTagAndDigest,
            "WithImageSHA256");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains("WithImageTag(REPLACE_WITH_VERIFIED_IMAGE_TAG)", updatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("WithImageTag(\"REPLACE_WITH_VERIFIED_IMAGE_TAG\")", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aspire_prefixed_digest_fix_removes_sha256_prefix()
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
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelAspireCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            AspireDiagnosticIds.ImageTagAndDigest,
            "sha256:2838d5524559494f6f1cd66e97e76b200d64a633a8614200620755ed395daf32");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains("WithImageSHA256(\"2838d5524559494f6f1cd66e97e76b200d64a633a8614200620755ed395daf32\")", updatedText, StringComparison.Ordinal);
        Assert.DoesNotContain("sha256:", updatedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aspire_prefixed_digest_fix_still_offers_missing_tag_placeholder()
    {
        // Arrange
        const string source = """
            namespace Demo;

            public sealed class AppHost
            {
                public void Configure(dynamic builder)
                {
                    builder.AddRedis("cache")
                        .WithImageSHA256("sha256:2838d5524559494f6f1cd66e97e76b200d64a633a8614200620755ed395daf32");
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelAspireCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            AspireDiagnosticIds.ImageTagAndDigest,
            "sha256:2838d5524559494f6f1cd66e97e76b200d64a633a8614200620755ed395daf32");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        Assert.Contains(codeActions, static action => string.Equals(action.Title, "Remove sha256: prefix from verified digest", StringComparison.Ordinal));
        Assert.Contains(codeActions, static action => string.Equals(action.Title, "Insert placeholder to replace with verified image tag", StringComparison.Ordinal));
    }

    [Fact]
    public void Fix_all_is_not_advertised_for_aspire_placeholder_fixes()
    {
        // Arrange
        var provider = new SharedKernelAspireCodeFixProvider();

        // Act
        var fixAllProvider = provider.GetFixAllProvider();

        // Assert
        Assert.Null(fixAllProvider);
    }

    [Fact]
    public void Fixable_diagnostic_ids_match_registered_aspire_fixes()
    {
        // Arrange
        var provider = new SharedKernelAspireCodeFixProvider();

        // Act
        var diagnosticIds = provider.FixableDiagnosticIds.ToArray();

        // Assert
        Assert.Equal(
            [
                AspireDiagnosticIds.ImageTagAndDigest
            ],
            diagnosticIds);
    }

    [Fact]
    public async Task Aspire_prefixed_digest_with_existing_tag_only_offers_prefix_removal()
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
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelAspireCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            AspireDiagnosticIds.ImageTagAndDigest,
            "sha256:2838d5524559494f6f1cd66e97e76b200d64a633a8614200620755ed395daf32");

        // Act
        var codeActions = await workspace.GetCodeActions(provider, diagnostic);

        // Assert
        var codeAction = Assert.Single(codeActions);
        Assert.Equal("Remove sha256: prefix from verified digest", codeAction.Title);
    }

    [Fact]
    public async Task Aspire_missing_digest_fix_appends_after_outermost_chain_call()
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
                        .WithDataVolume();
                }
            }
            """;
        var workspace = CodeFixTestWorkspace.Create(source);
        var provider = new SharedKernelAspireCodeFixProvider();
        var diagnostic = await workspace.CreateDocumentDiagnostic(
            AspireDiagnosticIds.ImageTagAndDigest,
            "WithImageTag(\"18.4\")");

        // Act
        var codeAction = Assert.Single(await workspace.GetCodeActions(provider, diagnostic));
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        Assert.Contains(".WithDataVolume().WithImageSHA256(REPLACE_WITH_VERIFIED_SHA256_DIGEST)", updatedText, StringComparison.Ordinal);
    }
}
