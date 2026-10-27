namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// A seeded corpus with one violation of each Gherkin rule, plus line ending, byte order mark, keyword, and tag
/// variants, and the issues that <c>verify-gherkin</c> reports for it.
/// </summary>
internal static class GherkinCorpus
{
    /// <summary>The repository configuration whose Gherkin section applies the corpus rules.</summary>
    public const string Configuration = """
        {
          "version": 1,
          "gherkin": {
            "include": ["tests/*"],
            "exclude": [],
            "allowedTagPatterns": ["@smoke", "@wip", "@ADR:\\d+", "@US:\\d+", "@BC:.+", "@integration-event-transport"],
            "forbiddenTags": ["@skip"],
            "featureNameMaxLength": 250,
            "scenarioNameMaxLength": 150
          }
        }
        """;

    private static readonly string LongFeatureName = new('F', 251);
    private static readonly string LongScenarioName = new('S', 151);

    // 150 code points outside the Basic Multilingual Plane: within the limit, although 300 UTF-16 code units long.
    private static readonly string WideScenarioName = string.Concat(Enumerable.Repeat("\U0001F600", 150));

    /// <summary>The corpus files by repository path, with the content of each.</summary>
    public static IReadOnlyDictionary<string, string> Files
    {
        get;
    } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["tests/corpus/clean.feature"] = "Feature: Clean\n    Scenario: s\n        Given x\n",
        ["tests/corpus/empty.feature"] = "",
        ["tests/corpus/missing-newline.feature"] = "Feature: Missing newline\n    Scenario: s\n        Given x",
        ["tests/corpus/trailing.feature"] = "Feature: Trailing  \n    Scenario: s\n        Given x\n    \n# comment \n",
        ["tests/corpus/nbsp-trailing.feature"] = "Feature: Nbsp\n    Scenario: s\u00A0\n        Given x\n",
        ["tests/corpus/tabs.feature"] = "Feature: Tabs\n\tScenario: s\n        Given\tx\n# comment\twith tab\n",
        ["tests/corpus/tags.feature"] = "@smoke @Unknown\n@skip\n@smoke # @unknown\n@ADR:12a @ADR:12 @US:\u0661\u0662 @BC:Admin\n@integration-event-transport @integration-event-transportX\nFeature: Tags\n    @wip\n    Scenario: s\n        Given x\n",
        ["tests/corpus/feature-unnamed.feature"] = "Feature:\n    Scenario: s\n",
        ["tests/corpus/feature-bare.feature"] = "Feature\n    Scenario: s\n",
        ["tests/corpus/feature-spaced.feature"] = "Feature : Spaced\n    Scenario: s\n",
        ["tests/corpus/feature-long.feature"] = "Feature: " + LongFeatureName + "\n    Scenario: s\n",
        ["tests/corpus/feature-colon.feature"] = "Feature: Colon: name\n    Scenario: s\n",
        ["tests/corpus/feature-twice.feature"] = "Feature: Twice\n    Scenario: a\nFeature: Twice\n    Scenario: b\n",
        ["tests/corpus/feature-reset.feature"] = "Feature: Reset\n    Scenario: a\nFeature:\n",
        ["tests/corpus/no-feature.feature"] = "    Scenario: s\n        Given x\n",
        ["tests/corpus/scenario-unnamed.feature"] = "Feature: Scenario unnamed\n    Scenario:\n    Scenario Outline\n        Examples:\n",
        ["tests/corpus/scenario-long.feature"] = "Feature: Scenario long\n    Scenario: " + LongScenarioName + "\n",
        ["tests/corpus/scenario-wide.feature"] = "Feature: Scenario wide\n    Scenario: " + WideScenarioName + "\n",
        ["tests/corpus/scenario-dup.feature"] = "Feature: Scenario dup\n    Scenario: same\n    Scenario Outline: same\n        Examples:\n    Scenario: item #1\n    Scenario: item #2\n    Scenario: same\n",
        ["tests/corpus/outline.feature"] = "Feature: Outline\n    Scenario Outline: first\n        Given <x>\n    Scenario: plain\n        Examples:\n    Scenario Outline: second\n        Scenarios:\n    Rule: r\n    Scenario Outline: third\n        Examples: e\n    Scenario Template: fourth\n    Scenario Outline: last\n",
        ["tests/corpus/no-scenario.feature"] = "Feature: No scenario\n    Background:\n        Given x\n",
        ["tests/corpus/example-keyword.feature"] = "Feature: Example keyword\n    Example: e\n        Given x\n",
        ["tests/corpus/example-and-scenario.feature"] = "Feature: Example and scenario\n    Example: e\n    Scenario: s\n",
        ["tests/corpus/dup-a.feature"] = "Feature: Duplicate\n    Scenario: s\n        Given x\n",
        ["tests/corpus/dup-b.feature"] = "Feature: Duplicate\n    Scenario: s\n        Given x\n",
        ["tests/corpus/sub/dup-c.feature"] = "Feature: Duplicate\n    Scenario: s\n        Given x\n",
        ["tests/corpus/crlf-clean.feature"] = "Feature: Crlf clean\r\n    Scenario: s\r\n        Given x\r\n",
        ["tests/corpus/crlf-trailing.feature"] = "Feature: Crlf trailing \r\n    Scenario: s\r\n        Given x",
        ["tests/corpus/cr-only.feature"] = "Feature: Cr only\r    Scenario: s \r",
        ["tests/corpus/bom.feature"] = "\uFEFFFeature: Bom\n    Scenario: s\n",
        ["tests/corpus/bom-tag.feature"] = "\uFEFF@Unknown\nFeature: Bom tag\n    Scenario: s\n",
        ["tests/corpus/bom-comment.feature"] = "\uFEFF# comment \nFeature: Bom comment\n    Scenario: s\n",
        ["tests/corpus/blank-lines.feature"] = "\n\nFeature: Blank lines\n\n\n    Scenario: s\n\n",
        ["tests/corpus/caf\u00E9 name.feature"] = "Feature: Caf\u00E9\n    Scenario: s\n        Given x\n",
        ["tests/corpus/not-a-feature.txt"] = "Feature:\t \n",
        ["tests/corpus/bin/ignored.feature"] = "Feature:\t \n",
        ["docs/outside.feature"] = "Feature:\t \n",
    };

    /// <summary>The issues that <c>verify-gherkin</c> reports for the whole corpus.</summary>
    public static IReadOnlyList<string> Issues
    {
        get;
    } =
    [
        "- tests/corpus/bom-comment.feature: Line 1: Trailing whitespace is not allowed.",
        "- tests/corpus/bom.feature: Line 1: A Feature declaration is required.",
        "- tests/corpus/cr-only.feature: Line 2: Trailing whitespace is not allowed.",
        "- tests/corpus/crlf-trailing.feature: Line 3: The file does not end with a newline.",
        "- tests/corpus/crlf-trailing.feature: Line 1: Trailing whitespace is not allowed.",
        "- tests/corpus/dup-b.feature: Line 1: Duplicate Feature name 'Duplicate'.",
        "- tests/corpus/empty.feature: Line 1: The file is empty.",
        "- tests/corpus/example-keyword.feature: Line 1: The file has no Scenario.",
        "- tests/corpus/feature-bare.feature: Line 1: The Feature name is required.",
        "- tests/corpus/feature-bare.feature: Line 1: A Feature declaration is required.",
        "- tests/corpus/feature-long.feature: Line 1: The Feature name exceeds 250 characters.",
        "- tests/corpus/feature-reset.feature: Line 3: The Feature name is required.",
        "- tests/corpus/feature-reset.feature: Line 1: A Feature declaration is required.",
        "- tests/corpus/feature-spaced.feature: Line 1: A Feature declaration is required.",
        "- tests/corpus/feature-twice.feature: Line 3: Duplicate Feature name 'Twice'.",
        "- tests/corpus/feature-unnamed.feature: Line 1: The Feature name is required.",
        "- tests/corpus/feature-unnamed.feature: Line 1: A Feature declaration is required.",
        "- tests/corpus/missing-newline.feature: Line 3: The file does not end with a newline.",
        "- tests/corpus/nbsp-trailing.feature: Line 2: Trailing whitespace is not allowed.",
        "- tests/corpus/no-feature.feature: Line 1: A Feature declaration is required.",
        "- tests/corpus/no-scenario.feature: Line 1: The file has no Scenario.",
        "- tests/corpus/outline.feature: Line 4: A Scenario Outline ends here without an Examples section.",
        "- tests/corpus/outline.feature: Line 9: A Scenario Outline ends here without an Examples section.",
        "- tests/corpus/outline.feature: Line 12: A Scenario Outline ends here without an Examples section.",
        "- tests/corpus/scenario-dup.feature: Line 3: Duplicate Scenario name 'same'.",
        "- tests/corpus/scenario-dup.feature: Line 6: Duplicate Scenario name 'item'.",
        "- tests/corpus/scenario-dup.feature: Line 7: Duplicate Scenario name 'same'.",
        "- tests/corpus/scenario-long.feature: Line 2: The Scenario name exceeds 150 characters.",
        "- tests/corpus/scenario-unnamed.feature: Line 2: The Scenario name is required.",
        "- tests/corpus/scenario-unnamed.feature: Line 3: The Scenario name is required.",
        "- tests/corpus/sub/dup-c.feature: Line 1: Duplicate Feature name 'Duplicate'.",
        "- tests/corpus/tabs.feature: Line 2: Tabs are not allowed.",
        "- tests/corpus/tabs.feature: Line 3: Tabs are not allowed.",
        "- tests/corpus/tags.feature: Line 1: Tag '@Unknown' is not allowed.",
        "- tests/corpus/tags.feature: Line 2: Tag '@skip' is not allowed.",
        "- tests/corpus/tags.feature: Line 2: Tag '@skip' is forbidden.",
        "- tests/corpus/tags.feature: Line 4: Tag '@ADR:12a' is not allowed.",
        "- tests/corpus/tags.feature: Line 5: Tag '@integration-event-transportX' is not allowed.",
        "- tests/corpus/trailing.feature: Line 1: Trailing whitespace is not allowed.",
        "- tests/corpus/trailing.feature: Line 4: Trailing whitespace is not allowed.",
        "- tests/corpus/trailing.feature: Line 5: Trailing whitespace is not allowed.",
    ];

    /// <summary>The issues that <c>verify-gherkin</c> reports when it is given explicit paths.</summary>
    public static IReadOnlyList<string> ExplicitPathIssues
    {
        get;
    } =
    [
        "- tests/corpus/sub/dup-c.feature: Line 1: Duplicate Feature name 'Duplicate'.",
        "- tests/corpus/tags.feature: Line 1: Tag '@Unknown' is not allowed.",
        "- tests/corpus/tags.feature: Line 2: Tag '@skip' is not allowed.",
        "- tests/corpus/tags.feature: Line 2: Tag '@skip' is forbidden.",
        "- tests/corpus/tags.feature: Line 4: Tag '@ADR:12a' is not allowed.",
        "- tests/corpus/tags.feature: Line 5: Tag '@integration-event-transportX' is not allowed.",
    ];
}
