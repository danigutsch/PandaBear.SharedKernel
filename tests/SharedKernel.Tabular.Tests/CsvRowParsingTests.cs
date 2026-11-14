namespace PandaBear.Tabular.Tests;

/// <summary>Tests for <see cref="CsvRow"/>: values kept as given, list access, line numbers and index bounds.</summary>
public class CsvRowParsingTests
{
    [Fact]
    public void Create_keeps_the_values_as_given()
    {
        // Act
        var row = CsvRow.Create([" John ", "Doe", ""]);

        // Assert
        row.Count.ShouldBe(3);
        row[0].ShouldBe(" John ");
        row.ToArray().ShouldBe([" John ", "Doe", ""]);
        row.LineNumber.ShouldBe(0);
    }

    [Fact]
    public void Parsed_rows_know_the_line_they_start_on()
    {
        // Act
        var document = CsvDocument.Parse("A,B\n\"x\ny\",1\nz,2").ShouldBeSuccess();

        // Assert
        document.Rows.Select(row => row.LineNumber).ShouldBe([2, 4]);
    }

    [Fact]
    public void Index_outofrange_throwsexception()
    {
        // Arrange
        var row = CsvRow.Create(["John", "Doe", "john.doe@example.com"]);

        // Act, Assert
        ShouldAssertionExtensions.ShouldThrow<IndexOutOfRangeException>((Func<object?>)(() => row[99]));
    }
}
