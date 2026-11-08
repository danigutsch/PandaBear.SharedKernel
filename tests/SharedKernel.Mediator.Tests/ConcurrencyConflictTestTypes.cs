using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Mediator.Tests;

/// <summary>
/// Provides the test-only requests, handlers and exceptions used by the concurrency conflict behavior tests.
/// </summary>
internal static class ConcurrencyConflictTestTypes
{
    /// <summary>The text planted in thrown exceptions to prove it never reaches a result.</summary>
    internal const string Secret = "row-42 secret-token";

    /// <summary>A request that produces a <see cref="Result"/>.</summary>
    internal sealed record RenameTour : IRequest<Result>;

    /// <summary>A request that produces a <see cref="Result{T}"/>.</summary>
    internal sealed record LoadTour : IRequest<Result<string>>;

    /// <summary>Handler for <see cref="RenameTour"/> that throws the exception it was given.</summary>
    /// <param name="failure">The exception to throw.</param>
    internal sealed class ThrowingRenameHandler(Exception failure) : IRequestHandler<RenameTour, Result>
    {
        /// <inheritdoc />
        public ValueTask<Result> Handle(RenameTour request, CancellationToken ct) => throw failure;
    }

    /// <summary>Creates a conflict exception whose message and inner exception contain <see cref="Secret"/>.</summary>
    internal static ConcurrencyConflictException CreateConflict() =>
        new($"conflict on {Secret}", new InvalidOperationException(Secret));
}
