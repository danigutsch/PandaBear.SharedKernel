namespace PandaBear.Testing.Analyzers;

internal static class TestMethodNameSegments
{
    private static readonly string[] Allowed = [
        "API",
        "AsyncAPI",
        "CSV",
        "DTO",
        "DataAnnotationsValidator",
        "DateOnly",
        "DateTime",
        "EF",
        "EditContext",
        "GUID",
        "Guid",
        "HTTP",
        "HTTP2",
        "HttpClient",
        "HttpContext",
        "HttpRequest",
        "HttpResponse",
        "HttpStatusCode",
        "ID",
        "IDs",
        "JSON",
        "Json",
        "NavLink",
        "NavLinks",
        "NavMenu",
        "OpenApi",
        "ProblemDetails",
        "QuickGrid",
        "SKTEST",
        "Task",
        "TimeOnly",
        "TimeSpan",
        "URI",
        "URL",
        "UI",
        "UTC",
        "ValidationMessage",
        "ValueTask",
        "xUnit",
    ];

    public static string? Canonical(string segment)
    {
        if (Array.Exists(Allowed, allowed => string.Equals(segment, allowed, StringComparison.Ordinal)))
        {
            return segment;
        }

        if (string.Equals(segment, "API", StringComparison.OrdinalIgnoreCase))
        {
            return "API";
        }

        if (string.Equals(segment, "AsyncAPI", StringComparison.OrdinalIgnoreCase))
        {
            return "AsyncAPI";
        }

        if (segment.StartsWith("SKTEST", StringComparison.OrdinalIgnoreCase)
            && segment.Length > "SKTEST".Length
            && segment.Substring("SKTEST".Length).All(char.IsDigit))
        {
            return "SKTEST" + segment.Substring("SKTEST".Length);
        }

        return null;
    }
}
