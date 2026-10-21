namespace PandaBear.Tabular.Tests;

public class CsvRowParsingTests
{
    [Fact]
    public void Parse_single_data_row_returns_parsed_row()
    {
        // Arrange
        const string csvLine = "John,Doe,john.doe@example.com";

        // Act
        var result = CsvRow.Parse(csvLine);

        // Assert
        _ = (result).ShouldNotBeNull();
        (result[0]).ShouldBe("John");
        (result[1]).ShouldBe("Doe");
        (result[2]).ShouldBe("john.doe@example.com");
    }

    [Fact]
    public void Parse_with_whitespace_trims_values()
    {
        // Arrange
        const string csvLine = " John , Doe , john.doe@example.com ";

        // Act
        var result = CsvRow.Parse(csvLine);

        // Assert
        (result[0]).ShouldBe("John");
        (result[1]).ShouldBe("Doe");
        (result[2]).ShouldBe("john.doe@example.com");
    }

    [Fact]
    public void Index_outofrange_throwsexception()
    {
        // Arrange
        var row = CsvRow.Parse("John,Doe,john.doe@example.com");

        // Act & Assert
        ((Func<object?>)(() => row[99])).ShouldThrow<IndexOutOfRangeException>();
    }
}
