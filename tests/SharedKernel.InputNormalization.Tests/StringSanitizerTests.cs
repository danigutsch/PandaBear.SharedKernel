
namespace SharedKernel.InputNormalization.Tests;

public class StringSanitizerTests
{
    [Fact]
    public void Sanitize_returns_null_when_input_is_null()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize(null);

        // Assert
        (result).ShouldBeNull();
    }

    [Fact]
    public void Sanitize_returns_empty_string_when_input_is_empty()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize(string.Empty);

        // Assert
        (result).ShouldBe(string.Empty);
    }

    [Fact]
    public void Sanitize_trims_leading_whitespace()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize("   Hello");

        // Assert
        (result).ShouldBe("Hello");
    }

    [Fact]
    public void Sanitize_trims_trailing_whitespace()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize("Hello   ");

        // Assert
        (result).ShouldBe("Hello");
    }

    [Fact]
    public void Sanitize_trims_leading_and_trailing_whitespace()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize("   Hello   ");

        // Assert
        (result).ShouldBe("Hello");
    }

    [Fact]
    public void Sanitize_normalizes_multiple_spaces_to_single_space()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize("Hello    World");

        // Assert
        (result).ShouldBe("Hello World");
    }

    [Fact]
    public void Sanitize_normalizes_multiple_whitespace_characters_to_single_space()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize("Hello \t\n  World");

        // Assert
        (result).ShouldBe("Hello World");
    }

    [Fact]
    public void Sanitize_removes_control_characters()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize("Hello" + "\x00\x01\x02" + "World");

        // Assert
        (result).ShouldBe("HelloWorld");
    }

    [Fact]
    public void Sanitize_removes_null_character()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize("Hello" + "\0" + "World");

        // Assert
        (result).ShouldBe("HelloWorld");
    }

    [Fact]
    public void Sanitize_removes_bell_character()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize("Hello" + "\x07" + "World");

        // Assert
        (result).ShouldBe("HelloWorld");
    }

    [Fact]
    public void Sanitize_removes_delete_character()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize("Hello" + "\x7F" + "World");

        // Assert
        (result).ShouldBe("HelloWorld");
    }

    [Fact]
    public void Sanitize_preserves_tab_character_but_normalizes_to_space()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize("Hello\tWorld");

        // Assert
        (result).ShouldBe("Hello World");
    }

    [Fact]
    public void Sanitize_preserves_newline_character_but_normalizes_to_space()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize("Hello\nWorld");

        // Assert
        (result).ShouldBe("Hello World");
    }

    [Fact]
    public void Sanitize_handles_complex_sanitization_with_multiple_issues()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize("  Hello" + "\x00" + "\t\t  World" + "\x01" + "  ");

        // Assert
        (result).ShouldBe("Hello World");
    }

    [Fact]
    public void Sanitize_preserves_valid_string_without_changes()
    {
        // Arrange
        var input = "Hello World";

        // Act
        var result = StringSanitizer.Sanitize(input);

        // Assert
        (result).ShouldBe(input);
    }

    [Fact]
    public void Sanitize_handles_string_with_only_whitespace()
    {
        // Arrange
        // Act
        var result = StringSanitizer.Sanitize("   \t\n   ");

        // Assert
        (result).ShouldBe(string.Empty);
    }

    [Fact]
    public void Sanitize_handles_string_with_special_characters()
    {
        // Arrange
        var input = "Hello@World!#$%";

        // Act
        var result = StringSanitizer.Sanitize(input);

        // Assert
        (result).ShouldBe(input);
    }

    [Fact]
    public void Sanitize_handles_string_with_numbers()
    {
        // Arrange
        var input = "Test123";

        // Act
        var result = StringSanitizer.Sanitize(input);

        // Assert
        (result).ShouldBe(input);
    }

    [Fact]
    public void Sanitize_handles_string_with_accented_characters()
    {
        // Arrange
        var input = "José García";

        // Act
        var result = StringSanitizer.Sanitize(input);

        // Assert
        (result).ShouldBe(input);
    }

    [Fact]
    public void Sanitize_normalizes_unicode_to_form_c()
    {
        // Arrange
        var input = "Cafe\u0301";

        // Act
        var result = StringSanitizer.Sanitize(input);

        // Assert
        (result).ShouldBe("Café");
    }

    [Fact]
    public void Sanitize_handles_string_with_emoji()
    {
        // Arrange
        var input = "Hello 🌍 World";

        // Act
        var result = StringSanitizer.Sanitize(input);

        // Assert
        (result).ShouldBe(input);
    }

    [Fact]
    public void Sanitize_notes_returns_null_when_input_is_null()
    {
        // Arrange
        // Act
        var result = StringSanitizer.SanitizeNotes(null);

        // Assert
        (result).ShouldBeNull();
    }

    [Fact]
    public void Sanitize_notes_returns_empty_string_when_input_is_empty()
    {
        // Arrange
        // Act
        var result = StringSanitizer.SanitizeNotes(string.Empty);

        // Assert
        (result).ShouldBe(string.Empty);
    }

    [Fact]
    public void Sanitize_notes_trims_leading_and_trailing_whitespace()
    {
        // Arrange
        // Act
        var result = StringSanitizer.SanitizeNotes("   Hello World   ");

        // Assert
        (result).ShouldBe("Hello World");
    }

    [Fact]
    public void Sanitize_notes_preserves_newlines_within_text()
    {
        // Arrange
        var input = "Line 1\nLine 2\nLine 3";

        // Act
        var result = StringSanitizer.SanitizeNotes(input);

        // Assert
        (result).ShouldBe(input);
    }

    [Fact]
    public void Sanitize_notes_preserves_multiple_newlines()
    {
        // Arrange
        var input = "Paragraph 1\n\nParagraph 2";

        // Act
        var result = StringSanitizer.SanitizeNotes(input);

        // Assert
        (result).ShouldBe(input);
    }

    [Fact]
    public void Sanitize_notes_preserves_tabs_within_text()
    {
        // Arrange
        var input = "Column1\tColumn2\tColumn3";

        // Act
        var result = StringSanitizer.SanitizeNotes(input);

        // Assert
        (result).ShouldBe(input);
    }

    [Fact]
    public void Sanitize_notes_preserves_multiple_spaces()
    {
        // Arrange
        var input = "Hello    World";

        // Act
        var result = StringSanitizer.SanitizeNotes(input);

        // Assert
        (result).ShouldBe(input);
    }

    [Fact]
    public void Sanitize_notes_preserves_formatting_in_multi_line_notes()
    {
        // Arrange
        var input = "Note:\n  - Item 1\n  - Item 2\n  - Item 3";

        // Act
        var result = StringSanitizer.SanitizeNotes(input);

        // Assert
        (result).ShouldBe(input);
    }

    [Fact]
    public void Sanitize_notes_handles_string_with_only_whitespace()
    {
        // Arrange
        // Act
        var result = StringSanitizer.SanitizeNotes("   \t\n   ");

        // Assert
        (result).ShouldBe(string.Empty);
    }

    [Fact]
    public void Sanitize_collection_returns_empty_array_when_input_is_null()
    {
        // Arrange
        // Act
        var result = StringSanitizer.SanitizeCollection(null);

        // Assert
        _ = (result).ShouldNotBeNull();
        (result).ShouldBeEmpty();
    }

    [Fact]
    public void Sanitize_collection_returns_empty_array_when_input_is_empty()
    {
        // Arrange
        // Act
        var result = StringSanitizer.SanitizeCollection([]);

        // Assert
        _ = (result).ShouldNotBeNull();
        (result).ShouldBeEmpty();
    }

    [Fact]
    public void Sanitize_collection_removes_null_entries()
    {
        // Arrange
        var input = new[] { "Hello", null, "World" };

        // Act
        var result = StringSanitizer.SanitizeCollection(input!);

        // Assert
        (result.Length).ShouldBe(2);
        (result).ShouldContain("Hello");
        (result).ShouldContain("World");
    }

    [Fact]
    public void Sanitize_collection_removes_empty_string_entries()
    {
        // Arrange
        var input = new[] { "Hello", "", "World" };

        // Act
        var result = StringSanitizer.SanitizeCollection(input);

        // Assert
        (result.Length).ShouldBe(2);
        (result).ShouldContain("Hello");
        (result).ShouldContain("World");
    }

    [Fact]
    public void Sanitize_collection_removes_whitespace_only_entries()
    {
        // Arrange
        var input = new[] { "Hello", "   ", "World" };

        // Act
        var result = StringSanitizer.SanitizeCollection(input);

        // Assert
        (result.Length).ShouldBe(2);
        (result).ShouldContain("Hello");
        (result).ShouldContain("World");
    }

    [Fact]
    public void Sanitize_collection_trims_each_entry()
    {
        // Arrange
        var input = new[] { "  Hello  ", "  World  " };

        // Act
        var result = StringSanitizer.SanitizeCollection(input);

        // Assert
        (result.Length).ShouldBe(2);
        (result).ShouldContain("Hello");
        (result).ShouldContain("World");
    }

    [Fact]
    public void Sanitize_collection_removes_duplicate_entries_case_insensitive()
    {
        // Arrange
        var input = new[] { "Hello", "hello", "HELLO", "World" };

        // Act
        var result = StringSanitizer.SanitizeCollection(input);

        // Assert
        (result.Length).ShouldBe(2);
        (result).ShouldContain("Hello");
        (result).ShouldContain("World");
    }

    [Fact]
    public void Sanitize_collection_preserves_first_occurrence_of_duplicate()
    {
        // Arrange
        var input = new[] { "Hello", "hello" };

        // Act
        var result = StringSanitizer.SanitizeCollection(input);

        // Assert
        (result).ShouldHaveSingleItem();
        (result[0]).ShouldBe("Hello");
    }

    [Fact]
    public void Sanitize_collection_sanitizes_each_entry()
    {
        // Arrange
        var input = new[] { "  Hello" + "\x00" + "  ", "  World\t\t  " };

        // Act
        var result = StringSanitizer.SanitizeCollection(input);

        // Assert
        (result.Length).ShouldBe(2);
        (result).ShouldContain("Hello");
        (result).ShouldContain("World");
    }

    [Fact]
    public void Sanitize_collection_handles_complex_collection_with_multiple_issues()
    {
        // Arrange
        var input = new[] { "  Item1  ", null, "", "item1", "  Item2  ", "   ", "Item3" };

        // Act
        var result = StringSanitizer.SanitizeCollection(input!);

        // Assert
        (result.Length).ShouldBe(3);
        (result).ShouldContain("Item1");
        (result).ShouldContain("Item2");
        (result).ShouldContain("Item3");
    }

    [Fact]
    public void Sanitize_collection_returns_empty_array_when_all_entries_are_invalid()
    {
        // Arrange
        var input = new[] { null, "", "   ", "\t\n" };

        // Act
        var result = StringSanitizer.SanitizeCollection(input!);

        // Assert
        _ = (result).ShouldNotBeNull();
        (result).ShouldBeEmpty();
    }

    [Fact]
    public void Sanitize_collection_preserves_order_of_first_occurrences()
    {
        // Arrange
        var input = new[] { "Zebra", "Apple", "Banana" };

        // Act
        var result = StringSanitizer.SanitizeCollection(input);

        // Assert
        (result.Length).ShouldBe(3);
        (result[0]).ShouldBe("Zebra");
        (result[1]).ShouldBe("Apple");
        (result[2]).ShouldBe("Banana");
    }

    [Fact]
    public void Sanitize_collection_handles_collection_with_special_characters()
    {
        // Arrange
        var input = new[] { "Item@1", "Item#2", "Item$3" };

        // Act
        var result = StringSanitizer.SanitizeCollection(input);

        // Assert
        (result.Length).ShouldBe(3);
        (result).ShouldContain("Item@1");
        (result).ShouldContain("Item#2");
        (result).ShouldContain("Item$3");
    }

    [Fact]
    public void Sanitize_collection_handles_collection_with_unicode_characters()
    {
        // Arrange
        var input = new[] { "José", "José", "María" };

        // Act
        var result = StringSanitizer.SanitizeCollection(input);

        // Assert
        (result.Length).ShouldBe(2);
        (result).ShouldContain("José");
        (result).ShouldContain("María");
    }

    [Fact]
    public void Normalize_key_sanitizes_before_uppercase_conversion()
    {
        // Arrange
        var input = "  Cafe\u0301" + "\x00" + "  name  ";

        // Act
        var result = StringSanitizer.NormalizeKey(input);

        // Assert
        (result).ShouldBe("CAFÉ NAME");
    }
}
