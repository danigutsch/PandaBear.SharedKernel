using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Blazor;

/// <summary>Renders the common form field markup.</summary>
internal static class FormFieldMarkup
{
    /// <summary>Renders the label, control, help and messages, or only messages when no input ID is supplied.</summary>
    internal static void Render(
        RenderTreeBuilder builder, string? inputId, string? label, string? describedBy, string? help, bool inline,
        RenderFragment<IReadOnlyDictionary<string, object>>? control, IReadOnlyList<string> messages)
    {
        if (inputId is not null)
        {
            var helpId = help is null ? null : inputId + "-help";
            var attributes = ControlAttributes(inputId, messages.Count, describedBy, helpId);
            if (inline)
            {
                builder.OpenElement(0, "p");
                builder.AddContent(1, control, attributes);
                builder.OpenElement(2, "label");
                builder.AddAttribute(3, "class", "inline");
                builder.AddAttribute(4, "for", inputId);
                builder.AddContent(5, label);
                builder.CloseElement();
                builder.CloseElement();
            }
            else
            {
                builder.OpenElement(6, "label");
                builder.AddAttribute(7, "for", inputId);
                builder.AddContent(8, label);
                builder.CloseElement();
                builder.AddContent(9, control, attributes);
            }

            if (helpId is not null)
            {
                builder.OpenElement(10, "p");
                builder.AddAttribute(11, "id", helpId);
                builder.AddAttribute(12, "class", "help");
                builder.AddContent(13, help);
                builder.CloseElement();
            }
        }

        for (var index = 0; index < messages.Count; index++)
        {
            builder.OpenElement(14, "div");
            builder.AddAttribute(15, "id", inputId is null ? null : MessageId(inputId, index));
            builder.AddAttribute(16, "class", "validation-message");
            builder.AddContent(17, messages[index]);
            builder.CloseElement();
        }
    }

    private static Dictionary<string, object> ControlAttributes(string inputId, int messageCount, string? describedBy, string? helpId)
    {
        var ids = Enumerable.Range(0, messageCount).Select(index => MessageId(inputId, index))
            .Prepend(helpId).Prepend(describedBy?.Trim()).Where(id => !string.IsNullOrEmpty(id));
        var description = string.Join(' ', ids);
        var attributes = new Dictionary<string, object>(StringComparer.Ordinal);
        if (description.Length > 0)
        {
            attributes["aria-describedby"] = description;
        }

        if (messageCount > 0)
        {
            attributes["aria-invalid"] = "true";
        }

        return attributes;
    }

    private static string MessageId(string inputId, int index) => index == 0 ? inputId + "-error" : $"{inputId}-error-{index + 1}";
}
