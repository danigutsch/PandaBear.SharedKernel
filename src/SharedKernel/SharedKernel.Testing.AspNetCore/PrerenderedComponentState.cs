using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace PandaBear.Testing.AspNetCore;

/// <summary>
/// Reads the component state that a prerendered Blazor page carries, and checks that the Interactive Server state fits in the
/// message that starts the circuit.
/// </summary>
/// <remarks>
/// A prerendered page writes the state of its <c>[PersistentState]</c> properties into an HTML comment,
/// <c>&lt;!--Blazor-Server-Component-State:…--&gt;</c> for Interactive Server and
/// <c>&lt;!--Blazor-WebAssembly-Component-State:…--&gt;</c> for Interactive WebAssembly (the markers of
/// <c>Microsoft.AspNetCore.Components.Endpoints</c> in .NET 11). The browser sends the server state back to the hub when it starts
/// the circuit, and SignalR closes the connection when an incoming message exceeds <c>HubOptions.MaximumReceiveMessageSize</c>, so
/// a large state leaves the page without interactivity. Read the HTML as the server sent it, for example from
/// <c>HttpClient.GetStringAsync</c> or a Playwright response's <c>TextAsync</c>, not from the document after the script ran.
/// Sizes are the UTF-8 bytes between a marker and its closing <c>--&gt;</c>, summed over every comment of that kind.
/// </remarks>
public static class PrerenderedComponentState
{
    /// <summary>The opening of the comment that carries the Interactive Server state.</summary>
    public const string ServerStateMarker = "<!--Blazor-Server-Component-State:";

    /// <summary>The opening of the comment that carries the Interactive WebAssembly state.</summary>
    public const string WebAssemblyStateMarker = "<!--Blazor-WebAssembly-Component-State:";

    /// <summary>The Microsoft Learn guidance on prerendered state size and the SignalR message size limit.</summary>
    public const string GuidanceUrl =
        "https://learn.microsoft.com/aspnet/core/blazor/fundamentals/signalr?view=aspnetcore-11.0#prerendered-state-size-and-signalr-message-size-limit";

    private const string CommentEnd = "-->";

    /// <summary>
    /// Gets SignalR's default <c>HubOptions.MaximumReceiveMessageSize</c> (32 KB), read from the framework's own defaults
    /// (<see cref="HubOptionsSetup"/>), so it follows the framework rather than a copied number.
    /// </summary>
    public static long DefaultMaximumReceiveMessageSize { get; } = ReadDefaultMaximumReceiveMessageSize();

    /// <summary>Measures the Interactive Server state of a prerendered page.</summary>
    /// <param name="html">The HTML of the page as the server sent it.</param>
    /// <returns>The state's size in UTF-8 bytes, or 0 when the page carries none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="html"/> is null.</exception>
    /// <exception cref="FormatException">A state comment is not closed.</exception>
    public static int ServerStateSize(string html) => StateSize(html, ServerStateMarker);

    /// <summary>Measures the Interactive WebAssembly state of a prerendered page.</summary>
    /// <param name="html">The HTML of the page as the server sent it.</param>
    /// <returns>The state's size in UTF-8 bytes, or 0 when the page carries none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="html"/> is null.</exception>
    /// <exception cref="FormatException">A state comment is not closed.</exception>
    public static int WebAssemblyStateSize(string html) => StateSize(html, WebAssemblyStateMarker);

    /// <summary>
    /// Verifies that the Interactive Server state of a prerendered page stays below SignalR's default receive limit,
    /// <see cref="DefaultMaximumReceiveMessageSize"/>. A page without state passes.
    /// </summary>
    /// <param name="html">The HTML of the page as the server sent it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="html"/> is null.</exception>
    /// <exception cref="FormatException">A state comment is not closed.</exception>
    /// <exception cref="InvalidOperationException">The state is not below the limit; the message names the size, the limit and <see cref="GuidanceUrl"/>.</exception>
    public static void ShouldKeepServerStateBelow(string html) => ShouldKeepServerStateBelow(html, DefaultMaximumReceiveMessageSize);

    /// <summary>Verifies that the Interactive Server state of a prerendered page stays below a limit. A page without state passes.</summary>
    /// <param name="html">The HTML of the page as the server sent it.</param>
    /// <param name="limit">
    /// The limit in bytes, typically the application's <c>MaximumReceiveMessageSize</c>. The circuit's start message carries more than
    /// the state, so a lower limit leaves headroom.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="html"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    /// <exception cref="FormatException">A state comment is not closed.</exception>
    /// <exception cref="InvalidOperationException">The state is not below the limit; the message names the size, the limit and <see cref="GuidanceUrl"/>.</exception>
    public static void ShouldKeepServerStateBelow(string html, long limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var size = ServerStateSize(html);
        if (size < limit)
        {
            return;
        }

        throw new InvalidOperationException(string.Format(
            CultureInfo.InvariantCulture,
            "The prerendered Interactive Server component state is {0} bytes, which is not below the limit of {1} bytes. The browser sends this state in the message that starts the circuit, and SignalR closes a connection whose incoming message exceeds HubOptions.MaximumReceiveMessageSize, so the page would never become interactive. Put less data into [PersistentState] (page or search large lists instead); see {2}",
            size,
            limit,
            GuidanceUrl));
    }

    private static int StateSize(string html, string marker)
    {
        ArgumentNullException.ThrowIfNull(html);

        var size = 0;
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        while (start >= 0)
        {
            var payloadStart = start + marker.Length;
            var end = html.IndexOf(CommentEnd, payloadStart, StringComparison.Ordinal);
            if (end < 0)
            {
                throw new FormatException("The page has a " + marker + " comment that is not closed with " + CommentEnd + ".");
            }

            size = checked(size + Encoding.UTF8.GetByteCount(html.AsSpan(payloadStart, end - payloadStart)));
            start = html.IndexOf(marker, end + CommentEnd.Length, StringComparison.Ordinal);
        }

        return size;
    }

    private static long ReadDefaultMaximumReceiveMessageSize()
    {
        var options = new HubOptions();
        new HubOptionsSetup(Array.Empty<IHubProtocol>()).Configure(options);
        return options.MaximumReceiveMessageSize
            ?? throw new InvalidOperationException("SignalR's default options set no MaximumReceiveMessageSize.");
    }
}
