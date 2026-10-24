namespace PandaBear.CI.Tool;

/// <summary>The test projects that a slice runs.</summary>
/// <param name="Name">The configured slice name.</param>
/// <param name="Required">Whether the slice runs any test project.</param>
/// <param name="Tests">The selected test projects of the slice, in ordinal order.</param>
internal sealed record SliceSelection(string Name, bool Required, SelectedTest[] Tests);
