using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Tests;

/// <summary>Builds component inputs for the Blazor helper tests.</summary>
internal static class ComponentFixtures
{
    /// <summary>Creates error summary items.</summary>
    /// <param name="entries">The target id and message pairs.</param>
    /// <returns>The items.</returns>
    public static ErrorSummaryItem[] Items(params (string Target, string Message)[] entries)
        => entries.Select(entry => new ErrorSummaryItem(entry.Target, entry.Message)).ToArray();

    /// <summary>Creates a fragment that renders one component without parameters.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>The fragment.</returns>
    public static RenderFragment Child<T>()
        where T : IComponent => builder =>
        {
            builder.OpenComponent<T>(0);
            builder.CloseComponent();
        };

    /// <summary>Creates a fragment that renders a <see cref="ThrowingChild" /> whose behavior is read at render time.</summary>
    /// <param name="shouldThrow">Whether the child throws.</param>
    /// <returns>The fragment.</returns>
    public static RenderFragment ThrowingFragment(Func<bool> shouldThrow) => builder =>
    {
        builder.OpenComponent<ThrowingChild>(0);
        builder.AddAttribute(1, nameof(ThrowingChild.Throw), shouldThrow());
        builder.CloseComponent();
    };

    /// <summary>Creates the exception a focus call raises when the circuit is gone.</summary>
    /// <param name="kind">The failure kind: disconnected, canceled, or disposed.</param>
    /// <returns>The exception.</returns>
    public static Exception FocusFailure(string kind) => kind switch
    {
        "disconnected" => new Microsoft.JSInterop.JSDisconnectedException("gone"),
        "canceled" => new TaskCanceledException(),
        _ => new ObjectDisposedException("renderer"),
    };

    /// <summary>Starts an upload operation.</summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="total">The declared length.</param>
    /// <returns>The state with a queued operation.</returns>
    public static UploadOperationState Started(string fileName = "customers.csv", long total = 100)
    {
        var state = new UploadOperationState();
        state.Begin(fileName, total, isRetry: false);
        return state;
    }
}
