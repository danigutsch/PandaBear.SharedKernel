namespace SharedKernel.Mediator.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ContractsCapability)]
public sealed class MediatorContractsTests
{
    [Fact]
    public void ICommand_is_assignable_to_irequest_unit()
    {
        // Arrange
        var commandType = typeof(TestCommand);

        // Act
        var isAssignable = typeof(IRequest<Unit>).IsAssignableFrom(commandType);

        // Assert
        Assert.True(isAssignable);
    }

    [Fact]
    public void IQuery_of_t_is_assignable_to_irequest_of_t()
    {
        // Arrange
        var queryType = typeof(TestQuery);

        // Act
        var isAssignable = typeof(IRequest<string>).IsAssignableFrom(queryType);

        // Assert
        Assert.True(isAssignable);
    }

    [Fact]
    public void ICommand_of_t_is_assignable_to_irequest_of_t()
    {
        // Arrange
        var commandType = typeof(TestCommandWithResponse);

        // Act
        var isAssignable = typeof(IRequest<int>).IsAssignableFrom(commandType);

        // Assert
        Assert.True(isAssignable);
    }

    [Fact]
    public void Handler_variance_works_where_expected()
    {
        // Arrange
        var handlerContractType = typeof(IQueryHandler<DerivedQuery, string>);

        // Act
        var isAssignable = handlerContractType.IsAssignableFrom(typeof(BaseQueryHandler));

        // Assert
        Assert.True(isAssignable);
    }

    [Fact]
    public void Notification_contracts_allow_class_and_struct()
    {
        // Arrange
        var classNotificationType = typeof(INotificationHandler<ClassNotification>);
        var structNotificationType = typeof(INotificationHandler<StructNotification>);

        // Act
        var classConstraintHolds = classNotificationType.IsAssignableFrom(typeof(ClassNotificationHandler));
        var structConstraintHolds = structNotificationType.IsAssignableFrom(typeof(StructNotificationHandler));

        // Assert
        Assert.True(classConstraintHolds);
        Assert.True(structConstraintHolds);
    }

    [Fact]
    public void Notification_order_attribute_exposes_configured_order()
    {
        // Arrange
        var attribute = typeof(OrderedClassNotificationHandler)
            .GetCustomAttributes(typeof(NotificationOrderAttribute), inherit: false)
            .Cast<NotificationOrderAttribute>()
            .Single();

        // Act
        var order = attribute.Order;

        // Assert
        Assert.Equal(5, order);
    }

    [Fact]
    public void Notification_dispatch_attribute_exposes_configured_strategy()
    {
        // Arrange
        var attribute = typeof(ParallelClassNotification)
            .GetCustomAttributes(typeof(NotificationDispatchAttribute), inherit: false)
            .Cast<NotificationDispatchAttribute>()
            .Single();

        // Act
        var strategy = attribute.Strategy;

        // Assert
        Assert.Equal(NotificationDispatchStrategy.Parallel, strategy);
    }

    [Fact]
    public void Stream_and_pipeline_contracts_compile()
    {
        // Arrange
        var streamHandlerType = typeof(IStreamRequestHandler<TestStreamRequest, string>);
        var streamPipelineType = typeof(IStreamPipelineBehavior<TestStreamRequest, string>);
        var streamCommandHandlerType = typeof(IRequestHandler<TestStreamCommand, string>);
        var duplexStreamHandlerType = typeof(IStreamRequestHandler<TestDuplexStreamCommand, string>);
        var pipelineType = typeof(IPipelineBehavior<TestQuery, string>);

        // Act
        var streamConstraintHolds = streamHandlerType.IsAssignableFrom(typeof(TestStreamHandler));
        var streamPipelineConstraintHolds = streamPipelineType.IsAssignableFrom(typeof(TestStreamPipelineBehavior));
        var streamCommandConstraintHolds = streamCommandHandlerType.IsAssignableFrom(typeof(TestStreamCommandHandler));
        var duplexStreamConstraintHolds = duplexStreamHandlerType.IsAssignableFrom(typeof(TestDuplexStreamCommandHandler));
        var pipelineConstraintHolds = pipelineType.IsAssignableFrom(typeof(TestPipelineBehavior));

        // Assert
        Assert.True(streamConstraintHolds);
        Assert.True(streamPipelineConstraintHolds);
        Assert.True(streamCommandConstraintHolds);
        Assert.True(duplexStreamConstraintHolds);
        Assert.True(pipelineConstraintHolds);
    }

    [Fact]
    public void Semantic_stream_contracts_distinguish_unary_stream_input_and_duplex_stream_shapes()
    {
        // Arrange
        var streamCommandType = typeof(TestStreamCommand);
        var streamQueryType = typeof(TestStreamQuery);
        var streamInputQueryType = typeof(TestStreamInputQuery);
        var duplexStreamCommandType = typeof(TestDuplexStreamCommand);
        var duplexStreamQueryType = typeof(TestDuplexStreamQuery);

        // Act
        var isStreamCommand = typeof(IStreamCommand<int, string>).IsAssignableFrom(streamCommandType);
        var isStreamQuery = typeof(IStreamQuery<string>).IsAssignableFrom(streamQueryType);
        var isStreamInputQuery = typeof(IStreamQuery<int, string>).IsAssignableFrom(streamInputQueryType);
        var isDuplexStreamCommand = typeof(IDuplexStreamCommand<int, string>).IsAssignableFrom(duplexStreamCommandType);
        var isDuplexStreamQuery = typeof(IDuplexStreamQuery<int, string>).IsAssignableFrom(duplexStreamQueryType);
        var isUnaryRequest = typeof(IRequest<string>).IsAssignableFrom(streamCommandType);
        var isResponseOnlyStream = typeof(IStreamRequest<string>).IsAssignableFrom(streamCommandType);
        var isResponseStreamCommand = typeof(IStreamRequest<string>).IsAssignableFrom(duplexStreamCommandType);
        var isResponseStreamQuery = typeof(IStreamRequest<string>).IsAssignableFrom(streamQueryType);

        // Assert
        Assert.True(isStreamCommand);
        Assert.True(isStreamQuery);
        Assert.True(isStreamInputQuery);
        Assert.True(isDuplexStreamCommand);
        Assert.True(isDuplexStreamQuery);
        Assert.True(isUnaryRequest);
        Assert.False(isResponseOnlyStream);
        Assert.True(isResponseStreamCommand);
        Assert.True(isResponseStreamQuery);
    }

    [Fact]
    public void ISender_exposes_stream_send_generic_method()
    {
        // Arrange
        var method = typeof(ISender)
            .GetMethods()
            .Single(
                candidate => candidate.Name == nameof(ISender.Send)
                    && candidate.GetParameters()[0].ParameterType.IsGenericType
                    && candidate.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IStreamRequest<>));

        // Act
        var returnType = method?.ReturnType;
        var genericReturnType = returnType?.GetGenericTypeDefinition();

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(IAsyncEnumerable<>), genericReturnType);
    }
}
