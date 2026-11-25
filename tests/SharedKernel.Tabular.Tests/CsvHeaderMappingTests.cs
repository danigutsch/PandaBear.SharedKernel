using System.Text;

namespace SharedKernel.Tabular.Tests;

/// <summary>Tests header matching and remapping without losing CSV source information.</summary>
public sealed class CsvHeaderMappingTests
{
    [Fact]
    public void Contact_import_preserves_column_order_and_physical_source_lines_when_overridden()
    {
        // Arrange
        const string csv = "\r\nfirstname,Alternate Name,instagram,Email,Extra\r\n\r\nAlice,Sam,profile,sam@example.org,\"first\r\nsecond\"\r\n\r\nLucy,Fran,profile2,fran@example.org,other";
        var original = CsvDocument.ParseUtf8(Encoding.UTF8.GetBytes(csv)).ShouldBeSuccess();
        string[] targets = ["FirstName", "Instagram", "Email", "LastName"];
        var overrides = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["FirstName"] = "Alternate Name",
            ["Instagram"] = null,
        };

        // Act
        var matches = CsvHeaderMapping.Match(original.Headers, targets, overrides);
        var mapped = CsvHeaderMapping.Remap(original, matches, header => header + " (unmapped)");

        // Assert
        matches.Select(match => match.SourceHeader).ShouldBe(["Alternate Name", null, "Email", null]);
        mapped.Headers.ShouldBe(["firstname (unmapped)", "FirstName", "instagram (unmapped)", "Email", "Extra"]);
        mapped.Rows.Select(row => row.LineNumber).ShouldBe([4, 7]);
        mapped.Rows[0].ShouldBeSameAs(original.Rows[0]);
        mapped.Rows[0][mapped.Headers, "FirstName"].ShouldBe("Sam");
        mapped.Rows[0][mapped.Headers, "Extra"].ShouldBe("first\r\nsecond");
        original.Headers[1].ShouldBe("Alternate Name");
    }

    [Fact]
    public void Venue_inventory_can_select_exact_case_and_preserve_values_for_internal_exchange()
    {
        // Arrange
        var source = CsvDocument.Parse("room,Room,Notes\nannex,studio,\" =A1\"").ShouldBeSuccess();
        string[] targets = ["Code", "Room", "Notes"];
        var overrides = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Code"] = "room" };
        var exactOptions = new CsvWriteOptions { GuardFormulas = false };

        // Act
        var matches = CsvHeaderMapping.Match(source.Headers, targets, overrides, StringComparer.Ordinal);
        var mapped = CsvHeaderMapping.Remap(source, matches, comparer: StringComparer.Ordinal);
        var internalFile = CsvWriter.Write(mapped.Headers, mapped.Rows, exactOptions);
        var spreadsheet = CsvWriter.Write(mapped.Headers, mapped.Rows);

        // Assert
        mapped.Headers.ShouldBe(targets);
        CsvSamples.ShouldMatch(CsvDocument.Parse(internalFile).ShouldBeSuccess(), targets, [["annex", "studio", " =A1"]], internalFile);
        CsvSamples.ShouldMatch(CsvDocument.Parse(spreadsheet).ShouldBeSuccess(), targets, [["annex", "studio", "' =A1"]], spreadsheet);
    }

    [Fact]
    public void Matching_selects_the_first_source_and_conflicting_assignments_use_the_last_target()
    {
        // Arrange
        var source = CsvDocument.Parse("label,LABEL,Other\nfirst,second,third").ShouldBeSuccess();
        var overrides = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Title"] = "Label",
            ["Heading"] = "Label",
        };

        // Act
        var matches = CsvHeaderMapping.Match(source.Headers, ["Title", "Heading"], overrides);
        var mapped = CsvHeaderMapping.Remap(source, matches);

        // Assert
        matches.Select(match => match.SourceHeader).ShouldBe(["label", "label"]);
        mapped.Headers.ShouldBe(["Heading", "LABEL", "Other"]);
        mapped.Rows.ShouldHaveSingleItem()[mapped.Headers, "Heading"].ShouldBe("first");
    }

    [Fact]
    public void An_unchanged_mapping_reuses_the_document()
    {
        // Arrange
        var document = CsvDocument.Parse("Name,Extra\nA,B").ShouldBeSuccess();
        var matches = CsvHeaderMapping.Match(document.Headers, ["Name"]);

        // Act
        var mapped = CsvHeaderMapping.Remap(document, matches);

        // Assert
        mapped.ShouldBeSameAs(document);
    }
}
