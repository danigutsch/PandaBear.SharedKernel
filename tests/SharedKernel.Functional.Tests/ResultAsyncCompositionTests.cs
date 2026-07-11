namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
public sealed class ResultAsyncCompositionTests
{
    [Fact]
    public async Task Maps_a_success_with_a_Task_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var mapped = await result.Map(static value => Task.FromResult(value.Length));

        // Assert
        (mapped.IsSuccess).ShouldBeTrue();
        (mapped.Value).ShouldBe(5);
    }

    [Fact]
    public async Task Preserves_a_failure_when_mapping_with_a_ValueTask_delegate()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var mapped = await result.Map(static value => ValueTask.FromResult(value.Length));

        // Assert
        (mapped.IsFailure).ShouldBeTrue();
        (mapped.TryGetError(out var error)).ShouldBeTrue();
        _ = (error).ShouldNotBeNull();
        (error.Detail).ShouldBe("Unexpected failure");
    }

    [Fact]
    public async Task Preserves_a_failure_when_mapping_with_a_Task_delegate()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var mapped = await result.Map(static value => Task.FromResult(value.Length));

        // Assert
        (mapped.IsFailure).ShouldBeTrue();
        (mapped.TryGetError(out var error)).ShouldBeTrue();
        _ = (error).ShouldNotBeNull();
        (error.Detail).ShouldBe("Unexpected failure");
    }

    [Fact]
    public async Task Binds_a_success_with_a_Task_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var bound = await result.Bind(static value => Task.FromResult(Result.Ok(value.Length)));

        // Assert
        (bound.IsSuccess).ShouldBeTrue();
        (bound.Value).ShouldBe(5);
    }

    [Fact]
    public async Task Binds_a_success_with_a_ValueTask_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var bound = await result.Bind(static value => ValueTask.FromResult(Result.Ok(value.Length)));

        // Assert
        (bound.IsSuccess).ShouldBeTrue();
        (bound.Value).ShouldBe(5);
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
        (matched).ShouldBe("Unexpected failure".Length);
    }

    [Fact]
    public async Task Matches_a_generic_success_result_with_a_Task_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var matched = await result.Match(
            static value => Task.FromResult(value.Length),
            static error => Task.FromResult(error.Detail.Length));

        // Assert
        (matched).ShouldBe(5);
    }

    [Fact]
    public async Task Matches_a_generic_success_result_with_a_ValueTask_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var matched = await result.Match(
            static value => ValueTask.FromResult(value.Length),
            static error => ValueTask.FromResult(error.Detail.Length));

        // Assert
        (matched).ShouldBe(5);
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
        (matched).ShouldBe("success");
    }

    [Fact]
    public async Task Matches_a_non_generic_failure_result_with_a_Task_delegate()
    {
        // Arrange
        var result = Result.Error("Unexpected failure");

        // Act
        var matched = await result.Match(
            static () => Task.FromResult("success"),
            static error => Task.FromResult(error.Detail));

        // Assert
        (matched).ShouldBe("Unexpected failure");
    }

    [Fact]
    public async Task Matches_a_non_generic_failure_result_with_a_ValueTask_delegate()
    {
        // Arrange
        var result = Result.Error("Unexpected failure");

        // Act
        var matched = await result.Match(
            static () => ValueTask.FromResult("success"),
            static error => ValueTask.FromResult(error.Detail));

        // Assert
        (matched).ShouldBe("Unexpected failure");
    }

    [Fact]
    public async Task Maps_an_asynchronous_Task_result()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok("porto"));

        // Act
        var mapped = await resultTask.Map(static value => value.Length);

        // Assert
        (mapped.IsSuccess).ShouldBeTrue();
        (mapped.Value).ShouldBe(5);
    }

    [Fact]
    public async Task Binds_an_asynchronous_ValueTask_result()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var bound = await resultTask.Bind(static value => ValueTask.FromResult(Result.Ok(value.Length)));

        // Assert
        (bound.IsSuccess).ShouldBeTrue();
        (bound.Value).ShouldBe(5);
    }

    [Fact]
    public async Task Matches_an_asynchronous_Task_result()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var matched = await resultTask.Match(
            static value => ValueTask.FromResult(value.Length),
            static error => ValueTask.FromResult(error.Detail.Length));

        // Assert
        (matched).ShouldBe("Unexpected failure".Length);
    }

    [Fact]
    public async Task Maps_an_asynchronous_Task_result_with_a_Task_delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok("porto"));

        // Act
        var mapped = await resultTask.Map(static value => Task.FromResult(value.Length));

        // Assert
        (mapped.IsSuccess).ShouldBeTrue();
        (mapped.Value).ShouldBe(5);
    }

    [Fact]
    public async Task Maps_a_failed_asynchronous_Task_result_with_a_ValueTask_delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var mapped = await resultTask.Map(static value => ValueTask.FromResult(value.Length));

        // Assert
        (mapped.IsFailure).ShouldBeTrue();
        (mapped.TryGetError(out var error)).ShouldBeTrue();
        _ = (error).ShouldNotBeNull();
        (error.Detail).ShouldBe("Unexpected failure");
    }

    [Fact]
    public async Task Maps_an_asynchronous_ValueTask_result_with_a_ValueTask_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var mapped = await resultTask.Map(static value => ValueTask.FromResult(value.Length));

        // Assert
        (mapped.IsSuccess).ShouldBeTrue();
        (mapped.Value).ShouldBe(5);
    }

    [Fact]
    public async Task Binds_an_asynchronous_Task_result_with_a_Task_delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok("porto"));

        // Act
        var bound = await resultTask.Bind(static value => Task.FromResult(Result.Ok(value.Length)));

        // Assert
        (bound.IsSuccess).ShouldBeTrue();
        (bound.Value).ShouldBe(5);
    }

    [Fact]
    public async Task Maps_an_asynchronous_ValueTask_result_with_a_sync_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var mapped = await resultTask.Map(static value => value.Length);

        // Assert
        (mapped.IsSuccess).ShouldBeTrue();
        (mapped.Value).ShouldBe(5);
    }

    [Fact]
    public async Task Maps_an_asynchronous_ValueTask_result_with_a_Task_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var mapped = await resultTask.Map(static value => Task.FromResult(value.Length));

        // Assert
        (mapped.IsSuccess).ShouldBeTrue();
        (mapped.Value).ShouldBe(5);
    }

    [Fact]
    public async Task Binds_an_asynchronous_Task_result_with_a_ValueTask_delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok("porto"));

        // Act
        var bound = await resultTask.Bind(static value => ValueTask.FromResult(Result.Ok(value.Length)));

        // Assert
        (bound.IsSuccess).ShouldBeTrue();
        (bound.Value).ShouldBe(5);
    }

    [Fact]
    public async Task Binds_an_asynchronous_ValueTask_result_with_a_sync_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var bound = await resultTask.Bind(static value => Result.Ok(value.Length));

        // Assert
        (bound.IsSuccess).ShouldBeTrue();
        (bound.Value).ShouldBe(5);
    }

    [Fact]
    public async Task Binds_an_asynchronous_ValueTask_result_with_a_Task_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var bound = await resultTask.Bind(static value => Task.FromResult(Result.Ok(value.Length)));

        // Assert
        (bound.IsSuccess).ShouldBeTrue();
        (bound.Value).ShouldBe(5);
    }

    [Fact]
    public async Task Ensures_a_success_with_a_Task_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var ensured = await result.Ensure(static value => Task.FromResult(value.Length == 5), new ResultError("Length mismatch"));

        // Assert
        (ensured.IsSuccess).ShouldBeTrue();
        (ensured.Value).ShouldBe("porto");
    }

    [Fact]
    public async Task Returns_the_provided_error_when_ensuring_with_a_ValueTask_delegate_fails()
    {
        // Arrange
        var failure = new ResultError("Length mismatch", ResultErrorCodes.Error);
        var result = Result.Ok("porto");

        // Act
        var ensured = await result.Ensure(static value => ValueTask.FromResult(value.Length == 4), failure);

        // Assert
        (ensured.IsFailure).ShouldBeTrue();
        (ensured.TryGetError(out var error)).ShouldBeTrue();
        _ = (error).ShouldNotBeNull();
        (error).ShouldBe(failure);
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
        (ensured.IsFailure).ShouldBeTrue();
        (ensured.Status).ShouldBe(ResultStatus.Invalid);
        (ensured.TryGetError(out var error)).ShouldBeTrue();
        _ = (error).ShouldNotBeNull();
        (error.ValidationErrors).ShouldNotBeNull();
        (error.ValidationErrors["Name"]).ShouldBe(["Name is required"]);
    }

    [Fact]
    public async Task Ensures_an_asynchronous_Task_result_with_a_sync_delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok("porto"));

        // Act
        var ensured = await resultTask.Ensure(static value => value.Length == 5, new ResultError("Length mismatch"));

        // Assert
        (ensured.IsSuccess).ShouldBeTrue();
        (ensured.Value).ShouldBe("porto");
    }

    [Fact]
    public async Task Short_circuits_a_failed_asynchronous_Task_result_when_ensuring()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var ensured = await resultTask.Ensure(static _ => true, new ResultError("Should not be used"));

        // Assert
        (ensured.IsFailure).ShouldBeTrue();
        (ensured.TryGetError(out var error)).ShouldBeTrue();
        _ = (error).ShouldNotBeNull();
        (error.Detail).ShouldBe("Unexpected failure");
    }

    [Fact]
    public async Task Ensures_an_asynchronous_ValueTask_result_with_a_sync_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var ensured = await resultTask.Ensure(static value => value.Length == 5, new ResultError("Length mismatch"));

        // Assert
        (ensured.IsSuccess).ShouldBeTrue();
        (ensured.Value).ShouldBe("porto");
    }

    [Fact]
    public async Task Short_circuits_a_failed_asynchronous_ValueTask_result_when_ensuring()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var ensured = await resultTask.Ensure(static _ => true, new ResultError("Should not be used"));

        // Assert
        (ensured.IsFailure).ShouldBeTrue();
        (ensured.TryGetError(out var error)).ShouldBeTrue();
        _ = (error).ShouldNotBeNull();
        (error.Detail).ShouldBe("Unexpected failure");
    }

    [Fact]
    public async Task Ensures_an_asynchronous_Task_result_with_a_Task_delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok("porto"));

        // Act
        var ensured = await resultTask.Ensure(static value => Task.FromResult(value.Length == 5), new ResultError("Length mismatch"));

        // Assert
        (ensured.IsSuccess).ShouldBeTrue();
        (ensured.Value).ShouldBe("porto");
    }

    [Fact]
    public async Task Ensures_an_asynchronous_ValueTask_result_with_a_ValueTask_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok("porto"));

        // Act
        var ensured = await resultTask.Ensure(static value => ValueTask.FromResult(value.Length == 5), new ResultError("Length mismatch"));

        // Assert
        (ensured.IsSuccess).ShouldBeTrue();
        (ensured.Value).ShouldBe("porto");
    }

    [Fact]
    public async Task Matches_an_asynchronous_Task_result_with_a_Task_delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var matched = await resultTask.Match(
            static value => Task.FromResult(value.Length),
            static error => Task.FromResult(error.Detail.Length));

        // Assert
        (matched).ShouldBe("Unexpected failure".Length);
    }

    [Fact]
    public async Task Matches_an_asynchronous_ValueTask_result_with_a_sync_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var matched = await resultTask.Match(
            static value => value.Length,
            static error => error.Detail.Length);

        // Assert
        (matched).ShouldBe("Unexpected failure".Length);
    }

    [Fact]
    public async Task Matches_an_asynchronous_ValueTask_result_with_a_ValueTask_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var matched = await resultTask.Match(
            static value => ValueTask.FromResult(value.Length),
            static error => ValueTask.FromResult(error.Detail.Length));

        // Assert
        (matched).ShouldBe("Unexpected failure".Length);
    }

    [Fact]
    public async Task Matches_an_asynchronous_ValueTask_result_with_a_Task_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Error<string>("Unexpected failure"));

        // Act
        var matched = await resultTask.Match(
            static value => Task.FromResult(value.Length),
            static error => Task.FromResult(error.Detail.Length));

        // Assert
        (matched).ShouldBe("Unexpected failure".Length);
    }

    [Fact]
    public async Task Matches_an_asynchronous_Task_non_generic_result()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Error("Unexpected failure"));

        // Act
        var matched = await resultTask.Match(
            static () => "success",
            static error => error.Detail);

        // Assert
        (matched).ShouldBe("Unexpected failure");
    }

    [Fact]
    public async Task Matches_an_asynchronous_ValueTask_non_generic_result_with_a_Task_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok());

        // Act
        var matched = await resultTask.Match(
            static () => Task.FromResult("success"),
            static error => Task.FromResult(error.Detail));

        // Assert
        (matched).ShouldBe("success");
    }

    [Fact]
    public async Task Matches_an_asynchronous_Task_non_generic_result_with_a_Task_delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok());

        // Act
        var matched = await resultTask.Match(
            static () => Task.FromResult("success"),
            static error => Task.FromResult(error.Detail));

        // Assert
        (matched).ShouldBe("success");
    }

    [Fact]
    public async Task Matches_an_asynchronous_Task_non_generic_result_with_a_ValueTask_delegate()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Ok());

        // Act
        var matched = await resultTask.Match(
            static () => ValueTask.FromResult("success"),
            static error => ValueTask.FromResult(error.Detail));

        // Assert
        (matched).ShouldBe("success");
    }

    [Fact]
    public async Task Matches_an_asynchronous_ValueTask_non_generic_result_with_a_sync_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Error("Unexpected failure"));

        // Act
        var matched = await resultTask.Match(
            static () => "success",
            static error => error.Detail);

        // Assert
        (matched).ShouldBe("Unexpected failure");
    }

    [Fact]
    public async Task Matches_an_asynchronous_ValueTask_non_generic_result_with_a_ValueTask_delegate()
    {
        // Arrange
        var resultTask = ValueTask.FromResult(Result.Ok());

        // Act
        var matched = await resultTask.Match(
            static () => ValueTask.FromResult("success"),
            static error => ValueTask.FromResult(error.Detail));

        // Assert
        (matched).ShouldBe("success");
    }

    [Fact]
    public async Task Rejects_a_null_Task_result_source_for_map()
    {
        // Arrange
        var exception = await ((Func<Task>)(() => ResultTaskExtensions.Map(NullArgumentData.Task<Result<string>>(), static value => value.Length))).ShouldThrow<ArgumentNullException>();

        // Assert
        (exception.ParamName).ShouldBe("source");
    }

    [Fact]
    public async Task Rejects_a_null_Task_result_source_for_bind()
    {
        // Arrange
        var exception = await ((Func<Task>)(() => ResultTaskExtensions.Bind(NullArgumentData.Task<Result<string>>(), static value => Result.Ok(value.Length)))).ShouldThrow<ArgumentNullException>();

        // Assert
        (exception.ParamName).ShouldBe("source");
    }

    [Fact]
    public async Task Rejects_a_null_Task_result_source_for_ensure()
    {
        // Arrange
        var exception = await ((Func<Task>)(() => ResultTaskExtensions.Ensure(NullArgumentData.Task<Result<string>>(), static value => value.Length == 5, new ResultError("Length mismatch")))).ShouldThrow<ArgumentNullException>();

        // Assert
        (exception.ParamName).ShouldBe("source");
    }

    [Fact]
    public async Task Rejects_a_null_Task_result_source_for_match()
    {
        // Arrange
        var exception = await ((Func<Task>)(() => ResultTaskExtensions.Match(NullArgumentData.Task<Result<string>>(), static value => value.Length, static error => error.Detail.Length))).ShouldThrow<ArgumentNullException>();

        // Assert
        (exception.ParamName).ShouldBe("source");
    }

    [Fact]
    public async Task Rejects_a_null_Task_map_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = await ((Func<Task>)(() => result.Map(NullArgumentData.TaskFunc<string, int>()))).ShouldThrow<ArgumentNullException>();

        // Assert
        (exception.ParamName).ShouldBe("map");
    }

    [Fact]
    public async Task Rejects_a_null_ValueTask_map_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = await ((Func<Task>)(async () => await result.Map(NullArgumentData.ValueTaskFunc<string, int>()))).ShouldThrow<ArgumentNullException>();

        // Assert
        (exception.ParamName).ShouldBe("map");
    }

    [Fact]
    public async Task Rejects_a_null_Task_bind_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = await ((Func<Task>)(() => result.Bind(NullArgumentData.TaskFunc<string, Result<int>>()))).ShouldThrow<ArgumentNullException>();

        // Assert
        (exception.ParamName).ShouldBe("bind");
    }

    [Fact]
    public async Task Rejects_a_null_ValueTask_bind_delegate()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = await ((Func<Task>)(async () => await result.Bind(NullArgumentData.ValueTaskFunc<string, Result<int>>()))).ShouldThrow<ArgumentNullException>();

        // Assert
        (exception.ParamName).ShouldBe("bind");
    }

    [Fact]
    public async Task Rejects_a_null_Task_match_delegate_for_generic_result()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = await ((Func<Task>)(() => result.Match(NullArgumentData.TaskFunc<string, int>(), static error => Task.FromResult(error.Detail.Length)))).ShouldThrow<ArgumentNullException>();

        // Assert
        (exception.ParamName).ShouldBe("whenSuccess");
    }

    [Fact]
    public async Task Rejects_a_null_ValueTask_match_delegate_for_generic_result()
    {
        // Arrange
        var result = Result.Ok("porto");
        var exception = await ((Func<Task>)(async () => await result.Match(NullArgumentData.ValueTaskFunc<string, int>(), static error => ValueTask.FromResult(error.Detail.Length)))).ShouldThrow<ArgumentNullException>();

        // Assert
        (exception.ParamName).ShouldBe("whenSuccess");
    }

    [Fact]
    public async Task Rejects_a_null_Task_match_delegate_for_non_generic_result()
    {
        // Arrange
        var result = Result.Ok();
        var exception = await ((Func<Task>)(() => result.Match(NullArgumentData.TaskFactory<int>(), static error => Task.FromResult(error.Detail.Length)))).ShouldThrow<ArgumentNullException>();

        // Assert
        (exception.ParamName).ShouldBe("whenSuccess");
    }

    [Fact]
    public async Task Rejects_a_null_ValueTask_match_delegate_for_non_generic_result()
    {
        // Arrange
        var result = Result.Ok();
        var exception = await ((Func<Task>)(async () => await result.Match(NullArgumentData.ValueTaskFactory<int>(), static error => ValueTask.FromResult(error.Detail.Length)))).ShouldThrow<ArgumentNullException>();

        // Assert
        (exception.ParamName).ShouldBe("whenSuccess");
    }

    [Fact]
    public async Task Throws_for_an_uninitialized_non_generic_result_with_Task_match()
    {
        // Arrange
        var result = default(Result);

        // Act
        var exception = await ((Func<Task>)(() => result.Match(static () => Task.FromResult("success"), static error => Task.FromResult(error.Detail)))).ShouldThrow<InvalidOperationException>();

        // Assert
        (exception.Message).ShouldBe("Result status is not initialized.");
    }

    [Fact]
    public async Task Throws_for_an_uninitialized_generic_result_with_ValueTask_match()
    {
        // Arrange
        var result = default(Result<string>);

        // Act
        var exception = await ((Func<Task>)(async () => await result.Match(static value => ValueTask.FromResult(value.Length), static error => ValueTask.FromResult(error.Detail.Length)))).ShouldThrow<InvalidOperationException>();

        // Assert
        (exception.Message).ShouldBe("Result status is not initialized.");
    }
}
