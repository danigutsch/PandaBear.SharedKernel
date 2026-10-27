namespace PandaBear.Results.Tests;

/// <summary>
/// Supplies null values of reference types without the compiler's nullability warnings, for argument-validation tests.
/// </summary>
internal static class NullArgumentData
{
    /// <summary>
    /// Returns a null string.
    /// </summary>
    public static string String() => Ref<string>();

    /// <summary>
    /// Returns a null task of T.
    /// </summary>
    public static Task<T> Task<T>() => Ref<Task<T>>();

    /// <summary>
    /// Returns a null synchronous mapping delegate.
    /// </summary>
    public static Func<T, TResult> Func<T, TResult>() => Ref<Func<T, TResult>>();

    /// <summary>
    /// Returns a null delegate that maps a value to a task.
    /// </summary>
    public static Func<T, Task<TResult>> TaskFunc<T, TResult>() => Ref<Func<T, Task<TResult>>>();

    /// <summary>
    /// Returns a null delegate that maps a value to a value task.
    /// </summary>
    public static Func<T, ValueTask<TResult>> ValueTaskFunc<T, TResult>() => Ref<Func<T, ValueTask<TResult>>>();

    /// <summary>
    /// Returns a null synchronous factory delegate.
    /// </summary>
    public static Func<TResult> Factory<TResult>() => Ref<Func<TResult>>();

    /// <summary>
    /// Returns a null factory delegate that produces a task.
    /// </summary>
    public static Func<Task<TResult>> TaskFactory<TResult>() => Ref<Func<Task<TResult>>>();

    /// <summary>
    /// Returns a null factory delegate that produces a value task.
    /// </summary>
    public static Func<ValueTask<TResult>> ValueTaskFactory<TResult>() => Ref<Func<ValueTask<TResult>>>();

    /// <summary>
    /// Returns a null string array.
    /// </summary>
    public static string[] StringArray() => Ref<string[]>();

    private static T Ref<T>() where T : class =>
        System.Linq.Expressions.Expression.Lambda<Func<T>>(
            System.Linq.Expressions.Expression.Constant(null, typeof(T)))
            .Compile()();
}
