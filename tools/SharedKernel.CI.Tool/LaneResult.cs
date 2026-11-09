namespace PandaBear.CI.Tool;

/// <summary>What one lane of <c>local-check</c> did.</summary>
/// <param name="Name">The lane: <c>whitespace</c>, <c>build</c>, or <c>tests</c>.</param>
/// <param name="Status">How the lane ended.</param>
/// <param name="Detail">What the lane checked, why it failed, or why it did not run; control characters are already escaped.</param>
/// <param name="Duration">How long the lane took.</param>
internal sealed record LaneResult(string Name, LaneStatus Status, string Detail, TimeSpan Duration);
