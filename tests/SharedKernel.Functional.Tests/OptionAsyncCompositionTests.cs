namespace SharedKernel.Functional.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
public sealed class OptionAsyncCompositionTests
{
    [Fact]
    public async Task Maps_a_value_with_a_Task_delegate()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var mapped = await option.Map(static value => Task.FromResult(value.Length));

        // Assert
        mapped.ShouldBe(Option.Some(5));
    }

    [Fact]
    public async Task Maps_a_value_with_a_ValueTask_delegate()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var mapped = await option.Map(static value => ValueTask.FromResult(value.Length));

        // Assert
        mapped.ShouldBe(Option.Some(5));
    }

    [Fact]
    public async Task Binds_a_value_with_a_ValueTask_delegate()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var bound = await option.Bind(static value => ValueTask.FromResult(Option.Some(value.Length)));

        // Assert
        bound.ShouldBe(Option.Some(5));
    }

    [Fact]
    public async Task Matches_a_none_with_asynchronous_delegates()
    {
        // Arrange
        var option = Option.None<string>();

        // Act
        var matched = await option.Match(
            static value => Task.FromResult(value.ToUpperInvariant()),
            static () => Task.FromResult("EMPTY"));

        // Assert
        matched.ShouldBe("EMPTY");
    }

    [Fact]
    public async Task Maps_an_asynchronous_Task_option()
    {
        // Arrange
        var optionTask = Task.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => value.Length);

        // Assert
        mapped.ShouldBe(Option.Some(5));
    }

    [Fact]
    public async Task Binds_an_asynchronous_ValueTask_option()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => Task.FromResult(Option.Some(value.Length)));

        // Assert
        bound.ShouldBe(Option.Some(5));
    }

    [Fact]
    public async Task Matches_an_asynchronous_Task_option()
    {
        // Arrange
        var optionTask = Task.FromResult(Option.Some("porto"));

        // Act
        var matched = await optionTask.Match(
            static value => ValueTask.FromResult(value.ToUpperInvariant()),
            static () => ValueTask.FromResult("EMPTY"));

        // Assert
        matched.ShouldBe("PORTO");
    }

    [Fact]
    public async Task Maps_an_asynchronous_Task_option_with_a_Task_delegate()
    {
        // Arrange
        var optionTask = Task.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => Task.FromResult(value.Length));

        // Assert
        mapped.ShouldBe(Option.Some(5));
    }

    [Fact]
    public async Task Maps_an_asynchronous_ValueTask_option_with_a_ValueTask_delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => ValueTask.FromResult(value.Length));

        // Assert
        mapped.ShouldBe(Option.Some(5));
    }

    [Fact]
    public async Task Binds_an_asynchronous_Task_option_with_a_Task_delegate()
    {
        // Arrange
        var optionTask = Task.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => Task.FromResult(Option.Some(value.Length)));

        // Assert
        bound.ShouldBe(Option.Some(5));
    }

    [Fact]
    public async Task Maps_an_asynchronous_ValueTask_option_with_a_sync_delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => value.Length);

        // Assert
        mapped.ShouldBe(Option.Some(5));
    }

    [Fact]
    public async Task Maps_an_asynchronous_ValueTask_option_with_a_Task_delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => Task.FromResult(value.Length));

        // Assert
        mapped.ShouldBe(Option.Some(5));
    }

    [Fact]
    public async Task Binds_an_asynchronous_Task_option_with_a_ValueTask_delegate()
    {
        // Arrange
        var optionTask = Task.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => ValueTask.FromResult(Option.Some(value.Length)));

        // Assert
        bound.ShouldBe(Option.Some(5));
    }

    [Fact]
    public async Task Binds_an_asynchronous_ValueTask_option_with_a_sync_delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => Option.Some(value.Length));

        // Assert
        bound.ShouldBe(Option.Some(5));
    }

    [Fact]
    public async Task Binds_an_asynchronous_ValueTask_option_with_a_ValueTask_delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => ValueTask.FromResult(Option.Some(value.Length)));

        // Assert
        bound.ShouldBe(Option.Some(5));
    }

    [Fact]
    public async Task Matches_an_asynchronous_Task_option_with_a_Task_delegate()
    {
        // Arrange
        var optionTask = Task.FromResult(Option.Some("porto"));

        // Act
        var matched = await optionTask.Match(
            static value => Task.FromResult(value.ToUpperInvariant()),
            static () => Task.FromResult("EMPTY"));

        // Assert
        matched.ShouldBe("PORTO");
    }

    [Fact]
    public async Task Matches_an_asynchronous_ValueTask_option_with_a_sync_delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var matched = await optionTask.Match(
            static value => value.ToUpperInvariant(),
            static () => "EMPTY");

        // Assert
        matched.ShouldBe("PORTO");
    }

    [Fact]
    public async Task Matches_an_asynchronous_ValueTask_option_with_a_ValueTask_delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var matched = await optionTask.Match(
            static value => ValueTask.FromResult(value.ToUpperInvariant()),
            static () => ValueTask.FromResult("EMPTY"));

        // Assert
        matched.ShouldBe("PORTO");
    }

    [Fact]
    public async Task Matches_an_asynchronous_ValueTask_option_with_a_Task_delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var matched = await optionTask.Match(
            static value => Task.FromResult(value.ToUpperInvariant()),
            static () => Task.FromResult("EMPTY"));

        // Assert
        matched.ShouldBe("PORTO");
    }

    [Fact]
    public async Task Rejects_a_null_Task_option_source_for_map()
    {
        // Arrange
        var source = NullArgumentData.Task<Option<string>>();

        // Act
        var exception = await ((Func<Task>)(() => source.Map(static value => value.Length))).ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("source");
    }

    [Fact]
    public async Task Rejects_a_null_Task_option_source_for_bind()
    {
        // Arrange
        var source = NullArgumentData.Task<Option<string>>();

        // Act
        var exception = await ((Func<Task>)(() => source.Bind(static value => Option.Some(value.Length)))).ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("source");
    }

    [Fact]
    public async Task Rejects_a_null_Task_option_source_for_match()
    {
        // Arrange
        var source = NullArgumentData.Task<Option<string>>();

        // Act
        var exception = await ((Func<Task>)(() => source.Match(static value => value.Length, static () => 0))).ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("source");
    }

    [Fact]
    public async Task Rejects_a_null_Task_map_delegate()
    {
        // Arrange
        var option = Option.Some("porto");
        var map = NullArgumentData.TaskFunc<string, int>();

        // Act
        var exception = await ((Func<Task>)(() => option.Map(map))).ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("map");
    }

    [Fact]
    public async Task Rejects_a_null_ValueTask_map_delegate()
    {
        // Arrange
        var option = Option.Some("porto");
        var map = NullArgumentData.ValueTaskFunc<string, int>();

        // Act
        var exception = await ((Func<Task>)(async () => await option.Map(map))).ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("map");
    }

    [Fact]
    public async Task Rejects_a_null_Task_bind_delegate()
    {
        // Arrange
        var option = Option.Some("porto");
        var bind = NullArgumentData.TaskFunc<string, Option<int>>();

        // Act
        var exception = await ((Func<Task>)(() => option.Bind(bind))).ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("bind");
    }

    [Fact]
    public async Task Rejects_a_null_ValueTask_bind_delegate()
    {
        // Arrange
        var option = Option.Some("porto");
        var bind = NullArgumentData.ValueTaskFunc<string, Option<int>>();

        // Act
        var exception = await ((Func<Task>)(async () => await option.Bind(bind))).ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("bind");
    }

    [Fact]
    public async Task Rejects_a_null_Task_match_delegate()
    {
        // Arrange
        var option = Option.Some("porto");
        var whenSome = NullArgumentData.TaskFunc<string, int>();

        // Act
        var exception = await ((Func<Task>)(() => option.Match(whenSome, static () => Task.FromResult(0)))).ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("whenSome");
    }

    [Fact]
    public async Task Rejects_a_null_ValueTask_match_delegate()
    {
        // Arrange
        var option = Option.Some("porto");
        var whenSome = NullArgumentData.ValueTaskFunc<string, int>();

        // Act
        var exception = await ((Func<Task>)(async () => await option.Match(whenSome, static () => ValueTask.FromResult(0)))).ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("whenSome");
    }
}
