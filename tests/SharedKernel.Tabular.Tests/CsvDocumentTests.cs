namespace PandaBear.Tabular.Tests;

public class CsvDocumentTests
{
    [Fact]
    public void Create_with_headers_and_row_exposes_both_collections()
    {
        // Arrange
        string[] headers = ["FirstName", "LastName", "Email"];
        CsvRow[] rows = [CsvRow.Parse("John,Doe,john.doe@example.com")];

        // Act
        var documentResult = CsvDocument.Create(headers, rows);

        // Assert
        documentResult.ShouldBeSuccess();

        var document = documentResult.Value;
        document.Headers.Count.ShouldBe(3);
        document.Headers[0].ShouldBe("FirstName");
        document.Headers[1].ShouldBe("LastName");
        document.Headers[2].ShouldBe("Email");

        var row = document.Rows.ShouldHaveSingleItem();
        row[0].ShouldBe("John");
        row[1].ShouldBe("Doe");
        row[2].ShouldBe("john.doe@example.com");
    }

    [Fact]
    public void Parse_with_header_and_row_parses_document()
    {
        // Arrange
        const string csvContent = "FirstName,LastName,Email\nJohn,Doe,john.doe@example.com";

        // Act
        var documentResult = CsvDocument.Parse(csvContent);

        // Assert
        documentResult.ShouldBeSuccess();

        var document = documentResult.Value;
        document.Headers.Count.ShouldBe(3);
        document.Headers[0].ShouldBe("FirstName");
        document.Headers[1].ShouldBe("LastName");
        document.Headers[2].ShouldBe("Email");

        var row = document.Rows.ShouldHaveSingleItem();
        row[0].ShouldBe("John");
        row[1].ShouldBe("Doe");
        row[2].ShouldBe("john.doe@example.com");
    }

    [Fact]
    public void Parse_with_multiple_rows_parses_document()
    {
        // Arrange
        const string csvContent = "FirstName,LastName,Email\nJohn,Doe,john.doe@example.com\nJane,Smith,jane.smith@example.com\nAlice,Johnson,alice.johnson@example.com";

        // Act
        var documentResult = CsvDocument.Parse(csvContent);

        // Assert
        documentResult.ShouldBeSuccess();

        var document = documentResult.Value;
        document.Rows.Count.ShouldBe(3);

        var row1 = document.Rows[0];
        row1[0].ShouldBe("John");
        row1[1].ShouldBe("Doe");
        row1[2].ShouldBe("john.doe@example.com");

        var row2 = document.Rows[1];
        row2[0].ShouldBe("Jane");
        row2[1].ShouldBe("Smith");
        row2[2].ShouldBe("jane.smith@example.com");

        var row3 = document.Rows[2];
        row3[0].ShouldBe("Alice");
        row3[1].ShouldBe("Johnson");
        row3[2].ShouldBe("alice.johnson@example.com");
    }

    [Fact]
    public void Create_with_rows_of_different_lengths_fails()
    {
        // Arrange
        var differentLengthRow = CsvRow.Parse("Alice,alice.johnson@example.com");
        CsvRow[] rows =
        [
            CsvRow.Parse("John,Doe,john.doe@example.com"),
            CsvRow.Parse("Jane,Smith,jane.smith@example.com"),
            differentLengthRow
        ];

        // Act
        var documentResult = CsvDocument.Create(["FirstName", "LastName", "Email"], rows);

        // Assert
        documentResult.IsSuccess.ShouldBeFalse();
        documentResult.ErrorDetails.ShouldNotBeNull();
        documentResult.ErrorDetails.Detail.ShouldContain("All rows must have the same number of columns", StringComparison.Ordinal);
    }

    [Fact]
    public void Create_with_empty_headers_fails()
    {
        // Arrange
        string[] headers = [];
        CsvRow[] rows = [CsvRow.Parse("John,Doe,john.doe@example.com")];

        // Act
        var documentResult = CsvDocument.Create(headers, rows);

        // Assert
        documentResult.IsSuccess.ShouldBeFalse();
        documentResult.ErrorDetails.ShouldNotBeNull();
        documentResult.ErrorDetails.Detail.ShouldContain("Headers must contain at least one column", StringComparison.Ordinal);
    }

