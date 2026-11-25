using System.Globalization;

namespace SharedKernel.Testing.Tests;

/// <summary>Tests that the string writer for captured output produces the same text on every operating system.</summary>
public sealed class InvariantStringWriterTests
{
    [Fact]
    public static void Write_line_ends_each_line_with_a_line_feed()
    {
        // Arrange
        using var writer = new InvariantStringWriter();

        // Act
        writer.WriteLine("first");
        writer.WriteLine();
        writer.Write("last");

        // Assert
        writer.NewLine.ShouldBe("\n");
        writer.ToString().ShouldBe("first\n\nlast");
    }

    [Fact]
    public static async Task Asynchronous_write_line_ends_each_line_with_a_line_feed()
    {
        // Arrange
        await using var writer = new InvariantStringWriter();

        // Act
        await writer.WriteLineAsync("first".AsMemory(), TestContext.Current.CancellationToken);
        await writer.WriteLineAsync("second");

        // Assert
        writer.ToString().ShouldBe("first\nsecond\n");
    }

    [Fact]
    public static void Values_are_formatted_with_the_invariant_culture()
    {
        // Arrange
        using var writer = new InvariantStringWriter();

        // Act
        writer.Write(1234.5);
        writer.Write(' ');
        writer.Write(new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        writer.FormatProvider.ShouldBeSameAs(CultureInfo.InvariantCulture);
        writer.ToString().ShouldBe("1234.5 11/15/2026 00:00:00");
    }

    [Fact]
    public static void An_explicit_line_ending_replaces_the_line_feed()
    {
        // Arrange
        using var writer = new InvariantStringWriter { NewLine = "\r\n" };

        // Act
        writer.WriteLine("line");

        // Assert
        writer.ToString().ShouldBe("line\r\n");
    }
}
