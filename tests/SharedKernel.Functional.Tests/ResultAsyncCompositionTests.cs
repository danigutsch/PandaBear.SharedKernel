namespace SharedKernel.Functional.Tests;

[Trait(TestTraits.CapabilityName, TestTraits.ResultCapability)]
[Trait(TestTraits.CategoryName, TestTraits.CompositionCategory)]
public sealed class ResultAsyncCompositionTests
{
    [Fact]
    public async Task Maps_A_Success_With_A_Task_Delegate()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var mapped = await result.Map(static value => Task.FromResult(value.Length));

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(5, mapped.Value);
    }

    [Fact]
    public async Task Preserves_A_Failure_When_Mapping_With_A_ValueTask_Delegate()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var mapped = await result.Map(static value => ValueTask.FromResult(value.Length));

        // Assert
        Assert.True(mapped.IsFailure);
        Assert.True(mapped.TryGetError(out var error));
        Assert.NotNull(error);
        Assert.Equal("Unexpected failure", error.Detail);
    }

    [Fact]
    public async Task Preserves_A_Failure_When_Mapping_With_A_Task_Delegate()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var mapped = await result.Map(static value => Task.FromResult(value.Length));

        // Assert
        Assert.True(mapped.IsFailure);
        Assert.True(mapped.TryGetError(out var error));
        Assert.NotNull(error);
        Assert.Equal("Unexpected failure", error.Detail);
    }

    [Fact]
    public async Task Binds_A_Success_With_A_Task_Delegate()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var bound = await result.Bind(static value => Task.FromResult(Result.Ok(value.Length)));

        // Assert
        Assert.True(bound.IsSuccess);
        Assert.Equal(5, bound.Value);
    }

    [Fact]
    public async Task Binds_A_Success_With_A_ValueTask_Delegate()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var bound = await result.Bind(static value => ValueTask.FromResult(Result.Ok(value.Length)));

        // Assert
        Assert.True(bound.IsSuccess);
        Assert.Equal(5, bound.Value);
    }

    [Fact]
    public async Task Matches_A_Generic_Result_With_Asynchronous_Delegates()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var matched = await result.Match(
            static value => Task.FromResult(value.Length),
            static error => Task.FromResult(error.Detail.Length));

        // Assert
        Assert.Equal("Unexpected failure".Length, matched);
    }

    [Fact]
    public async Task Matches_A_Generic_Success_Result_With_A_Task_Delegate()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var matched = await result.Match(
            static value => Task.FromResult(value.Length),
            static error => Task.FromResult(error.Detail.Length));

        // Assert
        Assert.Equal(5, matched);
    }

    [Fact]
    public async Task Matches_A_Generic_Success_Result_With_A_ValueTask_Delegate()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var matched = await result.Match(
            static value => ValueTask.FromResult(value.Length),
            static error => ValueTask.FromResult(error.Detail.Length));

        // Assert
        Assert.Equal(5, matched);
    }

    [Fact]
    public async Task Matches_A_Non_Generic_Result_With_Asynchronous_Delegates()
    {
        // Arrange
        var result = Result.Ok();

        // Act
        var matched = await result.Match(
            static () => ValueTask.FromResult("success"),
            static error => ValueTask.FromResult(error.Detail));

        // Assert
        Assert.Equal("success", matched);
    }

    [Fact]
    public async Task Matches_A_Non_Generic_Failure_Result_With_A_Task_Delegate()
    {
        // Arrange
        var result = Result.Error("Unexpected failure");

        // Act
        var matched = await result.Match(
            static () => Task.FromResult("success"),
            static error => Task.FromResult(error.Detail));

        // Assert
        Assert.Equal("Unexpected failure", matched);
    }

    [Fact]
    public async Task Matches_A_Non_Generic_Failure_Result_With_A_ValueTask_Delegate()
    {
        // Arrange
        var result = Result.Error("Unexpected failure");

        // Act
        var matched = await result.Match(
            static () => ValueTask.FromResult("success"),
            static error => ValueTask.FromResult(error.Detail));

        // Assert
        Assert.Equal("Unexpected failure", matched);
    }

    [Fact]
    public async Task Maps_An_Asynchronous_Task_Result()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok("porto"));

        // Act
        var mapped = await resultTask.Map(static value => value.Length);

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(5, mapped.Value);
    }

    [Fact]
    public async Task Binds_An_Asynchronous_ValueTask_Result()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var bound = await resultTask.Bind(static value => ValueTask.FromResult(Result.Ok(value.Length)));

        // Assert
        Assert.True(bound.IsSuccess);
        Assert.Equal(5, bound.Value);
    }

    [Fact]
    public async Task Matches_An_Asynchronous_Task_Result()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var matched = await resultTask.Match(
            static value => ValueTask.FromResult(value.Length),
            static error => ValueTask.FromResult(error.Detail.Length));

        // Assert
        Assert.Equal("Unexpected failure".Length, matched);
    }

    [Fact]
    public async Task Maps_An_Asynchronous_Task_Result_With_A_Task_Delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok("porto"));

        // Act
        var mapped = await resultTask.Map(static value => Task.FromResult(value.Length));

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(5, mapped.Value);
    }

    [Fact]
    public async Task Maps_A_Failed_Asynchronous_Task_Result_With_A_ValueTask_Delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var mapped = await resultTask.Map(static value => ValueTask.FromResult(value.Length));

        // Assert
        Assert.True(mapped.IsFailure);
        Assert.True(mapped.TryGetError(out var error));
        Assert.NotNull(error);
        Assert.Equal("Unexpected failure", error.Detail);
    }

    [Fact]
    public async Task Maps_An_Asynchronous_ValueTask_Result_With_A_ValueTask_Delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var mapped = await resultTask.Map(static value => ValueTask.FromResult(value.Length));

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(5, mapped.Value);
    }

    [Fact]
    public async Task Binds_An_Asynchronous_Task_Result_With_A_Task_Delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok("porto"));

        // Act
        var bound = await resultTask.Bind(static value => Task.FromResult(Result.Ok(value.Length)));

        // Assert
        Assert.True(bound.IsSuccess);
        Assert.Equal(5, bound.Value);
    }

    [Fact]
    public async Task Maps_An_Asynchronous_ValueTask_Result_With_A_Sync_Delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var mapped = await resultTask.Map(static value => value.Length);

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(5, mapped.Value);
    }

    [Fact]
    public async Task Maps_An_Asynchronous_ValueTask_Result_With_A_Task_Delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var mapped = await resultTask.Map(static value => Task.FromResult(value.Length));

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(5, mapped.Value);
    }

    [Fact]
    public async Task Binds_An_Asynchronous_Task_Result_With_A_ValueTask_Delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok("porto"));

        // Act
        var bound = await resultTask.Bind(static value => ValueTask.FromResult(Result.Ok(value.Length)));

        // Assert
        Assert.True(bound.IsSuccess);
        Assert.Equal(5, bound.Value);
    }

    [Fact]
    public async Task Binds_An_Asynchronous_ValueTask_Result_With_A_Sync_Delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var bound = await resultTask.Bind(static value => Result.Ok(value.Length));

        // Assert
        Assert.True(bound.IsSuccess);
        Assert.Equal(5, bound.Value);
    }

    [Fact]
    public async Task Binds_An_Asynchronous_ValueTask_Result_With_A_Task_Delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var bound = await resultTask.Bind(static value => Task.FromResult(Result.Ok(value.Length)));

        // Assert
        Assert.True(bound.IsSuccess);
        Assert.Equal(5, bound.Value);
    }

    [Fact]
    public async Task Matches_An_Asynchronous_Task_Result_With_A_Task_Delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var matched = await resultTask.Match(
            static value => Task.FromResult(value.Length),
            static error => Task.FromResult(error.Detail.Length));

        // Assert
        Assert.Equal("Unexpected failure".Length, matched);
    }

    [Fact]
    public async Task Matches_An_Asynchronous_ValueTask_Result_With_A_Sync_Delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var matched = await resultTask.Match(
            static value => value.Length,
            static error => error.Detail.Length);

        // Assert
        Assert.Equal("Unexpected failure".Length, matched);
    }

    [Fact]
    public async Task Matches_An_Asynchronous_ValueTask_Result_With_A_ValueTask_Delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var matched = await resultTask.Match(
            static value => ValueTask.FromResult(value.Length),
            static error => ValueTask.FromResult(error.Detail.Length));

        // Assert
        Assert.Equal("Unexpected failure".Length, matched);
    }

    [Fact]
    public async Task Matches_An_Asynchronous_ValueTask_Result_With_A_Task_Delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var matched = await resultTask.Match(
            static value => Task.FromResult(value.Length),
            static error => Task.FromResult(error.Detail.Length));

        // Assert
        Assert.Equal("Unexpected failure".Length, matched);
    }

    [Fact]
    public async Task Matches_An_Asynchronous_Task_Non_Generic_Result()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Error("Unexpected failure"));

        // Act
        var matched = await resultTask.Match(
            static () => "success",
            static error => error.Detail);

        // Assert
        Assert.Equal("Unexpected failure", matched);
    }

    [Fact]
    public async Task Matches_An_Asynchronous_ValueTask_Non_Generic_Result_With_A_Task_Delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok());

        // Act
        var matched = await resultTask.Match(
            static () => Task.FromResult("success"),
            static error => Task.FromResult(error.Detail));

        // Assert
        Assert.Equal("success", matched);
    }

    [Fact]
    public async Task Matches_An_Asynchronous_Task_Non_Generic_Result_With_A_Task_Delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok());

        // Act
        var matched = await resultTask.Match(
            static () => Task.FromResult("success"),
            static error => Task.FromResult(error.Detail));

        // Assert
        Assert.Equal("success", matched);
    }

    [Fact]
    public async Task Matches_An_Asynchronous_Task_Non_Generic_Result_With_A_ValueTask_Delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok());

        // Act
        var matched = await resultTask.Match(
            static () => ValueTask.FromResult("success"),
            static error => ValueTask.FromResult(error.Detail));

        // Assert
        Assert.Equal("success", matched);
    }

    [Fact]
    public async Task Matches_An_Asynchronous_ValueTask_Non_Generic_Result_With_A_Sync_Delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Error("Unexpected failure"));

        // Act
        var matched = await resultTask.Match(
            static () => "success",
            static error => error.Detail);

        // Assert
        Assert.Equal("Unexpected failure", matched);
    }

    [Fact]
    public async Task Matches_An_Asynchronous_ValueTask_Non_Generic_Result_With_A_ValueTask_Delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok());

        // Act
        var matched = await resultTask.Match(
            static () => ValueTask.FromResult("success"),
            static error => ValueTask.FromResult(error.Detail));

        // Assert
        Assert.Equal("success", matched);
    }

    [Fact]
    public async Task Rejects_A_Null_Task_Result_Source_For_Map()
    {
        // Arrange
        var exception = ReflectionTestHelpers.InvokeStaticGenericAndUnwrapArgumentNull(
            typeof(ResultTaskExtensions),
            nameof(ResultTaskExtensions.Map),
            [typeof(string), typeof(int)],
            parameters => ReflectionTestHelpers.HasTwoParameters(typeof(Task<Result<string>>), typeof(Func<string, int>), parameters),
            [null, static (string value) => value.Length]);

        // Assert
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_A_Null_Task_Result_Source_For_Bind()
    {
        // Arrange
        var exception = ReflectionTestHelpers.InvokeStaticGenericAndUnwrapArgumentNull(
            typeof(ResultTaskExtensions),
            nameof(ResultTaskExtensions.Bind),
            [typeof(string), typeof(int)],
            parameters => ReflectionTestHelpers.HasTwoParameters(typeof(Task<Result<string>>), typeof(Func<string, Result<int>>), parameters),
            [null, static (string value) => Result.Ok(value.Length)]);

        // Assert
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_A_Null_Task_Result_Source_For_Match()
    {
        // Arrange
        var exception = ReflectionTestHelpers.InvokeStaticGenericAndUnwrapArgumentNull(
            typeof(ResultTaskExtensions),
            nameof(ResultTaskExtensions.Match),
            [typeof(string), typeof(int)],
            parameters => parameters.Length == 3
                && parameters[0].ParameterType == typeof(Task<Result<string>>)
                && parameters[1].ParameterType == typeof(Func<string, int>)
                && parameters[2].ParameterType == typeof(Func<ResultError, int>),
            [null, static (string value) => value.Length, static (ResultError error) => error.Detail.Length]);

        // Assert
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_A_Null_Task_Map_Delegate()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = ReflectionTestHelpers.InvokeInstanceAndUnwrapArgumentNull(
            result,
            nameof(Result<string>.Map),
            [typeof(int)],
            parameters => ReflectionTestHelpers.HasSingleFuncParameterReturning(typeof(Task<>), parameters),
            [null]);

        // Assert
        Assert.Equal("map", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_A_Null_ValueTask_Map_Delegate()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = ReflectionTestHelpers.InvokeInstanceAndUnwrapArgumentNull(
            result,
            nameof(Result<string>.Map),
            [typeof(int)],
            parameters => ReflectionTestHelpers.HasSingleFuncParameterReturning(typeof(ValueTask<>), parameters),
            [null]);

        // Assert
        Assert.Equal("map", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_A_Null_Task_Bind_Delegate()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = ReflectionTestHelpers.InvokeInstanceAndUnwrapArgumentNull(
            result,
            nameof(Result<string>.Bind),
            [typeof(int)],
            parameters => ReflectionTestHelpers.HasSingleFuncParameterReturning(typeof(Task<>), parameters),
            [null]);

        // Assert
        Assert.Equal("bind", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_A_Null_ValueTask_Bind_Delegate()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = ReflectionTestHelpers.InvokeInstanceAndUnwrapArgumentNull(
            result,
            nameof(Result<string>.Bind),
            [typeof(int)],
            parameters => ReflectionTestHelpers.HasSingleFuncParameterReturning(typeof(ValueTask<>), parameters),
            [null]);

        // Assert
        Assert.Equal("bind", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_A_Null_Task_Match_Delegate_For_Generic_Result()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = ReflectionTestHelpers.InvokeInstanceAndUnwrapArgumentNull(
            result,
            nameof(Result<string>.Match),
            [typeof(int)],
            parameters => ReflectionTestHelpers.HasTwoParametersWithReturnKinds(typeof(Task<>), typeof(Task<>), parameters),
            [null, static (ResultError error) => Task.FromResult(error.Detail.Length)]);

        // Assert
        Assert.Equal("whenSuccess", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_A_Null_ValueTask_Match_Delegate_For_Generic_Result()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = ReflectionTestHelpers.InvokeInstanceAndUnwrapArgumentNull(
            result,
            nameof(Result<string>.Match),
            [typeof(int)],
            parameters => ReflectionTestHelpers.HasTwoParametersWithReturnKinds(typeof(ValueTask<>), typeof(ValueTask<>), parameters),
            [null, static (ResultError error) => ValueTask.FromResult(error.Detail.Length)]);

        // Assert
        Assert.Equal("whenSuccess", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_A_Null_Task_Match_Delegate_For_Non_Generic_Result()
    {
        // Arrange
        var result = Result.Ok();
        var exception = ReflectionTestHelpers.InvokeInstanceAndUnwrapArgumentNull(
            result,
            nameof(Result.Match),
            [typeof(int)],
            parameters => ReflectionTestHelpers.HasTwoParametersWithReturnKinds(typeof(Task<>), typeof(Task<>), parameters),
            [null, static (ResultError error) => Task.FromResult(error.Detail.Length)]);

        // Assert
        Assert.Equal("whenSuccess", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_A_Null_ValueTask_Match_Delegate_For_Non_Generic_Result()
    {
        // Arrange
        var result = Result.Ok();
        var exception = ReflectionTestHelpers.InvokeInstanceAndUnwrapArgumentNull(
            result,
            nameof(Result.Match),
            [typeof(int)],
            parameters => ReflectionTestHelpers.HasTwoParametersWithReturnKinds(typeof(ValueTask<>), typeof(ValueTask<>), parameters),
            [null, static (ResultError error) => ValueTask.FromResult(error.Detail.Length)]);

        // Assert
        Assert.Equal("whenSuccess", exception.ParamName);
    }

    [Fact]
    public async Task Throws_For_An_Uninitialized_Non_Generic_Result_With_Task_Match()
    {
        // Arrange
        var result = default(Result);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => result.Match(static () => Task.FromResult("success"), static error => Task.FromResult(error.Detail)));

        // Assert
        Assert.Equal("Result status is not initialized.", exception.Message);
    }

    [Fact]
    public async Task Throws_For_An_Uninitialized_Generic_Result_With_ValueTask_Match()
    {
        // Arrange
        var result = default(Result<string>);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await result.Match(static value => ValueTask.FromResult(value.Length), static error => ValueTask.FromResult(error.Detail.Length)));

        // Assert
        Assert.Equal("Result status is not initialized.", exception.Message);
    }
}
