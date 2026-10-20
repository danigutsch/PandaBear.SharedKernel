using System.Text;

namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection("Repo config tool environment")]
public sealed class TextEncodingCommandTests
{
    [Fact]
    public async Task Help_lists_the_text_encoding_command()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();

        // Act
        var result = await repository.RunCommand(["--help"], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldContain("text-encoding", StringComparison.Ordinal);
        result.StandardError.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Text_encoding_honors_root_and_accepts_valid_utf8_baseline()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteBytes("empty.txt", []);
        repository.WriteText("plain.txt", "plain UTF-8\n");
        repository.WriteBytes("bom.txt", [0xEF, 0xBB, 0xBF, .. "valid UTF-8\n"u8]);
        repository.WriteText("folder/viagem ç com espaço.txt", "Olá!\n");
        await repository.Stage("empty.txt", TestContext.Current.CancellationToken);
        await repository.Stage("plain.txt", TestContext.Current.CancellationToken);
        await repository.Stage("bom.txt", TestContext.Current.CancellationToken);
        await repository.Stage("folder/viagem ç com espaço.txt", TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken,
            Path.GetTempPath());

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldContain("Repository text encoding is valid.", StringComparison.Ordinal);
        result.StandardError.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Text_encoding_rejects_extra_arguments()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "extra", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.StandardError.ShouldContain("Usage:", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("utf16le-bom")]
    [InlineData("utf16be-bom")]
    [InlineData("utf16le")]
    [InlineData("utf16be")]
    [InlineData("utf8-nul")]
    public async Task Text_encoding_rejects_nul_containing_text(string name)
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        var content = name switch
        {
            "utf16le-bom" => [.. Encoding.Unicode.Preamble, .. Encoding.Unicode.GetBytes("text\n")],
            "utf16be-bom" => [.. Encoding.BigEndianUnicode.Preamble, .. Encoding.BigEndianUnicode.GetBytes("text\n")],
            "utf16le" => new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true).GetBytes("text\n"),
            "utf16be" => new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true).GetBytes("text\n"),
            _ => "valid\0text\n"u8.ToArray()
        };
        var relativePath = $"{name}.txt";
        repository.WriteBytes(relativePath, content);
        await repository.Stage(relativePath, TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain($"{relativePath}: Contains NUL byte.", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain(repository.RootPath, StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("text\n", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_encoding_rejects_invalid_utf8_without_leaking_content_or_root()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteBytes("invalid.txt", [0xFF, .. "DO_NOT_PRINT_THIS_CONTENT"u8]);
        await repository.Stage("invalid.txt", TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("invalid.txt: Is not valid UTF-8.", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("DO_NOT_PRINT_THIS_CONTENT", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain(repository.RootPath, StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("System.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_encoding_skips_only_exact_binary_set_from_index_attributes()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteText(".gitattributes", "binary.dat binary\n");
        repository.WriteBytes("binary.dat", [0x00, 0xFF]);
        await repository.Stage(".gitattributes", TestContext.Current.CancellationToken);
        await repository.Stage("binary.dat", TestContext.Current.CancellationToken);
        repository.WriteText(".gitattributes", string.Empty);

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardError.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("packages.lock.json merge=binary", "packages.lock.json")]
    [InlineData("assigned.dat binary=custom", "assigned.dat")]
    [InlineData("unset.dat -binary", "unset.dat")]
    public async Task Text_encoding_scans_every_binary_value_except_set(string attribute, string relativePath)
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteText(".gitattributes", attribute + "\n");
        repository.WriteBytes(relativePath, [0xFF, .. "PRIVATE_ATTRIBUTE_CONTENT"u8]);
        await repository.Stage(".gitattributes", TestContext.Current.CancellationToken);
        await repository.Stage(relativePath, TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain($"{relativePath}: Is not valid UTF-8.", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("PRIVATE_ATTRIBUTE_CONTENT", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_encoding_sanitizes_special_path_diagnostics_to_one_line()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        const string relativePath = "-viagem ç\n\t\u001B.txt";
        repository.WriteBytes(relativePath, [0xFF, .. "PATH_SENTINEL"u8]);
        await repository.Stage(relativePath, TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);
        var lines = result.StandardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        result.ExitCode.ShouldBe(1);
        lines.Length.ShouldBe(2);
        result.StandardError.ShouldContain("-viagem ç\\u000A\\u0009\\u001B.txt: Is not valid UTF-8.", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("PATH_SENTINEL", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain(repository.RootPath, StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("\u001B", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_encoding_escapes_unicode_format_and_separator_characters()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        const string relativePath = "format-\u2028-\u202E-\U0001BCA0.txt";
        repository.WriteBytes(relativePath, [0xFF]);
        await repository.Stage(relativePath, TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("format-\\u2028-\\u202E-\\U0001BCA0.txt", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("\u2028", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("\u202E", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("\U0001BCA0", StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnostic_sanitizer_escapes_malformed_surrogate_code_units()
    {
        // Act
        var escaped = RepoConfigToolApplication.EscapeControlCharacters("before\uD800middle\uDC00after");

        // Assert
        escaped.ShouldBe("before\\uD800middle\\uDC00after");
    }

    [Fact]
    public async Task Text_encoding_ignores_alternate_git_directory_routing()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        using var alternate = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        await alternate.Initialize(TestContext.Current.CancellationToken);
        repository.WriteBytes("invalid.txt", [0xFF]);
        await repository.Stage("invalid.txt", TestContext.Current.CancellationToken);
        alternate.WriteText("valid.txt", "valid\n");
        await alternate.Stage("valid.txt", TestContext.Current.CancellationToken);
        Dictionary<string, string?> environment = new(StringComparer.Ordinal)
        {
            ["GIT_DIR"] = Path.Combine(alternate.RootPath, ".git"),
            ["GIT_WORK_TREE"] = alternate.RootPath
        };

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken,
            environment: environment);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("invalid.txt: Is not valid UTF-8.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_encoding_ignores_alternate_index_routing()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteText("content.txt", "valid\n");
        await repository.Stage("content.txt", TestContext.Current.CancellationToken);
        var alternateIndex = repository.CopyIndex("alternate-index");
        repository.WriteBytes("content.txt", [0xFF]);
        await repository.Stage("content.txt", TestContext.Current.CancellationToken);
        Dictionary<string, string?> environment = new(StringComparer.Ordinal)
        {
            ["GIT_INDEX_FILE"] = alternateIndex
        };

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken,
            environment: environment);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("content.txt: Is not valid UTF-8.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_encoding_ignores_global_binary_attributes()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteBytes("invalid.txt", [0xFF]);
        await repository.Stage("invalid.txt", TestContext.Current.CancellationToken);
        var globalConfig = repository.CreateGlobalAttributesConfig("* binary\n");
        Dictionary<string, string?> environment = new(StringComparer.Ordinal)
        {
            ["GIT_CONFIG_GLOBAL"] = globalConfig
        };

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken,
            environment: environment);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("invalid.txt: Is not valid UTF-8.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_encoding_ignores_original_info_attributes()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteBytes("invalid.txt", [0xFF]);
        await repository.Stage("invalid.txt", TestContext.Current.CancellationToken);
        repository.WriteInfoAttributes("* binary\n");

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("invalid.txt: Is not valid UTF-8.", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain(repository.RootPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_encoding_ignores_blob_replacement_refs()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteBytes("invalid.txt", [0xFF]);
        await repository.Stage("invalid.txt", TestContext.Current.CancellationToken);
        var indexEntry = await repository.RunGit(
            ["ls-files", "--stage", "--", "invalid.txt"],
            TestContext.Current.CancellationToken);
        var invalidObjectId = indexEntry.StandardOutput.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
        repository.WriteText("valid-replacement.txt", "valid\n");
        var validObjectId = await repository.StoreBlob("valid-replacement.txt", TestContext.Current.CancellationToken);
        var replaceResult = await repository.RunGit(
            ["replace", invalidObjectId, validObjectId],
            TestContext.Current.CancellationToken);
        replaceResult.ExitCode.ShouldBe(0);

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("invalid.txt: Is not valid UTF-8.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_encoding_supports_sha256_repositories_when_git_supports_them()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        var supported = await repository.TryInitializeSha256(TestContext.Current.CancellationToken);
        if (!supported)
        {
            return;
        }

        repository.WriteBytes("invalid.txt", [0xFF]);
        await repository.Stage("invalid.txt", TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("invalid.txt: Is not valid UTF-8.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_encoding_reads_split_index_with_its_shared_index()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteText("valid.txt", "valid\n");
        await repository.Stage("valid.txt", TestContext.Current.CancellationToken);
        var splitResult = await repository.RunGit(
            ["update-index", "--split-index"],
            TestContext.Current.CancellationToken);
        var sharedIndexResult = await repository.RunGit(
            ["rev-parse", "--shared-index-path"],
            TestContext.Current.CancellationToken);
        var sharedIndexPath = sharedIndexResult.StandardOutput.Trim();
        splitResult.ExitCode.ShouldBe(0);
        sharedIndexResult.ExitCode.ShouldBe(0);
        sharedIndexPath.ShouldNotBe(string.Empty);

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardError.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Text_encoding_preserves_repository_declared_object_alternates()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        using var alternate = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        await alternate.Initialize(TestContext.Current.CancellationToken);
        alternate.WriteBytes("alternate-invalid.txt", [0xFF]);
        var objectId = await alternate.StoreBlob("alternate-invalid.txt", TestContext.Current.CancellationToken);
        var alternatesPath = Path.Combine(repository.RootPath, ".git", "objects", "info", "alternates");
        var alternateObjectsPath = Path.Combine(alternate.RootPath, ".git", "objects");
        await File.WriteAllTextAsync(
            alternatesPath,
            alternateObjectsPath + "\n",
            TestContext.Current.CancellationToken);
        await repository.SetIndexEntry("100644", objectId, "alternate-invalid.txt", TestContext.Current.CancellationToken);
        var alternateBlob = await repository.RunGit(["cat-file", "-t", objectId], TestContext.Current.CancellationToken);
        alternateBlob.StandardError.ShouldBe(string.Empty);
        alternateBlob.ExitCode.ShouldBe(0);
        alternateBlob.StandardOutput.Trim().ShouldBe("blob");

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("alternate-invalid.txt: Is not valid UTF-8.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_encoding_ignores_caller_provided_object_alternates()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        using var alternate = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        await alternate.Initialize(TestContext.Current.CancellationToken);
        repository.WriteBytes("invalid.txt", [0xFF]);
        alternate.WriteBytes("invalid.txt", [0xFF]);
        await repository.Stage("invalid.txt", TestContext.Current.CancellationToken);
        var objectId = await alternate.StoreBlob("invalid.txt", TestContext.Current.CancellationToken);
        repository.DeleteLooseObject(objectId);
        Dictionary<string, string?> environment = new(StringComparer.Ordinal)
        {
            ["GIT_ALTERNATE_OBJECT_DIRECTORIES"] = Path.Combine(alternate.RootPath, ".git", "objects")
        };

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken,
            environment: environment);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("Git blob inspection failed.", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain(objectId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_encoding_uses_the_linked_worktree_index()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteText("content.txt", "valid in the main worktree\n");
        await repository.Stage("content.txt", TestContext.Current.CancellationToken);
        var commitResult = await repository.RunGit(
            ["-c", "user.name=Encoding Tests", "-c", "user.email=encoding-tests@example.invalid", "commit", "--quiet", "-m", "baseline"],
            TestContext.Current.CancellationToken);
        commitResult.ExitCode.ShouldBe(0);
        var linkedRoot = await repository.CreateLinkedWorktree(TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(linkedRoot, "content.txt"),
            [0xFF],
            TestContext.Current.CancellationToken);
        var stageResult = await repository.RunGit(
            ["-C", linkedRoot, "add", "--", "content.txt"],
            TestContext.Current.CancellationToken);
        stageResult.ExitCode.ShouldBe(0);

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", linkedRoot],
            TestContext.Current.CancellationToken,
            linkedRoot);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("content.txt: Is not valid UTF-8.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_encoding_skips_symlink_index_entries_without_reading_the_blob()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteBytes("invalid-link-source.bin", [0x00, 0xFF]);
        var objectId = await repository.StoreBlob("invalid-link-source.bin", TestContext.Current.CancellationToken);
        repository.Delete("invalid-link-source.bin");
        await repository.SetIndexEntry("120000", objectId, "link.txt", TestContext.Current.CancellationToken);

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardError.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Text_encoding_reads_staged_content_not_working_tree_content()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteText("staged.txt", "valid staged content\n");
        await repository.Stage("staged.txt", TestContext.Current.CancellationToken);
        repository.WriteBytes("staged.txt", [0xFF, .. "UNSTAGED_SENTINEL"u8]);

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardError.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Text_encoding_rejects_invalid_staged_content_even_when_working_tree_is_valid()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteBytes("staged-invalid.txt", [0xFF, .. "STAGED_SENTINEL"u8]);
        await repository.Stage("staged-invalid.txt", TestContext.Current.CancellationToken);
        repository.WriteText("staged-invalid.txt", "valid working tree content\n");

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("staged-invalid.txt: Is not valid UTF-8.", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("STAGED_SENTINEL", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Text_encoding_reports_non_git_roots_without_raw_process_details()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("Git attribute isolation failed.", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain(repository.RootPath, StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("fatal:", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Text_encoding_reports_corrupt_index_without_raw_git_details()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteText("valid.txt", "valid\n");
        await repository.Stage("valid.txt", TestContext.Current.CancellationToken);
        repository.CorruptIndex();

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("Git index inspection failed.", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain(repository.RootPath, StringComparison.Ordinal);
        result.StandardError.ShouldNotContain("fatal:", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Text_encoding_reports_missing_blob_without_raw_git_details()
    {
        // Arrange
        using var repository = new TemporaryGitRepository();
        await repository.Initialize(TestContext.Current.CancellationToken);
        repository.WriteText("missing-blob.txt", "valid\n");
        await repository.Stage("missing-blob.txt", TestContext.Current.CancellationToken);
        var indexEntry = await repository.RunGit(
            ["ls-files", "--stage", "--", "missing-blob.txt"],
            TestContext.Current.CancellationToken);
        var objectId = indexEntry.StandardOutput.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];
        repository.DeleteLooseObject(objectId);

        // Act
        var result = await repository.RunCommand(
            ["text-encoding", "--root", repository.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("Git blob inspection failed.", StringComparison.Ordinal);
        result.StandardError.ShouldNotContain(objectId, StringComparison.Ordinal);
        result.StandardError.ShouldNotContain(repository.RootPath, StringComparison.Ordinal);
    }
}
