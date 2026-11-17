namespace PandaBear.Repositories;

/// <summary>
/// The target frameworks that the projects matching one pattern must build.
/// </summary>
/// <param name="Include">A path pattern of the tracked project files that the rule covers; <c>*</c> also matches <c>/</c>.</param>
/// <param name="Frameworks">The target framework monikers that each covered project declares, in any order.</param>
/// <param name="RollForward">
/// The <c>RollForward</c> value that each covered project must have, or <see langword="null"/> to leave it unchecked.
/// </param>
public sealed record TargetFrameworkRule(string Include, IReadOnlyList<string> Frameworks, string? RollForward = null);
