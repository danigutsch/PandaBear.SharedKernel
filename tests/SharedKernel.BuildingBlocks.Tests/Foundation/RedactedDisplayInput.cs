namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>A nested value held by a record with redacted display.</summary>
/// <param name="Value">The synthetic payload supplied to the test.</param>
internal sealed record RedactedDisplayInput(string Value);
