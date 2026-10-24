namespace PandaBear.CI.Tool;

/// <summary>A finished phase of <c>run</c>.</summary>
/// <param name="Name">The phase, such as <c>build</c> or <c>openapi-admin</c>.</param>
/// <param name="Status"><c>passed</c> or <c>failed</c>.</param>
/// <param name="StartedAt">When the phase started.</param>
/// <param name="Duration">How long the phase ran.</param>
/// <param name="Log">The phase log, relative to the execution directory.</param>
internal sealed record RunPhase(string Name, string Status, DateTimeOffset StartedAt, TimeSpan Duration, string Log);
