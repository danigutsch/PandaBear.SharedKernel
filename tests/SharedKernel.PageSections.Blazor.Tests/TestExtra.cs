namespace PandaBear.PageSections.Blazor.Tests;

/// <summary>An application extension: a settings block to show and a display condition, as ViajantesTurismo V2's.</summary>
/// <param name="Block">The block, 0 for none.</param>
/// <param name="OnRequest">Whether the section is shown only for an offering on request.</param>
public readonly record struct TestExtra(int Block, bool OnRequest = false);
