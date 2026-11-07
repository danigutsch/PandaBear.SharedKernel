using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Mediator.Tests;

/// <summary>
/// Provides the test-only requests, handlers, rules and exceptions used by the unique violation behavior tests.
/// </summary>
internal static class UniqueViolationTestTypes
{
    /// <summary>The internal rule name planted in thrown exceptions to prove it never reaches a result.</summary>
    internal const string MappedRule = "tour-code-secret-rule";

    /// <summary>A rule that no test maps.</summary>
    internal const string UnmappedRule = "unmapped-secret-rule";

    /// <summary>The text planted in the exception inner exception.</summary>
    internal const string Secret = "row-42 secret-token";

    /// <summary>The field the mapped rule names.</summary>
    internal const string MappedField = "email";

    /// <summary>The caller-authored message of the mapped rule.</summary>
    internal const string MappedMessage = "An account with this email already exists.";

    /// <summary>A request that produces a <see cref="Result"/>.</summary>
    internal sealed record CreateTour : IRequest<Result>;

    /// <summary>A request that produces a <see cref="Result{T}"/>.</summary>
    internal sealed record LoadTour : IRequest<Result<string>>;

    /// <summary>Handler for <see cref="CreateTour"/> that throws the exception it was given.</summary>
    /// <param name="failure">The exception to throw.</param>
    internal sealed class ThrowingCreateHandler(Exception failure) : IRequestHandler<CreateTour, Result>
    {
        /// <inheritdoc />
        public ValueTask<Result> Handle(CreateTour request, CancellationToken ct) => throw failure;
    }

    /// <summary>Creates a violation of <paramref name="rule"/> whose inner exception contains <see cref="Secret"/>.</summary>
    /// <param name="rule">The violated rule.</param>
    internal static UniqueConstraintViolationException CreateViolation(string rule) =>
        new(rule, new InvalidOperationException(Secret));

    /// <summary>Creates a map with <see cref="MappedRule"/> mapped.</summary>
    internal static UniqueRuleMap CreateMap() => new UniqueRuleMap().Add(MappedRule, MappedField, MappedMessage);
}
