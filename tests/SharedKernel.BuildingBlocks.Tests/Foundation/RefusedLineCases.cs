namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>Lists of items that each break one rule of <see cref="SanitizedText.Lines" /> at most 2 items of at most 10 characters.</summary>
public static class RefusedLineCases
{
    /// <summary>Gets the lists with the message each records.</summary>
    /// <returns>The cases.</returns>
    public static TheoryData<string?[], string> All() => new()
    {
        { ["a", "b", "c"], "Highlights cannot have more than 2 items." },
        { ["a", "a\uD800b"], "Highlights is not valid text." },
        { ["a", " "], "Highlights cannot have an empty item." },
        { ["abcdefghijk"], "Highlights: each item must be at most 10 characters." },
    };
}
