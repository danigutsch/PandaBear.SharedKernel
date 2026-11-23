namespace PandaBear.Blazor.Tests;

/// <summary>An image option of the picker tests.</summary>
/// <param name="Id">The key.</param>
/// <param name="Label">The label.</param>
/// <param name="Thumbnail">The thumbnail address, or <see langword="null"/>.</param>
public sealed record PickerImage(int Id, string Label, string? Thumbnail);
