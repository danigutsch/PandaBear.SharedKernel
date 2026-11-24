using PandaBear.Results;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the shared rules of a list of per-culture translations.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "Localization")]
public sealed class TranslationListRulesTests
{
    private const string Field = "translations";
    private static readonly ContentCulture Pt = ContentCulture.Restore("pt");
    private static readonly ContentCulture En = ContentCulture.Restore("en");
    private static readonly TranslationListRules Rules = TitleListRules.Rules;

    [Fact]
    public void Validate_builds_each_translation_of_a_supported_culture_in_order()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var translations = TitleListRules.Validate(errors, [new EnteredTitle("EN", "Andes"), new EnteredTitle("pt", "Andes pt")]);

        // Assert
        errors.HasErrors.ShouldBeFalse();
        translations.ShouldBe([new TitleTranslation(En, "Andes"), new TitleTranslation(Pt, "Andes pt")]);
    }

    [Fact]
    public void Validate_refuses_an_unsupported_culture_a_repeated_one_and_a_missing_default_in_that_order()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var translations = TitleListRules.Validate(errors, [new EnteredTitle("fr", "x"), new EnteredTitle("en", "a"), new EnteredTitle("en", "b"), new EnteredTitle(null, "c")]);

        // Assert
        translations.ShouldBe([new TitleTranslation(En, "a")]);
        FieldMessages.Of(errors, Field).ShouldBe(
        [
            "Each translation must use one of the album languages: pt, en.",
            "Each language can have only one translation.",
            "Each translation must use one of the album languages: pt, en.",
            "A translation in the default language (pt) is required.",
        ]);
    }

    [Fact]
    public void Validate_of_none_requires_the_default_culture()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var translations = TitleListRules.Validate(errors, null);

        // Assert
        translations.ShouldBeEmpty();
        FieldMessages.Of(errors, Field).ShouldBe(["A translation in the default language (pt) is required."]);
    }

    [Fact]
    public void Validate_leaves_out_a_translation_that_the_builder_refuses_but_counts_its_culture()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var translations = Rules.Validate<EnteredTitle, TitleTranslation>(errors, [new EnteredTitle("pt", "x")], static entered => entered.Culture, static (_, _) => null, "definition");

        // Assert
        translations.ShouldBeEmpty();
        errors.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void Validate_names_the_given_parameter_for_a_null_translation()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        Action validate = () => TitleListRules.Validate(errors, [null]);

        // Assert
        validate.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("definition");
    }

    [Fact]
    public void Merge_keeps_the_instance_of_a_culture_that_stays_adds_new_ones_and_removes_the_rest()
    {
        // Arrange
        var kept = new EditableTitle(Pt, "old");
        var removed = new EditableTitle(En, "gone");
        var current = new List<EditableTitle> { kept, removed };
        var added = new EditableTitle(ContentCulture.Restore("es"), "nuevo");

        // Act
        TranslationListRules.Merge(current, [new EditableTitle(Pt, "new"), added], static title => title.Culture, static (target, source) => target.Text = source.Text);

        // Assert
        current.ShouldBe([kept, added]);
        kept.Text.ShouldBe("new");
    }

    [Fact]
    public void Same_compares_language_by_language_in_any_order()
    {
        // Arrange
        TitleTranslation[] current = [new(Pt, "a"), new(En, "b")];

        // Act
        var reordered = TranslationListRules.Same(current, [new(En, "b"), new(Pt, "a")], static title => title.Culture, static (left, right) => left == right);
        var changed = TranslationListRules.Same(current, [new(En, "b"), new(Pt, "c")], static title => title.Culture, static (left, right) => left == right);
        var fewer = TranslationListRules.Same(current, [new(Pt, "a")], static title => title.Culture, static (left, right) => left == right);

        // Assert
        reordered.ShouldBeTrue();
        changed.ShouldBeFalse();
        fewer.ShouldBeFalse();
    }

    [Fact]
    public void The_rules_refuse_a_blank_field_or_subject()
    {
        // Arrange
        var cultures = Rules.Cultures;

        // Act
        Action blankField = () => _ = new TranslationListRules(cultures, " ", "album");
        Action blankSubject = () => _ = new TranslationListRules(cultures, Field, "");

        // Assert
        blankField.ShouldThrow<ArgumentException>().ParamName.ShouldBe("field");
        blankSubject.ShouldThrow<ArgumentException>().ParamName.ShouldBe("subject");
    }
}
