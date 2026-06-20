namespace SharedKernel.Results;

/// <summary>
/// Extension methods for composing task-based result flows.
/// </summary>
public static class ResultTaskExtensions
{
    /// <summary>
    /// Maps the successful value of an asynchronous result.
    /// </summary>
    public static async Task<Result<TResult>> Map<T, TResult>(this Task<Result<T>> source, Func<T, TResult> map)
        where T : notnull
        where TResult : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        (await source.ConfigureAwait(false)).Map(map);

    /// <summary>
    /// Maps the successful value of an asynchronous result.
    /// </summary>
    public static async Task<Result<TResult>> Map<T, TResult>(this Task<Result<T>> source, Func<T, Task<TResult>> map)
        where T : notnull
        where TResult : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Map(map).ConfigureAwait(false);

    /// <summary>
    /// Maps the successful value of an asynchronous result.
    /// </summary>
    public static async Task<Result<TResult>> Map<T, TResult>(this Task<Result<T>> source, Func<T, ValueTask<TResult>> map)
        where T : notnull
        where TResult : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Map(map).ConfigureAwait(false);

    /// <summary>
    /// Maps the successful value of an asynchronous result.
    /// </summary>
    public static async ValueTask<Result<TResult>> Map<T, TResult>(this ValueTask<Result<T>> source, Func<T, TResult> map)
        where T : notnull
        where TResult : notnull =>
        (await source.ConfigureAwait(false)).Map(map);

    /// <summary>
    /// Maps the successful value of an asynchronous result.
    /// </summary>
    public static async ValueTask<Result<TResult>> Map<T, TResult>(this ValueTask<Result<T>> source, Func<T, Task<TResult>> map)
        where T : notnull
        where TResult : notnull =>
        await (await source.ConfigureAwait(false)).Map(map).ConfigureAwait(false);

    /// <summary>
    /// Maps the successful value of an asynchronous result.
    /// </summary>
    public static async ValueTask<Result<TResult>> Map<T, TResult>(this ValueTask<Result<T>> source, Func<T, ValueTask<TResult>> map)
        where T : notnull
        where TResult : notnull =>
        await (await source.ConfigureAwait(false)).Map(map).ConfigureAwait(false);

    /// <summary>
    /// Binds the successful value of an asynchronous result.
    /// </summary>
    public static async Task<Result<TResult>> Bind<T, TResult>(this Task<Result<T>> source, Func<T, Result<TResult>> bind)
        where T : notnull
        where TResult : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        (await source.ConfigureAwait(false)).Bind(bind);

    /// <summary>
    /// Binds the successful value of an asynchronous result.
    /// </summary>
    public static async Task<Result<TResult>> Bind<T, TResult>(this Task<Result<T>> source, Func<T, Task<Result<TResult>>> bind)
        where T : notnull
        where TResult : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Bind(bind).ConfigureAwait(false);

    /// <summary>
    /// Binds the successful value of an asynchronous result.
    /// </summary>
    public static async Task<Result<TResult>> Bind<T, TResult>(this Task<Result<T>> source, Func<T, ValueTask<Result<TResult>>> bind)
        where T : notnull
        where TResult : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Bind(bind).ConfigureAwait(false);

    /// <summary>
    /// Binds the successful value of an asynchronous result.
    /// </summary>
    public static async ValueTask<Result<TResult>> Bind<T, TResult>(this ValueTask<Result<T>> source, Func<T, Result<TResult>> bind)
        where T : notnull
        where TResult : notnull =>
        (await source.ConfigureAwait(false)).Bind(bind);

    /// <summary>
    /// Binds the successful value of an asynchronous result.
    /// </summary>
    public static async ValueTask<Result<TResult>> Bind<T, TResult>(this ValueTask<Result<T>> source, Func<T, Task<Result<TResult>>> bind)
        where T : notnull
        where TResult : notnull =>
        await (await source.ConfigureAwait(false)).Bind(bind).ConfigureAwait(false);

    /// <summary>
    /// Binds the successful value of an asynchronous result.
    /// </summary>
    public static async ValueTask<Result<TResult>> Bind<T, TResult>(this ValueTask<Result<T>> source, Func<T, ValueTask<Result<TResult>>> bind)
        where T : notnull
        where TResult : notnull =>
        await (await source.ConfigureAwait(false)).Bind(bind).ConfigureAwait(false);

