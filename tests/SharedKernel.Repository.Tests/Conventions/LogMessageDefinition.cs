namespace PandaBear.Repository.Tests.Conventions;

/// <summary>One <c>[LoggerMessage]</c> definition in the library sources.</summary>
/// <param name="Package">The package folder, which is the assembly name.</param>
/// <param name="File">The declaring file, relative to <c>src/SharedKernel</c>.</param>
/// <param name="EventId">The resolved event id, or <see langword="null"/> when it is missing or not a constant.</param>
/// <param name="EventName">The event name, or <see langword="null"/> when it is missing.</param>
/// <param name="Placeholders">The placeholder names of the message template.</param>
internal sealed record LogMessageDefinition(string Package, string File, int? EventId, string? EventName, IReadOnlyList<string> Placeholders);
