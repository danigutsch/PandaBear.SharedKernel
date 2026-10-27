namespace PandaBear.Blazor.Tests;

/// <summary>A captured counter measurement.</summary>
/// <param name="Instrument">The instrument name.</param>
/// <param name="Value">The measured value.</param>
/// <param name="Tags">The tags by name.</param>
internal sealed record RecordedMeasurement(string Instrument, long Value, IReadOnlyDictionary<string, object?> Tags);
