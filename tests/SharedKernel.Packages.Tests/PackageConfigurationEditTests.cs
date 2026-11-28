namespace SharedKernel.Packages.Tests;

/// <summary>Exercises package declaration ownership and the file save boundary.</summary>
[Trait(TestTraitNames.CapabilityName, "Packages")]
public sealed class PackageConfigurationEditTests
{
    private const string Original = """{"version":1,"packages":{"apt":["git"],"cargo":["bat"]},"minimumReleaseAgeDays":7}""";

    [Fact]
    public async Task An_edit_cannot_claim_another_managers_package()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = ConfigurationRepository.Create(Original);
        var edit = await PackageConfigurationEdit.Open(repository.Path, ct);
        var path = Path.Combine(repository.Path, PackagesConfiguration.RelativePath);

        // Act
        Action prepare = () => edit.Prepare(PackageManagerKind.Cargo, ["git=2.45"]);
        var error = prepare.ShouldThrow<InvalidOperationException>();
        var unchanged = await File.ReadAllTextAsync(path, ct);

        // Assert
        error.Message.ShouldContain("git is declared for more than one package manager", StringComparison.Ordinal);
        unchanged.ShouldBe(Original);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_confirmed_edit_refuses_a_file_changed_by_another_writer(bool removeFile)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = ConfigurationRepository.Create(Original);
        var edit = await PackageConfigurationEdit.Open(repository.Path, ct);
        var proposed = edit.Prepare(PackageManagerKind.Cargo, ["ripgrep"]).ShouldNotBeNull();
        var path = Path.Combine(repository.Path, PackagesConfiguration.RelativePath);
        const string changed = """{"version":1,"packages":{"apt":["git","curl"],"cargo":["bat"]},"minimumReleaseAgeDays":7}""";
        if (removeFile)
        {
            File.Delete(path);
        }
        else
        {
            await File.WriteAllTextAsync(path, changed, ct);
        }

        // Act
        Func<Task> save = () => edit.Save(proposed, ct);
        var error = await save.ShouldThrow<InvalidOperationException>();
        var contents = File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : null;

        // Assert
        error.Message.ShouldContain("changed during review", StringComparison.Ordinal);
        contents.ShouldBe(removeFile ? null : changed);
    }

    [Fact]
    public async Task Saving_one_empty_manager_preserves_the_other_empty_declarations()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const string source = """{"version":1,"packages":{"apt":[],"cargo":["bat"]},"minimumReleaseAgeDays":7}""";
        using var repository = ConfigurationRepository.Create(source);
        var edit = await PackageConfigurationEdit.Open(repository.Path, ct);
        var path = Path.Combine(repository.Path, PackagesConfiguration.RelativePath);
        var proposed = edit.Prepare(PackageManagerKind.Npm, ["typescript"]).ShouldNotBeNull();

        // Act
        await edit.Save(proposed, ct);
        var result = await PackagesConfiguration.Read(repository.Path, ct);
        var contents = await File.ReadAllTextAsync(path, ct);

        // Assert
        result.Packages.Managers.ShouldBe([PackageManagerKind.Cargo, PackageManagerKind.Npm]);
        var npm = result.Packages.For(PackageManagerKind.Npm).Select(static item => item.ToString());
        npm.ShouldBe(["typescript"]);
        contents.ShouldContain("\"apt\": []", StringComparison.Ordinal);
        result.MinimumReleaseAge.ShouldBe(PackageFactory.Age(7));
    }

    [Theory]
    [InlineData(false, "does not exist.")]
    [InlineData(true, "is larger than 1 MiB.")]
    public async Task An_edit_requires_an_existing_bounded_declaration(bool oversized, string expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = ConfigurationRepository.Create(Original);
        var path = Path.Combine(repository.Path, PackagesConfiguration.RelativePath);
        if (oversized)
        {
            await File.WriteAllTextAsync(path, Original + new string(' ', 1024 * 1024), ct);
        }
        else
        {
            File.Delete(path);
        }

        // Act
        Func<Task> open = () => PackageConfigurationEdit.Open(repository.Path, ct);
        var error = await open.ShouldThrow<InvalidOperationException>();

        // Assert
        error.Message.ShouldBe($"{PackagesConfiguration.RelativePath} {expected}");
    }

    [Fact]
    public async Task A_confirmed_edit_refuses_a_link_substituted_for_its_source()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = ConfigurationRepository.Create(Original);
        using var otherRepository = ConfigurationRepository.Create(Original);
        var edit = await PackageConfigurationEdit.Open(repository.Path, ct);
        var replacement = edit.Prepare(PackageManagerKind.Cargo, ["ripgrep"]).ShouldNotBeNull();
        var path = Path.Combine(repository.Path, PackagesConfiguration.RelativePath);
        var otherPath = Path.Combine(otherRepository.Path, PackagesConfiguration.RelativePath);
        File.Delete(path);
        File.CreateSymbolicLink(path, otherPath);

        // Act
        Func<Task> save = () => edit.Save(replacement, ct);
        var error = await save.ShouldThrow<InvalidOperationException>();
        var contents = await File.ReadAllTextAsync(otherPath, ct);
        var link = new FileInfo(path).LinkTarget;

        // Assert
        error.Message.ShouldContain("Symbolic links are not followed", StringComparison.Ordinal);
        contents.ShouldBe(Original);
        link.ShouldBe(otherPath);
    }

    [Fact]
    public async Task A_cancelled_save_leaves_the_original_bytes_in_place()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var repository = ConfigurationRepository.Create(Original);
        var edit = await PackageConfigurationEdit.Open(repository.Path, ct);
        var replacement = edit.Prepare(PackageManagerKind.Cargo, ["ripgrep"]).ShouldNotBeNull();
        var path = Path.Combine(repository.Path, PackagesConfiguration.RelativePath);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act
        Func<Task> save = () => edit.Save(replacement, cancelled.Token);
        await save.ShouldThrow<OperationCanceledException>();
        var contents = await File.ReadAllTextAsync(path, ct);

        // Assert
        contents.ShouldBe(Original);
    }
}
