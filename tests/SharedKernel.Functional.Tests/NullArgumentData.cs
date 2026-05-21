namespace SharedKernel.Functional.Tests;

internal static class NullArgumentData
{
    public static string String() => Ref<string>();

    public static Task<T> Task<T>() => Ref<Task<T>>();

    public static Func<T, TResult> Func<T, TResult>() => Ref<Func<T, TResult>>();

    public static Func<T, Task<TResult>> TaskFunc<T, TResult>() => Ref<Func<T, Task<TResult>>>();

    public static Func<T, ValueTask<TResult>> ValueTaskFunc<T, TResult>() => Ref<Func<T, ValueTask<TResult>>>();

    public static Func<TResult> Factory<TResult>() => Ref<Func<TResult>>();

    public static Func<Task<TResult>> TaskFactory<TResult>() => Ref<Func<Task<TResult>>>();

    public static Func<ValueTask<TResult>> ValueTaskFactory<TResult>() => Ref<Func<ValueTask<TResult>>>();

    public static string[] StringArray() => Ref<string[]>();

    private static T Ref<T>() where T : class =>
        System.Linq.Expressions.Expression.Lambda<Func<T>>(
            System.Linq.Expressions.Expression.Constant(null, typeof(T)))
            .Compile()();
}
