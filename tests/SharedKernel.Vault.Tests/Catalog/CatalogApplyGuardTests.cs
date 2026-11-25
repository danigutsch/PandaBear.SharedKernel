using SharedKernel.Testing;
using SharedKernel.Vault.Catalog;

namespace SharedKernel.Vault.Tests.Catalog;

/// <summary>Tests the source identity and removal authorization required for catalog application.</summary>
public sealed class CatalogApplyGuardTests
{
    [Fact]
    public void Check_source_rejects_another_name_without_reading_the_file()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var expected = new CatalogSource("original.json", "abc");
        string path = Path.Combine(directory.Path, "renamed.json");

        // Act
        var match = CatalogFile.CheckSource(expected, path);

        // Assert
        match.ShouldBe(CatalogSourceMatch.FileNameMismatch);
    }

    [Fact]
    public void Check_source_accepts_the_digest_in_either_hexadecimal_case()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "vault.json");
        File.WriteAllText(path, "fixture");
        var expected = new CatalogSource("vault.json", CatalogFile.Hash(path).ToUpperInvariant());

        // Act
        var match = CatalogFile.CheckSource(expected, path);

        // Assert
        match.ShouldBe(CatalogSourceMatch.Match);
    }

    [Fact]
    public void Check_source_rejects_changed_bytes_with_the_original_name()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        string path = Path.Combine(directory.Path, "vault.json");
        File.WriteAllText(path, "original fixture");
        var expected = new CatalogSource("vault.json", CatalogFile.Hash(path));
        File.WriteAllText(path, "changed fixture");

        // Act
        var match = CatalogFile.CheckSource(expected, path);

        // Assert
        match.ShouldBe(CatalogSourceMatch.DigestMismatch);
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void Can_write_requires_an_edit_and_authorization_for_removals(
        bool hasEditedVault,
        bool hasRemovals,
        bool allowRemovals,
        bool expected)
    {
        // Arrange
        var result = new CatalogApplyResult(hasEditedVault ? new([]) : null, 0, [])
        {
            Removals = hasRemovals ? ["Removed a declared folder."] : [],
        };

        // Act
        bool authorized = result.CanWrite(allowRemovals);

        // Assert
        authorized.ShouldBe(expected);
    }
}
