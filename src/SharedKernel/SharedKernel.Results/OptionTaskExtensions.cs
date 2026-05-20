namespace SharedKernel.Results;

/// <summary>
/// Extension methods for composing task-based option flows.
/// </summary>
public static class OptionTaskExtensions
{
    /// <summary>
    /// Maps the successful value of an asynchronous option.
    /// </summary>
    public static async Task<Option<TResult>> Map<T, TResult>(this Task<Option<T>> source, Func<T, TResult> map)
        where T : notnull
        where TResult : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        (await source.ConfigureAwait(false)).Map(map);

    /// <summary>
    /// Maps the successful value of an asynchronous option.
    /// </summary>
    public static async Task<Option<TResult>> Map<T, TResult>(this Task<Option<T>> source, Func<T, Task<TResult>> map)
        where T : notnull
        where TResult : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Map(map).ConfigureAwait(false);

    /// <summary>
    /// Maps the successful value of an asynchronous option.
    /// </summary>
    public static async Task<Option<TResult>> Map<T, TResult>(this Task<Option<T>> source, Func<T, ValueTask<TResult>> map)
        where T : notnull
        where TResult : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Map(map).ConfigureAwait(false);

    /// <summary>
    /// Maps the successful value of an asynchronous option.
    /// </summary>
    public static async ValueTask<Option<TResult>> Map<T, TResult>(this ValueTask<Option<T>> source, Func<T, TResult> map)
        where T : notnull
        where TResult : notnull =>
        (await source.ConfigureAwait(false)).Map(map);

    /// <summary>
    /// Maps the successful value of an asynchronous option.
    /// </summary>
    public static async ValueTask<Option<TResult>> Map<T, TResult>(this ValueTask<Option<T>> source, Func<T, Task<TResult>> map)
        where T : notnull
        where TResult : notnull =>
        await (await source.ConfigureAwait(false)).Map(map).ConfigureAwait(false);

    /// <summary>
    /// Maps the successful value of an asynchronous option.
    /// </summary>
    public static async ValueTask<Option<TResult>> Map<T, TResult>(this ValueTask<Option<T>> source, Func<T, ValueTask<TResult>> map)
        where T : notnull
        where TResult : notnull =>
        await (await source.ConfigureAwait(false)).Map(map).ConfigureAwait(false);

    /// <summary>
    /// Binds the successful value of an asynchronous option.
    /// </summary>
    public static async Task<Option<TResult>> Bind<T, TResult>(this Task<Option<T>> source, Func<T, Option<TResult>> bind)
        where T : notnull
        where TResult : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        (await source.ConfigureAwait(false)).Bind(bind);

    /// <summary>
    /// Binds the successful value of an asynchronous option.
    /// </summary>
    public static async Task<Option<TResult>> Bind<T, TResult>(this Task<Option<T>> source, Func<T, Task<Option<TResult>>> bind)
        where T : notnull
        where TResult : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Bind(bind).ConfigureAwait(false);

    /// <summary>
    /// Binds the successful value of an asynchronous option.
    /// </summary>
    public static async Task<Option<TResult>> Bind<T, TResult>(this Task<Option<T>> source, Func<T, ValueTask<Option<TResult>>> bind)
        where T : notnull
        where TResult : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Bind(bind).ConfigureAwait(false);

    /// <summary>
    /// Binds the successful value of an asynchronous option.
    /// </summary>
    public static async ValueTask<Option<TResult>> Bind<T, TResult>(this ValueTask<Option<T>> source, Func<T, Option<TResult>> bind)
        where T : notnull
        where TResult : notnull =>
        (await source.ConfigureAwait(false)).Bind(bind);

    /// <summary>
    /// Binds the successful value of an asynchronous option.
    /// </summary>
    public static async ValueTask<Option<TResult>> Bind<T, TResult>(this ValueTask<Option<T>> source, Func<T, Task<Option<TResult>>> bind)
        where T : notnull
        where TResult : notnull =>
        await (await source.ConfigureAwait(false)).Bind(bind).ConfigureAwait(false);

    /// <summary>
    /// Binds the successful value of an asynchronous option.
    /// </summary>
    public static async ValueTask<Option<TResult>> Bind<T, TResult>(this ValueTask<Option<T>> source, Func<T, ValueTask<Option<TResult>>> bind)
        where T : notnull
        where TResult : notnull =>
        await (await source.ConfigureAwait(false)).Bind(bind).ConfigureAwait(false);

    /// <summary>
    /// Matches an asynchronous option.
    /// </summary>
    public static async Task<TResult> Match<T, TResult>(this Task<Option<T>> source, Func<T, TResult> whenSome, Func<TResult> whenNone)
        where T : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        (await source.ConfigureAwait(false)).Match(whenSome, whenNone);

    /// <summary>
    /// Matches an asynchronous option.
    /// </summary>
    public static async Task<TResult> Match<T, TResult>(this Task<Option<T>> source, Func<T, Task<TResult>> whenSome, Func<Task<TResult>> whenNone)
        where T : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Match(whenSome, whenNone).ConfigureAwait(false);

    /// <summary>
    /// Matches an asynchronous option.
    /// </summary>
    public static async Task<TResult> Match<T, TResult>(this Task<Option<T>> source, Func<T, ValueTask<TResult>> whenSome, Func<ValueTask<TResult>> whenNone)
        where T : notnull =>
        source is null
            ? throw new ArgumentNullException(nameof(source))
            :
        await (await source.ConfigureAwait(false)).Match(whenSome, whenNone).ConfigureAwait(false);

    /// <summary>
    /// Matches an asynchronous option.
    /// </summary>
    public static async ValueTask<TResult> Match<T, TResult>(this ValueTask<Option<T>> source, Func<T, TResult> whenSome, Func<TResult> whenNone)
        where T : notnull =>
        (await source.ConfigureAwait(false)).Match(whenSome, whenNone);

    /// <summary>
    /// Matches an asynchronous option.
    /// </summary>
    public static async ValueTask<TResult> Match<T, TResult>(this ValueTask<Option<T>> source, Func<T, Task<TResult>> whenSome, Func<Task<TResult>> whenNone)
        where T : notnull =>
        await (await source.ConfigureAwait(false)).Match(whenSome, whenNone).ConfigureAwait(false);

    /// <summary>
    /// Matches an asynchronous option.
    /// </summary>
    public static async ValueTask<TResult> Match<T, TResult>(this ValueTask<Option<T>> source, Func<T, ValueTask<TResult>> whenSome, Func<ValueTask<TResult>> whenNone)
        where T : notnull =>
        await (await source.ConfigureAwait(false)).Match(whenSome, whenNone).ConfigureAwait(false);
}
