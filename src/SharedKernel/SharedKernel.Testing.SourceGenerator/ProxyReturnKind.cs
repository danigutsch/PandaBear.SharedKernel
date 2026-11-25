namespace SharedKernel.Testing.SourceGenerator;

/// <summary>How a proxied method returns, which decides how the proxy awaits it and whether it has a result.</summary>
internal enum ProxyReturnKind
{
    /// <summary>A synchronous method without a result.</summary>
    Void,

    /// <summary>A synchronous method with a result.</summary>
    Value,

    /// <summary>A method that returns <c>Task</c>.</summary>
    Task,

    /// <summary>A method that returns <c>ValueTask</c>.</summary>
    ValueTask,

    /// <summary>A method that returns <c>Task&lt;T&gt;</c>.</summary>
    TaskOfResult,

    /// <summary>A method that returns <c>ValueTask&lt;T&gt;</c>.</summary>
    ValueTaskOfResult,
}
