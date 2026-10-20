namespace PandaBear.Mediator.SourceGenerator;

/// <summary>
/// Centralizes generated string literal escaping and mediator service type names.
/// </summary>
internal static class MediatorGenerationNames
{
    public static string EscapeStringLiteral(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    public static string GetSelfRegistrationServiceType(string implementationType)
    {
        return implementationType;
    }

    public static string GetHandlerServiceType(HandlerDescriptor handler)
    {
        return handler.Kind switch
        {
            HandlerKind.Request => $"global::PandaBear.Mediator.IRequestHandler<{handler.RequestMetadataName}, {handler.ResponseMetadataName}>",
            HandlerKind.Command => $"global::PandaBear.Mediator.ICommandHandler<{handler.RequestMetadataName}>",
            HandlerKind.CommandWithResponse => $"global::PandaBear.Mediator.ICommandHandler<{handler.RequestMetadataName}, {handler.ResponseMetadataName}>",
            HandlerKind.Query => $"global::PandaBear.Mediator.IQueryHandler<{handler.RequestMetadataName}, {handler.ResponseMetadataName}>",
            _ => throw new ArgumentOutOfRangeException(nameof(handler))
        };
    }

    public static string GetPipelineServiceType(string requestMetadataName, string responseMetadataName)
    {
        return GetPipelineServiceType(isStream: false, requestMetadataName, responseMetadataName);
    }

    public static string GetPipelineServiceType(bool isStream, string requestMetadataName, string responseMetadataName)
    {
        return isStream
            ? GetStreamPipelineServiceType(requestMetadataName, responseMetadataName)
            : $"global::PandaBear.Mediator.IPipelineBehavior<{requestMetadataName}, {responseMetadataName}>";
    }

    public static string GetNotificationHandlerServiceType(string notificationMetadataName)
    {
        return $"global::PandaBear.Mediator.INotificationHandler<{notificationMetadataName}>";
    }

    public static string GetStreamHandlerServiceType(string requestMetadataName, string responseMetadataName)
    {
        return $"global::PandaBear.Mediator.IStreamRequestHandler<{requestMetadataName}, {responseMetadataName}>";
    }

    public static string GetStreamPipelineServiceType(string requestMetadataName, string responseMetadataName)
    {
        return $"global::PandaBear.Mediator.IStreamPipelineBehavior<{requestMetadataName}, {responseMetadataName}>";
    }
}
