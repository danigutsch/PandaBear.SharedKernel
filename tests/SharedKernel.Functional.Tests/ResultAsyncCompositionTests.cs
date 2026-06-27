namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
public sealed class ResultAsyncCompositionTests
{
    [Fact]
    public async Task Maps_a_success_with_a_task_delegate()
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
    public async Task Preserves_a_failure_when_mapping_with_a_valueTask_delegate()
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
    public async Task Preserves_a_failure_when_mapping_with_a_task_delegate()
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
    public async Task Binds_a_success_with_a_task_delegate()
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
    public async Task Binds_a_success_with_a_valueTask_delegate()
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
    public async Task Matches_a_generic_result_with_asynchronous_delegates()
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
    public async Task Matches_a_generic_success_result_with_a_task_delegate()
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
    public async Task Matches_a_generic_success_result_with_a_valueTask_delegate()
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
    public async Task Matches_a_non_generic_result_with_asynchronous_delegates()
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
    public async Task Matches_a_non_generic_failure_result_with_a_task_delegate()
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
    public async Task Matches_a_non_generic_failure_result_with_a_valueTask_delegate()
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
    public async Task Maps_an_asynchronous_task_result()
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
    public async Task Binds_an_asynchronous_valueTask_result()
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
    public async Task Matches_an_asynchronous_task_result()
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
    public async Task Maps_an_asynchronous_task_result_with_a_task_delegate()
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
    public async Task Maps_a_failed_asynchronous_task_result_with_a_valueTask_delegate()
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
    public async Task Maps_an_asynchronous_valueTask_result_with_a_valueTask_delegate()
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
    public async Task Binds_an_asynchronous_task_result_with_a_task_delegate()
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
    public async Task Maps_an_asynchronous_valueTask_result_with_a_sync_delegate()
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
    public async Task Maps_an_asynchronous_valueTask_result_with_a_task_delegate()
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
    public async Task Binds_an_asynchronous_task_result_with_a_valueTask_delegate()
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
    public async Task Binds_an_asynchronous_valueTask_result_with_a_sync_delegate()
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
    public async Task Binds_an_asynchronous_valueTask_result_with_a_task_delegate()
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
    public async Task Ensures_a_success_with_a_task_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var ensured = await result.Ensure(static value => Task.FromResult(value.Length == 5), new ResultError("Length mismatch"));

