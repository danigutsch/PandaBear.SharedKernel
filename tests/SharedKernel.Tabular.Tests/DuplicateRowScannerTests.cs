namespace PandaBear.Tabular.Tests;

/// <summary>Tests for <see cref="DuplicateRowScanner"/> duplicate detection, comparer use and argument guards.</summary>
public sealed class DuplicateRowScannerTests
{
    [Fact]
    public void FindDuplicateLineNumbers_flags_only_later_normalized_duplicates()
    {
        // Arrange
        var document = CsvDocument.Parse("Code\nalpha\nALPHA\nbeta\nalpha").Value;

        // Act
        var duplicates = DuplicateRowScanner.FindDuplicateLineNumbers(
            document, row => row[0].ToUpperInvariant());

        // Assert
        duplicates.Count.ShouldBe(2);
        duplicates[0].ShouldBe(3);
        duplicates[1].ShouldBe(5);
    }

    [Fact]
    public void FindDuplicateLineNumbers_uses_the_supplied_comparer()
    {
        // Arrange
        var document = CsvDocument.Parse("Code\nalpha\nALPHA").Value;

        // Act
        var duplicates = DuplicateRowScanner.FindDuplicateLineNumbers(
            document, row => row[0], StringComparer.OrdinalIgnoreCase);

        // Assert
        duplicates.ShouldHaveSingleItem().ShouldBe(3);
    }

    [Fact]
    public void FindDuplicateLineNumbers_keeps_key_selection_and_blank_policy_with_the_caller()
    {
        // Arrange
        var document = CsvDocument.Parse("Code,Name\n,one\n,two\nalpha,three\nalpha,four").Value;

        // Act
        var duplicates = DuplicateRowScanner.FindDuplicateLineNumbers(
            document, row => string.IsNullOrWhiteSpace(row[0]) ? null : row[0]);

        // Assert
        duplicates.ShouldHaveSingleItem().ShouldBe(5);
    }

    [Fact]
    public void FindDuplicateLineNumbers_does_not_normalize_or_skip_empty_keys_implicitly()
    {
        // Arrange
        var document = CsvDocument.Parse("Code,Name\n,one\n,two\nalpha,three\nALPHA,four").Value;

        // Act
        var duplicates = DuplicateRowScanner.FindDuplicateLineNumbers(document, row => row[0]);

        // Assert
        duplicates.ShouldHaveSingleItem().ShouldBe(3);
    }

    [Fact]
    public void FindDuplicateLineNumbers_guards_document_and_selector()
    {
        // Arrange
        var document = CsvDocument.Parse("Code").Value;
        Action missingDocument = () => DuplicateRowScanner.FindDuplicateLineNumbers(null!, row => row[0]);
        Action missingSelector = () => DuplicateRowScanner.FindDuplicateLineNumbers(document, null!);

        // Act, Assert
        missingDocument.ShouldThrow<ArgumentNullException>();
        missingSelector.ShouldThrow<ArgumentNullException>();
    }
}
