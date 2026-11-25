using System.Globalization;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the public guards that components use for ids, position formats and enum parameters.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ParameterGuardTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("day-1")]
    public void An_absent_or_compact_element_ID_is_accepted(string? id)
    {
        // Act
        var exception = Record.Exception(() => ElementId.Require(id, "HeadingId"));

        // Assert
        exception.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("day 1")]
    [InlineData("day\t1")]
    public void An_empty_element_ID_or_one_with_white_space_is_refused(string id)
    {
        // Act
        var require = () => ElementId.Require(id, "HeadingId");

        // Assert
        var exception = require.ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("HeadingId");
        exception.Message.ShouldStartWith("A heading id must not be empty or contain white space.", StringComparison.Ordinal);
    }

    [Fact]
    public void A_position_format_gives_the_first_position()
    {
        // Act
        var first = PositionFormat.Require("step-{0}", "Format", CultureInfo.InvariantCulture);

        // Assert
        first.ShouldBe("step-1");
    }

    [Theory]
    [InlineData(" ", "The format must hold {0} for the position.")]
    [InlineData("Day", "The format must hold {0} for the position, so each step reads differently.")]
    [InlineData("Day {{0}}", "The format must hold {0} for the position, so each step reads differently.")]
    public void A_position_format_that_does_not_write_the_position_is_refused(string format, string message)
    {
        // Act
        var require = () => PositionFormat.Require(format, "Format", CultureInfo.InvariantCulture);

        // Assert
        var exception = require.ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("Format");
        exception.Message.ShouldStartWith(message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_position_format_is_a_null_argument()
    {
        // Act
        var failure = Record.Exception(() => PositionFormat.Require(null, "Format", CultureInfo.InvariantCulture));

        // Assert
        failure.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("Format");
    }

    [Fact]
    public void A_malformed_position_format_is_refused_with_the_format_error_inside()
    {
        // Act
        var require = () => PositionFormat.Require("Day {1}", "Format", CultureInfo.InvariantCulture);

        // Assert
        var exception = require.ShouldThrow<ArgumentException>();
        exception.ParamName.ShouldBe("Format");
        exception.Message.ShouldStartWith("The format must format the position as {0} alone.", StringComparison.Ordinal);
        exception.InnerException.ShouldBeOfType<FormatException>();
    }

    [Fact]
    public void A_declared_or_absent_enum_value_is_accepted()
    {
        // Act
        var declared = Record.Exception(() => EnumParameter.RequireDefined(HeroLayout.Cover, "Layout", "The hero layout is not defined."));
        var absent = Record.Exception(() => EnumParameter.RequireDefined((SiteAlignment?)null, "TextAlignment", "The text alignment is not defined."));

        // Assert
        declared.ShouldBeNull();
        absent.ShouldBeNull();
    }

    [Fact]
    public void An_undeclared_enum_value_is_refused_with_the_given_message()
    {
        // Act
        var plain = () => EnumParameter.RequireDefined((HeroLayout)9, "Layout", "The hero layout is not defined.");
        var nullable = () => EnumParameter.RequireDefined((SiteAlignment?)9, "TextAlignment", "The text alignment is not defined.");

        // Assert
        var plainException = plain.ShouldThrow<ArgumentOutOfRangeException>();
        plainException.ParamName.ShouldBe("Layout");
        plainException.ActualValue.ShouldBe((HeroLayout)9);
        plainException.Message.ShouldStartWith("The hero layout is not defined.", StringComparison.Ordinal);
        var nullableException = nullable.ShouldThrow<ArgumentOutOfRangeException>();
        nullableException.ParamName.ShouldBe("TextAlignment");
        nullableException.ActualValue.ShouldBe((SiteAlignment)9);
    }
}
