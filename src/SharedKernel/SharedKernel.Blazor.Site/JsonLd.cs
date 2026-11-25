using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SharedKernel.Blazor.Site;

/// <summary>
/// Structured data for search engines: a <c>&lt;script type="application/ld+json"&gt;</c> holding <see cref="Data" />, written with
/// <see cref="JavaScriptEncoder.Default" />, which escapes <c>&lt;</c>, <c>&gt;</c>, <c>&amp;</c> and quotes in every name and value,
/// so no text in the data can close the script or open a comment. It writes no other attribute and takes no class.
/// </summary>
public sealed class JsonLd : ComponentBase
{
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.Default };

    /// <summary>
    /// Gets or sets the data, a <see cref="JsonObject" /> such as a schema.org <c>TouristTrip</c>, or a <see cref="JsonArray" /> of such
    /// objects. Mark up only what the page shows.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public JsonNode? Data { get; set; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><see cref="Data" /> is null.</exception>
    /// <exception cref="ArgumentException"><see cref="Data" /> is neither an object nor an array.</exception>
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Data);
        if (Data is not (JsonObject or JsonArray))
        {
            throw new ArgumentException("JSON-LD data must be an object or an array.", nameof(Data));
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenElement(0, "script");
        builder.AddAttribute(1, "type", "application/ld+json");
        builder.AddMarkupContent(2, Serialize(Data!));
        builder.CloseElement();
    }

    private static string Serialize(JsonNode data)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            data.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
