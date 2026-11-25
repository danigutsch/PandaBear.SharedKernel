namespace SharedKernel.Forgejo;

/// <summary>One page of a Forgejo list and the paging headers that came with it.</summary>
/// <typeparam name="T">The type of the items.</typeparam>
/// <param name="Items">The items of the page.</param>
/// <param name="Link">The <c>Link</c> header, or <see langword="null"/> when the response had none.</param>
/// <param name="TotalCount">The <c>X-Total-Count</c> header, or <see langword="null"/> when the response had no valid one.</param>
public sealed record ForgejoPage<T>(IReadOnlyList<T> Items, string? Link, long? TotalCount);