    [Fact]
    public void Create_with_header_count_different_from_row_count_fails()
    {
        // Arrange
        string[] headers = ["FirstName", "LastName", "Email", "Phone"];
        CsvRow[] rows = [CsvRow.Parse("John,Doe,john.doe@example.com")];

        // Act
        var documentResult = CsvDocument.Create(headers, rows);

        // Assert
        documentResult.IsSuccess.ShouldBeFalse();
        documentResult.ErrorDetails.ShouldNotBeNull();
        documentResult.ErrorDetails.Detail.ShouldContain("Header count must match row column count", StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_with_missing_required_header_fails()
    {
        // Arrange
        const string csvContent = "FirstName,LastName\nJohn,Doe";
        string[] requiredHeaders = ["RequiredCode"];

        // Act
        var documentResult = CsvDocument.Parse(csvContent, requiredHeaders);

        // Assert
        documentResult.IsSuccess.ShouldBeFalse();
        documentResult.ErrorDetails.ShouldNotBeNull();
        documentResult.ErrorDetails.Detail.ShouldContain("Required header 'RequiredCode' is missing", StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_with_empty_csvcontent_fails()
    {
        // Arrange
        const string csvContent = "";

        // Act
        var documentResult = CsvDocument.Parse(csvContent);

        // Assert
        documentResult.IsSuccess.ShouldBeFalse();
        documentResult.ErrorDetails.ShouldNotBeNull();
        documentResult.ErrorDetails.Detail.ShouldContain("Headers must contain at least one column", StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_with_requiredheader_different_casing_and_whitespace_succeeds()
    {
        // Arrange
        const string csvContent = "FirstName,LastName,Email\nJohn,Doe,john.doe@example.com";
        string[] requiredHeaders = ["  email  "];

        // Act
        var documentResult = CsvDocument.Parse(csvContent, requiredHeaders);

        // Assert
        documentResult.ShouldBeSuccess();
    }

    [Fact]
    public void Parse_with_empty_data_row_does_not_ignore_it_and_fails_validation()
    {
        // Arrange
        const string csvContent = "FirstName,LastName,Email\n\nJohn,Doe,john.doe@example.com";

        // Act
        var documentResult = CsvDocument.Parse(csvContent);

        // Assert
        documentResult.IsSuccess.ShouldBeFalse();
        documentResult.ErrorDetails.ShouldNotBeNull();
        documentResult.ErrorDetails.Detail.ShouldContain("All rows must have the same number of columns", StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_with_blank_requiredheadernames_ignores_them()
    {
        // Arrange
        const string csvContent = "FirstName,LastName,Email\nJohn,Doe,john.doe@example.com";
        string[] requiredHeaders = ["   ", "Email"];

        // Act
        var documentResult = CsvDocument.Parse(csvContent, requiredHeaders);

        // Assert
        documentResult.ShouldBeSuccess();
    }

    [Fact]
    public void Create_with_rows_of_different_lengths_includes_csv_line_number_in_error_detail()
    {
        // Arrange
        CsvRow[] rows =
        [
            CsvRow.Parse("John,Doe,john.doe@example.com"),
            CsvRow.Parse("Jane,jane.smith@example.com")
        ];

        // Act
        var documentResult = CsvDocument.Create(["FirstName", "LastName", "Email"], rows);

        // Assert
        documentResult.IsSuccess.ShouldBeFalse();
        documentResult.ErrorDetails.ShouldNotBeNull();
        documentResult.ErrorDetails.Detail.ShouldContain("line 3", StringComparison.Ordinal);
    }

    [Fact]
    public void String_indexer_with_existing_header_returns_row_value()
    {
        // Arrange
        var documentResult = CsvDocument.Create(
            headers: ["FirstName", "LastName", "Email"],
            rows: [CsvRow.Parse("John,Doe,john.doe@example.com")]
        );

        var document = documentResult.Value;
        var row = document.Rows[0];

        // Act
        var email = row[document.Headers, "Email"];

        // Assert
        email.ShouldBe("john.doe@example.com");
    }

    [Fact]
    public void TryGetByHeader_with_missing_header_returns_false()
    {
        // Arrange
        var documentResult = CsvDocument.Create(
            headers: ["FirstName", "LastName", "Email"],
            rows: [CsvRow.Parse("John,Doe,john.doe@example.com")]
        );

        var document = documentResult.Value;
        var row = document.Rows[0];

        // Act
        var success = row.TryGetByHeader(document.Headers, "RequiredCode", out var value);

        // Assert
        success.ShouldBeFalse();
        value.ShouldBeNull();
    }
}
