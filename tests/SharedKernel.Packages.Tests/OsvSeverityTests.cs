namespace PandaBear.Packages.Tests;

/// <summary>Tests the mapping of OSV severity data to <see cref="AdvisorySeverity"/>.</summary>
[Trait("Capability", "Packages")]
public sealed class OsvSeverityTests
{
    [Theory]
    [InlineData("CRITICAL", AdvisorySeverity.Critical)]
    [InlineData("HIGH", AdvisorySeverity.High)]
    [InlineData("MODERATE", AdvisorySeverity.Medium)]
    [InlineData("MEDIUM", AdvisorySeverity.Medium)]
    [InlineData("LOW", AdvisorySeverity.Low)]
    [InlineData("high", AdvisorySeverity.High)]
    public void A_database_label_maps_to_its_severity(string label, AdvisorySeverity expected)
    {
        // Arrange
        var record = OsvRecords.Parse($$$"""{"id":"X","database_specific":{"severity":"{{{label}}}"}}""");

        // Act
        var severity = OsvSeverity.Map(record);

        // Assert
        severity.ShouldBe(expected);
    }

    [Theory]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H", 9.8, AdvisorySeverity.Critical)]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:C/C:H/I:H/A:H", 10.0, AdvisorySeverity.Critical)]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:N/A:H", 7.5, AdvisorySeverity.High)]
    [InlineData("CVSS:3.1/AV:N/AC:H/PR:N/UI:N/S:U/C:N/I:H/A:H", 7.4, AdvisorySeverity.High)]
    [InlineData("CVSS:3.0/AV:N/AC:L/PR:N/UI:N/S:U/C:L/I:N/A:N", 5.3, AdvisorySeverity.Medium)]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:R/S:C/C:L/I:L/A:N", 6.1, AdvisorySeverity.Medium)]
    [InlineData("CVSS:3.1/AV:P/AC:H/PR:H/UI:R/S:U/C:L/I:N/A:N", 1.6, AdvisorySeverity.Low)]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:N/A:N", 0.0, AdvisorySeverity.Low)]
    public void A_cvss_v3_vector_is_scored_and_banded(string vector, double score, AdvisorySeverity expected)
    {
        // Arrange
        var record = OsvRecords.Parse($$$"""{"id":"X","severity":[{"type":"CVSS_V3","score":"{{{vector}}}"}]}""");

        // Act
        var computed = OsvSeverity.BaseScore(vector);
        var severity = OsvSeverity.Map(record);

        // Assert
        computed.ShouldBe(score);
        severity.ShouldBe(expected);
    }

    [Theory]
    [InlineData("""{"id":"X"}""")]
    [InlineData("""{"id":"X","database_specific":{"severity":"WEIRD"}}""")]
    [InlineData("""{"id":"X","database_specific":{"severity":5}}""")]
    [InlineData("""{"id":"X","severity":[{"type":"CVSS_V2","score":"AV:N/AC:L/Au:N/C:N/I:N/A:P"}]}""")]
    [InlineData("""{"id":"X","severity":[{"type":"CVSS_V4","score":"CVSS:4.0/AV:N/AC:L/AT:N/PR:N/UI:N/VC:H/VI:H/VA:H/SC:N/SI:N/SA:N"}]}""")]
    [InlineData("""{"id":"X","severity":[{"type":"Ubuntu","score":"low"}]}""")]
    [InlineData("""{"id":"X","severity":[{"type":"CVSS_V3","score":"garbage"}]}""")]
    [InlineData("""{"id":"X","severity":[{"type":"CVSS_V3","score":"CVSS:3.1/AV:X/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H"}]}""")]
    [InlineData("""{"id":"X","severity":[{"type":"CVSS_V3","score":"CVSS:3.1/AV:N/AC:L/PR:N/UI:N/C:H/I:H/A:H"}]}""")]
    [InlineData("""{"id":"X","severity":[{"type":"CVSS_V3","score":"CVSS:3.1/AV:N/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H"}]}""")]
    public void Anything_that_cannot_be_read_gets_the_explicit_default_of_high(string json)
    {
        // Act
        var severity = OsvSeverity.Map(OsvRecords.Parse(json));

        // Assert
        severity.ShouldBe(OsvSeverity.Unknown);
        OsvSeverity.Unknown.ShouldBe(AdvisorySeverity.High);
    }

    [Fact]
    public void The_database_label_wins_over_a_cvss_vector()
    {
        // Arrange
        var record = OsvRecords.Parse("""{"id":"X","database_specific":{"severity":"LOW"},"severity":[{"type":"CVSS_V3","score":"CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H"}]}""");

        // Act
        var severity = OsvSeverity.Map(record);

        // Assert
        severity.ShouldBe(AdvisorySeverity.Low);
    }
}
