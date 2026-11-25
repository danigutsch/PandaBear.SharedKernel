using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Keeps a content revision: a single row whose value grows by one in the same transaction as every content change, so readers in any
/// process can cache by it (see <c>RevisionCache</c> in <c>SharedKernel.Caching</c>) without a message bus.
/// </summary>
/// <remarks>
/// The application owns the row: an entity type with a <see cref="long"/> value property, mapped to a table that holds exactly one row
/// (for example seeded with <c>HasData</c> at value 0). Every row of the table is incremented, so keep it at one.
/// </remarks>
public static class RevisionDbContextExtensions
{
    private const DynamicallyAccessedMemberTypes EntityMembers =
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors
        | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields
        | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties
        | DynamicallyAccessedMemberTypes.Interfaces;

    /// <summary>
    /// Adds one to the revision with a single <c>UPDATE ... SET Value = Value + 1</c>, so concurrent writers never lose a bump. It runs in
    /// the context's current transaction, if any.
    /// </summary>
    /// <typeparam name="TRevision">The revision row's entity type.</typeparam>
    /// <param name="dbContext">The context.</param>
    /// <param name="revision">The revision value property, such as <c>row =&gt; row.Value</c>.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The number of rows updated.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dbContext"/> or <paramref name="revision"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="revision"/> is not a property of the row.</exception>
    public static Task<int> IncrementRevision<[DynamicallyAccessedMembers(EntityMembers)] TRevision>(this DbContext dbContext, Expression<Func<TRevision, long>> revision, CancellationToken ct)
        where TRevision : class
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        var increment = Increment(revision);

        return dbContext.Set<TRevision>().ExecuteUpdateAsync(setters => setters.SetProperty(revision, increment), ct);
    }

    /// <summary>
    /// Saves the changes and, when any entity other than the revision row was added, modified or deleted, increments the revision in the same
    /// transaction (see <see cref="IncrementRevision{TRevision}"/>): a reader never sees new content under an old revision for longer than its
    /// check interval, and a failed save leaves the revision alone. Changes are detected first, so a change made in place is found even with
    /// automatic detection off. Without a current transaction one is opened and committed around the save and the increment; inside the
    /// caller's transaction both commit or roll back with it. A save without content changes calls <paramref name="saveChanges"/> alone.
    /// </summary>
    /// <remarks>
    /// Call it from the context's <c>SaveChangesAsync(bool, CancellationToken)</c> override, passing the base implementation, and refuse the
    /// synchronous <c>SaveChanges</c> so that no content change skips the increment:
    /// <code language="csharp">
    /// public override Task&lt;int&gt; SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =&gt;
    ///     this.SaveChangesIncrementingRevision&lt;ContentRevision&gt;(
    ///         row =&gt; row.Value,
    ///         token =&gt; base.SaveChangesAsync(acceptAllChangesOnSuccess, token),
    ///         cancellationToken);
    /// </code>
    /// A change made with SQL outside the context does not increment the revision.
    /// </remarks>
    /// <typeparam name="TRevision">The revision row's entity type.</typeparam>
    /// <param name="dbContext">The context.</param>
    /// <param name="revision">The revision value property, such as <c>row =&gt; row.Value</c>.</param>
    /// <param name="saveChanges">The context's base save.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The number of state entries <paramref name="saveChanges"/> wrote.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="revision"/> is not a property of the row.</exception>
    public static async Task<int> SaveChangesIncrementingRevision<[DynamicallyAccessedMembers(EntityMembers)] TRevision>(
        this DbContext dbContext,
        Expression<Func<TRevision, long>> revision,
        Func<CancellationToken, Task<int>> saveChanges,
        CancellationToken ct)
        where TRevision : class
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        var increment = Increment(revision);
        ArgumentNullException.ThrowIfNull(saveChanges);

        if (!HasContentChanges<TRevision>(dbContext))
        {
            return await saveChanges(ct).ConfigureAwait(false);
        }

        var transaction = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false)
            : null;
        try
        {
            var written = await saveChanges(ct).ConfigureAwait(false);
            await dbContext.Set<TRevision>().ExecuteUpdateAsync(setters => setters.SetProperty(revision, increment), ct).ConfigureAwait(false);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct).ConfigureAwait(false);
            }

            return written;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static Expression<Func<TRevision, long>> Increment<TRevision>(Expression<Func<TRevision, long>> revision)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (revision.Body is not MemberExpression { Expression: ParameterExpression parameter } || parameter != revision.Parameters[0])
        {
            throw new ArgumentException("The revision must be a property of the row, such as row => row.Value.", nameof(revision));
        }

        return Expression.Lambda<Func<TRevision, long>>(Expression.Add(revision.Body, Expression.Constant(1L)), revision.Parameters);
    }

    private static bool HasContentChanges<TRevision>(DbContext dbContext)
        where TRevision : class
    {
        dbContext.ChangeTracker.DetectChanges();
        return dbContext.ChangeTracker.Entries()
            .Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && entry.Entity is not TRevision);
    }
}
