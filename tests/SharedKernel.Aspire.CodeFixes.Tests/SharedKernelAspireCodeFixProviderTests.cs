using SharedKernel.Aspire.Analyzers;
using SharedKernel.CodeFixes.Testing;

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
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText).ShouldContain("WithImageSHA256(REPLACE_WITH_VERIFIED_SHA256_DIGEST)", StringComparison.Ordinal);
        (updatedText).ShouldNotContain("WithImageSHA256(\"REPLACE_WITH_VERIFIED_SHA256_DIGEST\")", StringComparison.Ordinal);
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
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText).ShouldContain("WithImageTag(REPLACE_WITH_VERIFIED_IMAGE_TAG)", StringComparison.Ordinal);
        (updatedText).ShouldNotContain("WithImageTag(\"REPLACE_WITH_VERIFIED_IMAGE_TAG\")", StringComparison.Ordinal);
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
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText).ShouldContain("WithImageSHA256(\"2838d5524559494f6f1cd66e97e76b200d64a633a8614200620755ed395daf32\")", StringComparison.Ordinal);
        (updatedText).ShouldNotContain("sha256:", StringComparison.Ordinal);
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
        (codeActions).ShouldContain(static action => string.Equals(action.Title, "Remove sha256: prefix from verified digest", StringComparison.Ordinal));
        (codeActions).ShouldContain(static action => string.Equals(action.Title, "Insert placeholder to replace with verified image tag", StringComparison.Ordinal));
    }

    [Fact]
    public void Fix_all_is_not_advertised_for_aspire_placeholder_fixes()
    {
        // Arrange
        var provider = new SharedKernelAspireCodeFixProvider();

        // Act
        var fixAllProvider = provider.GetFixAllProvider();

        // Assert
        (fixAllProvider).ShouldBeNull();
    }

    [Fact]
    public void Fixable_diagnostic_ids_match_registered_aspire_fixes()
    {
        // Arrange
        var provider = new SharedKernelAspireCodeFixProvider();

        // Act
        var diagnosticIds = provider.FixableDiagnosticIds.ToArray();

        // Assert
        (diagnosticIds).ShouldBe([
                AspireDiagnosticIds.ImageTagAndDigest
            ]);
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
        var codeAction = (codeActions).ShouldHaveSingleItem();
        (codeAction.Title).ShouldBe("Remove sha256: prefix from verified digest");
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
        var codeAction = (await workspace.GetCodeActions(provider, diagnostic)).ShouldHaveSingleItem();
        await workspace.ApplyCodeAction(codeAction);
        var updatedText = await workspace.GetDocumentText();

        // Assert
        (updatedText).ShouldContain(".WithDataVolume().WithImageSHA256(REPLACE_WITH_VERIFIED_SHA256_DIGEST)", StringComparison.Ordinal);
    }
}
