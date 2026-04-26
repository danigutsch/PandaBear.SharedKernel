namespace SharedKernel.Mediator.Tests;

[Trait(TestTraits.CapabilityName, TestTraits.ContractsCapability)]
public sealed class MediatorContractsTests
{
    [Fact]
    public void ICommand_Is_Assignable_To_IRequest_Unit_Expected_Behavior()
    {
        // Arrange
        var commandType = typeof(TestCommand);

        // Act
        var isAssignable = typeof(IRequest<Unit>).IsAssignableFrom(commandType);

        // Assert
        Assert.True(isAssignable);
    }

    [Fact]
    public void IQuery_Of_T_Is_Assignable_To_IRequest_Of_T_Expected_Behavior()
    {
        // Arrange
        var queryType = typeof(TestQuery);

        // Act
        var isAssignable = typeof(IRequest<string>).IsAssignableFrom(queryType);

        // Assert
        Assert.True(isAssignable);
    }

    [Fact]
    public void ICommand_Of_T_Is_Assignable_To_IRequest_Of_T_Expected_Behavior()
    {
        // Arrange
        var commandType = typeof(TestCommandWithResponse);

        // Act
        var isAssignable = typeof(IRequest<int>).IsAssignableFrom(commandType);

        // Assert
        Assert.True(isAssignable);
    }

    [Fact]
    public void Notification_Contracts_Allow_Class_And_Struct_Expected_Behavior()
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
    public void Stream_And_Pipeline_Contracts_Compile_Expected_Behavior()
    {
        // Arrange
        var streamHandlerType = typeof(IStreamRequestHandler<TestStreamRequest, string>);
        var pipelineType = typeof(IPipelineBehavior<TestQuery, string>);

        // Act
        var streamConstraintHolds = streamHandlerType.IsAssignableFrom(typeof(TestStreamHandler));
        var pipelineConstraintHolds = pipelineType.IsAssignableFrom(typeof(TestPipelineBehavior));

        // Assert
        Assert.True(streamConstraintHolds);
        Assert.True(pipelineConstraintHolds);
    }
}