        // Assert
        Assert.True(ensured.IsSuccess);
        Assert.Equal("porto", ensured.Value);
    }

    [Fact]
    public async Task Returns_the_provided_error_when_ensuring_with_a_valueTask_delegate_fails()
    {
        // Arrange
        var failure = new ResultError("Length mismatch", ResultErrorCodes.Error);
        var result = Result.Ok("porto");

        // Act
        var ensured = await result.Ensure(static value => ValueTask.FromResult(value.Length == 4), failure);

        // Assert
        Assert.True(ensured.IsFailure);
        Assert.True(ensured.TryGetError(out var error));
        Assert.NotNull(error);
        Assert.Equal(failure, error);
    }

    [Fact]
    public async Task Preserves_invalid_status_when_ensuring_asynchronously_with_a_validation_error()
    {
        // Arrange
        var failure = new ResultError(
            "Validation failed",
            ResultErrorCodes.Invalid,
            new Dictionary<string, string[]>
            {
                ["Name"] = ["Name is required"],
            });
        var result = Result.Ok("porto");

        // Act
        var ensured = await result.Ensure(static value => Task.FromResult(value.Length == 4), failure);

        // Assert
        Assert.True(ensured.IsFailure);
        Assert.Equal(ResultStatus.Invalid, ensured.Status);
        Assert.True(ensured.TryGetError(out var error));
        Assert.NotNull(error);
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(["Name is required"], error.ValidationErrors["Name"]);
    }

    [Fact]
    public async Task Ensures_an_asynchronous_task_result_with_a_sync_delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok("porto"));

        // Act
        var ensured = await resultTask.Ensure(static value => value.Length == 5, new ResultError("Length mismatch"));

        // Assert
        Assert.True(ensured.IsSuccess);
        Assert.Equal("porto", ensured.Value);
    }

    [Fact]
    public async Task Short_circuits_a_failed_asynchronous_task_result_when_ensuring()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var ensured = await resultTask.Ensure(static _ => true, new ResultError("Should not be used"));

        // Assert
        Assert.True(ensured.IsFailure);
        Assert.True(ensured.TryGetError(out var error));
        Assert.NotNull(error);
        Assert.Equal("Unexpected failure", error.Detail);
    }

    [Fact]
    public async Task Ensures_an_asynchronous_valueTask_result_with_a_sync_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var ensured = await resultTask.Ensure(static value => value.Length == 5, new ResultError("Length mismatch"));

        // Assert
        Assert.True(ensured.IsSuccess);
        Assert.Equal("porto", ensured.Value);
    }

    [Fact]
    public async Task Short_circuits_a_failed_asynchronous_valueTask_result_when_ensuring()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var ensured = await resultTask.Ensure(static _ => true, new ResultError("Should not be used"));

        // Assert
        Assert.True(ensured.IsFailure);
        Assert.True(ensured.TryGetError(out var error));
        Assert.NotNull(error);
        Assert.Equal("Unexpected failure", error.Detail);
    }

    [Fact]
    public async Task Ensures_an_asynchronous_task_result_with_a_task_delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok("porto"));

        // Act
        var ensured = await resultTask.Ensure(static value => Task.FromResult(value.Length == 5), new ResultError("Length mismatch"));

        // Assert
        Assert.True(ensured.IsSuccess);
        Assert.Equal("porto", ensured.Value);
    }

    [Fact]
    public async Task Ensures_an_asynchronous_valueTask_result_with_a_valueTask_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var ensured = await resultTask.Ensure(static value => ValueTask.FromResult(value.Length == 5), new ResultError("Length mismatch"));

        // Assert
        Assert.True(ensured.IsSuccess);
        Assert.Equal("porto", ensured.Value);
    }

    [Fact]
    public async Task Matches_an_asynchronous_task_result_with_a_task_delegate()
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
    public async Task Matches_an_asynchronous_valueTask_result_with_a_sync_delegate()
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
    public async Task Matches_an_asynchronous_valueTask_result_with_a_valueTask_delegate()
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
    public async Task Matches_an_asynchronous_valueTask_result_with_a_task_delegate()
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
    public async Task Matches_an_asynchronous_task_non_generic_result()
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
    public async Task Matches_an_asynchronous_valueTask_non_generic_result_with_a_task_delegate()
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
    public async Task Matches_an_asynchronous_task_non_generic_result_with_a_task_delegate()
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
    public async Task Matches_an_asynchronous_task_non_generic_result_with_a_valueTask_delegate()
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
    public async Task Matches_an_asynchronous_valueTask_non_generic_result_with_a_sync_delegate()
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
    public async Task Matches_an_asynchronous_valueTask_non_generic_result_with_a_valueTask_delegate()
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
    public async Task Rejects_a_null_task_result_source_for_map()
    {
        // Arrange
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => ResultTaskExtensions.Map(NullArgumentData.Task<Result<string>>(), static value => value.Length));

        // Assert
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_task_result_source_for_bind()
    {
        // Arrange
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => ResultTaskExtensions.Bind(NullArgumentData.Task<Result<string>>(), static value => Result.Ok(value.Length)));

        // Assert
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_task_result_source_for_ensure()
    {
        // Arrange
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => ResultTaskExtensions.Ensure(NullArgumentData.Task<Result<string>>(), static value => value.Length == 5, new ResultError("Length mismatch")));

        // Assert
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_task_result_source_for_match()
    {
        // Arrange
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => ResultTaskExtensions.Match(NullArgumentData.Task<Result<string>>(), static value => value.Length, static error => error.Detail.Length));

        // Assert
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_task_map_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => result.Map(NullArgumentData.TaskFunc<string, int>()));

        // Assert
        Assert.Equal("map", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_valueTask_map_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () => await result.Map(NullArgumentData.ValueTaskFunc<string, int>()));

        // Assert
        Assert.Equal("map", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_task_bind_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => result.Bind(NullArgumentData.TaskFunc<string, Result<int>>()));

        // Assert
        Assert.Equal("bind", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_valueTask_bind_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () => await result.Bind(NullArgumentData.ValueTaskFunc<string, Result<int>>()));

        // Assert
        Assert.Equal("bind", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_task_match_delegate_for_generic_result()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => result.Match(NullArgumentData.TaskFunc<string, int>(), static error => Task.FromResult(error.Detail.Length)));

        // Assert
        Assert.Equal("whenSuccess", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_valueTask_match_delegate_for_generic_result()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () => await result.Match(NullArgumentData.ValueTaskFunc<string, int>(), static error => ValueTask.FromResult(error.Detail.Length)));

        // Assert
        Assert.Equal("whenSuccess", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_task_match_delegate_for_non_generic_result()
    {
        // Arrange
        var result = Result.Ok();
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => result.Match(NullArgumentData.TaskFactory<int>(), static error => Task.FromResult(error.Detail.Length)));

        // Assert
        Assert.Equal("whenSuccess", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_valueTask_match_delegate_for_non_generic_result()
    {
        // Arrange
        var result = Result.Ok();
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () => await result.Match(NullArgumentData.ValueTaskFactory<int>(), static error => ValueTask.FromResult(error.Detail.Length)));

        // Assert
        Assert.Equal("whenSuccess", exception.ParamName);
    }

    [Fact]
    public async Task Throws_for_an_uninitialized_non_generic_result_with_task_match()
    {
        // Arrange
        var result = default(Result);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => result.Match(static () => Task.FromResult("success"), static error => Task.FromResult(error.Detail)));

        // Assert
        Assert.Equal("Result status is not initialized.", exception.Message);
    }

    [Fact]
    public async Task Throws_for_an_uninitialized_generic_result_with_valueTask_match()
    {
        // Arrange
        var result = default(Result<string>);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await result.Match(static value => ValueTask.FromResult(value.Length), static error => ValueTask.FromResult(error.Detail.Length)));

        // Assert
        Assert.Equal("Result status is not initialized.", exception.Message);
    }
}
