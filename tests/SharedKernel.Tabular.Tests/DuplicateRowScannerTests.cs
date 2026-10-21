namespace PandaBear.Tabular.Tests;

public sealed class DuplicateRowScannerTests
{
    [Fact]
    public void FindDuplicateLineNumbers_flags_only_later_normalized_duplicates()
    {
        var document = CsvDocument.Parse("Code\nalpha\nALPHA\nbeta\nalpha").Value;

        var duplicates = DuplicateRowScanner.FindDuplicateLineNumbers(
            document, row => row[0].ToUpperInvariant());

        duplicates.Count.ShouldBe(2);
        duplicates[0].ShouldBe(3);
        duplicates[1].ShouldBe(5);
    }

    [Fact]
    public void FindDuplicateLineNumbers_uses_the_supplied_comparer()
    {
        var document = CsvDocument.Parse("Code\nalpha\nALPHA").Value;

        var duplicates = DuplicateRowScanner.FindDuplicateLineNumbers(
            document, row => row[0], StringComparer.OrdinalIgnoreCase);

        duplicates.ShouldHaveSingleItem().ShouldBe(3);
    }

    [Fact]
    public void FindDuplicateLineNumbers_keeps_key_selection_and_blank_policy_with_the_caller()
    {
        var document = CsvDocument.Parse("Code,Name\n,one\n,two\nalpha,three\nalpha,four").Value;

        var duplicates = DuplicateRowScanner.FindDuplicateLineNumbers(
            document, row => string.IsNullOrWhiteSpace(row[0]) ? null : row[0]);

        duplicates.ShouldHaveSingleItem().ShouldBe(5);
    }

    [Fact]
    public void FindDuplicateLineNumbers_does_not_normalize_or_skip_empty_keys_implicitly()
    {
        var document = CsvDocument.Parse("Code,Name\n,one\n,two\nalpha,three\nALPHA,four").Value;

        var duplicates = DuplicateRowScanner.FindDuplicateLineNumbers(document, row => row[0]);

        duplicates.ShouldHaveSingleItem().ShouldBe(3);
    }

    [Fact]
    public void FindDuplicateLineNumbers_guards_document_and_selector()
    {
        var document = CsvDocument.Parse("Code").Value;
        Action missingDocument = () => DuplicateRowScanner.FindDuplicateLineNumbers(null!, row => row[0]);
        Action missingSelector = () => DuplicateRowScanner.FindDuplicateLineNumbers(document, null!);

        missingDocument.ShouldThrow<ArgumentNullException>();
        missingSelector.ShouldThrow<ArgumentNullException>();
    }
}
