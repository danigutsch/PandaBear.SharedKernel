namespace SharedKernel.Mediator.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ContractsCapability)]
public sealed class MediatorContractsTests
{
    [Fact]
    public void ICommand_Is_Assignable_To_IRequest_Unit()
    {
        // Arrange
        var commandType = typeof(TestCommand);

        // Act
        var isAssignable = typeof(IRequest<Unit>).IsAssignableFrom(commandType);

        // Assert
        Assert.True(isAssignable);
    }

    [Fact]
    public void IQuery_Of_T_Is_Assignable_To_IRequest_Of_T()
    {
        // Arrange
        var queryType = typeof(TestQuery);

        // Act
        var isAssignable = typeof(IRequest<string>).IsAssignableFrom(queryType);

        // Assert
        Assert.True(isAssignable);
    }

    [Fact]
    public void ICommand_Of_T_Is_Assignable_To_IRequest_Of_T()
    {
        // Arrange
        var commandType = typeof(TestCommandWithResponse);

        // Act
        var isAssignable = typeof(IRequest<int>).IsAssignableFrom(commandType);

        // Assert
        Assert.True(isAssignable);
    }

    [Fact]
    public void Handler_Variance_Works_Where_Expected()
    {
        // Arrange
        var handlerContractType = typeof(IQueryHandler<DerivedQuery, string>);

        // Act
        var isAssignable = handlerContractType.IsAssignableFrom(typeof(BaseQueryHandler));

        // Assert
        Assert.True(isAssignable);
    }

    [Fact]
    public void Notification_Contracts_Allow_Class_And_Struct()
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
    public void Notification_Order_Attribute_Exposes_Configured_Order()
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
    public void Notification_Dispatch_Attribute_Exposes_Configured_Strategy()
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
    public void Stream_And_Pipeline_Contracts_Compile()
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
    public void Semantic_Stream_Contracts_Distinguish_Unary_Stream_Input_And_Duplex_Stream_Shapes()
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
    public void ISender_Exposes_Stream_Send_Generic_Method()
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