    /// <summary>
    /// Ensures the successful value of an asynchronous result satisfies the provided predicate.
    /// </summary>
    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> source, Func<T, bool> predicate, ResultError error)
        where T : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        (await source.ConfigureAwait(false)).Ensure(predicate, error);

    /// <summary>
    /// Ensures the successful value of an asynchronous result satisfies the provided predicate.
    /// </summary>
    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> source, Func<T, Task<bool>> predicate, ResultError error)
        where T : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Ensure(predicate, error).ConfigureAwait(false);

    /// <summary>
    /// Ensures the successful value of an asynchronous result satisfies the provided predicate.
    /// </summary>
    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> source, Func<T, ValueTask<bool>> predicate, ResultError error)
        where T : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Ensure(predicate, error).ConfigureAwait(false);

    /// <summary>
    /// Ensures the successful value of an asynchronous result satisfies the provided predicate.
    /// </summary>
    public static async ValueTask<Result<T>> Ensure<T>(this ValueTask<Result<T>> source, Func<T, bool> predicate, ResultError error)
        where T : notnull =>
        (await source.ConfigureAwait(false)).Ensure(predicate, error);

    /// <summary>
    /// Ensures the successful value of an asynchronous result satisfies the provided predicate.
    /// </summary>
    public static async ValueTask<Result<T>> Ensure<T>(this ValueTask<Result<T>> source, Func<T, Task<bool>> predicate, ResultError error)
        where T : notnull =>
        await (await source.ConfigureAwait(false)).Ensure(predicate, error).ConfigureAwait(false);

    /// <summary>
    /// Ensures the successful value of an asynchronous result satisfies the provided predicate.
    /// </summary>
    public static async ValueTask<Result<T>> Ensure<T>(this ValueTask<Result<T>> source, Func<T, ValueTask<bool>> predicate, ResultError error)
        where T : notnull =>
        await (await source.ConfigureAwait(false)).Ensure(predicate, error).ConfigureAwait(false);

    /// <summary>
    /// Matches an asynchronous result.
    /// </summary>
    public static async Task<TResult> Match<T, TResult>(this Task<Result<T>> source, Func<T, TResult> whenSuccess, Func<ResultError, TResult> whenFailure)
        where T : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        (await source.ConfigureAwait(false)).Match(whenSuccess, whenFailure);

    /// <summary>
    /// Matches an asynchronous result.
    /// </summary>
    public static async Task<TResult> Match<T, TResult>(this Task<Result<T>> source, Func<T, Task<TResult>> whenSuccess, Func<ResultError, Task<TResult>> whenFailure)
        where T : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Match(whenSuccess, whenFailure).ConfigureAwait(false);

    /// <summary>
    /// Matches an asynchronous result.
    /// </summary>
    public static async Task<TResult> Match<T, TResult>(this Task<Result<T>> source, Func<T, ValueTask<TResult>> whenSuccess, Func<ResultError, ValueTask<TResult>> whenFailure)
        where T : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Match(whenSuccess, whenFailure).ConfigureAwait(false);

    /// <summary>
    /// Matches an asynchronous result.
    /// </summary>
    public static async ValueTask<TResult> Match<T, TResult>(this ValueTask<Result<T>> source, Func<T, TResult> whenSuccess, Func<ResultError, TResult> whenFailure)
        where T : notnull =>
        (await source.ConfigureAwait(false)).Match(whenSuccess, whenFailure);

    /// <summary>
    /// Matches an asynchronous result.
    /// </summary>
    public static async ValueTask<TResult> Match<T, TResult>(this ValueTask<Result<T>> source, Func<T, Task<TResult>> whenSuccess, Func<ResultError, Task<TResult>> whenFailure)
        where T : notnull =>
        await (await source.ConfigureAwait(false)).Match(whenSuccess, whenFailure).ConfigureAwait(false);

    /// <summary>
    /// Matches an asynchronous result.
    /// </summary>
    public static async ValueTask<TResult> Match<T, TResult>(this ValueTask<Result<T>> source, Func<T, ValueTask<TResult>> whenSuccess, Func<ResultError, ValueTask<TResult>> whenFailure)
        where T : notnull =>
        await (await source.ConfigureAwait(false)).Match(whenSuccess, whenFailure).ConfigureAwait(false);

    /// <summary>
    /// Matches an asynchronous non-generic result.
    /// </summary>
    public static async Task<TResult> Match<TResult>(this Task<Result> source, Func<TResult> whenSuccess, Func<ResultError, TResult> whenFailure) =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        (await source.ConfigureAwait(false)).Match(whenSuccess, whenFailure);

    /// <summary>
    /// Matches an asynchronous non-generic result.
    /// </summary>
    public static async Task<TResult> Match<TResult>(this Task<Result> source, Func<Task<TResult>> whenSuccess, Func<ResultError, Task<TResult>> whenFailure) =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Match(whenSuccess, whenFailure).ConfigureAwait(false);

    /// <summary>
    /// Matches an asynchronous non-generic result.
    /// </summary>
    public static async Task<TResult> Match<TResult>(this Task<Result> source, Func<ValueTask<TResult>> whenSuccess, Func<ResultError, ValueTask<TResult>> whenFailure) =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Match(whenSuccess, whenFailure).ConfigureAwait(false);

    /// <summary>
    /// Matches an asynchronous non-generic result.
    /// </summary>
    public static async ValueTask<TResult> Match<TResult>(this ValueTask<Result> source, Func<TResult> whenSuccess, Func<ResultError, TResult> whenFailure) =>
        (await source.ConfigureAwait(false)).Match(whenSuccess, whenFailure);

    /// <summary>
    /// Matches an asynchronous non-generic result.
    /// </summary>
    public static async ValueTask<TResult> Match<TResult>(this ValueTask<Result> source, Func<Task<TResult>> whenSuccess, Func<ResultError, Task<TResult>> whenFailure) =>
        await (await source.ConfigureAwait(false)).Match(whenSuccess, whenFailure).ConfigureAwait(false);

    /// <summary>
    /// Matches an asynchronous non-generic result.
    /// </summary>
    public static async ValueTask<TResult> Match<TResult>(this ValueTask<Result> source, Func<ValueTask<TResult>> whenSuccess, Func<ResultError, ValueTask<TResult>> whenFailure) =>
        await (await source.ConfigureAwait(false)).Match(whenSuccess, whenFailure).ConfigureAwait(false);
}
