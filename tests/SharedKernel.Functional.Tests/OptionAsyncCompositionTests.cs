namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
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
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public async Task Maps_a_value_with_a_ValueTask_delegate()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var mapped = await option.Map(static value => ValueTask.FromResult(value.Length));

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public async Task Binds_a_value_with_a_ValueTask_delegate()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var bound = await option.Bind(static value => ValueTask.FromResult(Option.Some(value.Length)));

        // Assert
        Assert.Equal(Option.Some(5), bound);
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
        Assert.Equal("EMPTY", matched);
    }

    [Fact]
    public async Task Maps_an_asynchronous_Task_option()
    {
        // Arrange
        var optionTask = Task.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => value.Length);

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public async Task Binds_an_asynchronous_ValueTask_option()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => Task.FromResult(Option.Some(value.Length)));

        // Assert
        Assert.Equal(Option.Some(5), bound);
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
        Assert.Equal("PORTO", matched);
    }

    [Fact]
    public async Task Maps_an_asynchronous_Task_option_with_a_Task_delegate()
    {
        // Arrange
        var optionTask = Task.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => Task.FromResult(value.Length));

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public async Task Maps_an_asynchronous_ValueTask_option_with_a_ValueTask_delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => ValueTask.FromResult(value.Length));

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public async Task Binds_an_asynchronous_Task_option_with_a_Task_delegate()
    {
        // Arrange
        var optionTask = Task.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => Task.FromResult(Option.Some(value.Length)));

        // Assert
        Assert.Equal(Option.Some(5), bound);
    }

    [Fact]
    public async Task Maps_an_asynchronous_ValueTask_option_with_a_sync_delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => value.Length);

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public async Task Maps_an_asynchronous_ValueTask_option_with_a_Task_delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => Task.FromResult(value.Length));

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public async Task Binds_an_asynchronous_Task_option_with_a_ValueTask_delegate()
    {
        // Arrange
        var optionTask = Task.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => ValueTask.FromResult(Option.Some(value.Length)));

        // Assert
        Assert.Equal(Option.Some(5), bound);
    }

    [Fact]
    public async Task Binds_an_asynchronous_ValueTask_option_with_a_sync_delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => Option.Some(value.Length));

        // Assert
        Assert.Equal(Option.Some(5), bound);
    }

    [Fact]
    public async Task Binds_an_asynchronous_ValueTask_option_with_a_ValueTask_delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => ValueTask.FromResult(Option.Some(value.Length)));

        // Assert
        Assert.Equal(Option.Some(5), bound);
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
        Assert.Equal("PORTO", matched);
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
        Assert.Equal("PORTO", matched);
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
        Assert.Equal("PORTO", matched);
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
        Assert.Equal("PORTO", matched);
    }

    [Fact]
    public async Task Rejects_a_null_Task_option_source_for_map()
    {
        // Arrange
        var source = NullArgumentData.Task<Option<string>>();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => source.Map(static value => value.Length));

        // Assert
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_Task_option_source_for_bind()
    {
        // Arrange
        var source = NullArgumentData.Task<Option<string>>();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => source.Bind(static value => Option.Some(value.Length)));

        // Assert
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_Task_option_source_for_match()
    {
        // Arrange
        var source = NullArgumentData.Task<Option<string>>();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => source.Match(static value => value.Length, static () => 0));

        // Assert
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_Task_map_delegate()
    {
        // Arrange
        var option = Option.Some("porto");
        var map = NullArgumentData.TaskFunc<string, int>();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => option.Map(map));

        // Assert
        Assert.Equal("map", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_ValueTask_map_delegate()
    {
        // Arrange
        var option = Option.Some("porto");
        var map = NullArgumentData.ValueTaskFunc<string, int>();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () => await option.Map(map));

        // Assert
        Assert.Equal("map", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_Task_bind_delegate()
    {
        // Arrange
        var option = Option.Some("porto");
        var bind = NullArgumentData.TaskFunc<string, Option<int>>();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => option.Bind(bind));

        // Assert
        Assert.Equal("bind", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_ValueTask_bind_delegate()
    {
        // Arrange
        var option = Option.Some("porto");
        var bind = NullArgumentData.ValueTaskFunc<string, Option<int>>();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () => await option.Bind(bind));

        // Assert
        Assert.Equal("bind", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_Task_match_delegate()
    {
        // Arrange
        var option = Option.Some("porto");
        var whenSome = NullArgumentData.TaskFunc<string, int>();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => option.Match(whenSome, static () => Task.FromResult(0)));

        // Assert
        Assert.Equal("whenSome", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_a_null_ValueTask_match_delegate()
    {
        // Arrange
        var option = Option.Some("porto");
        var whenSome = NullArgumentData.ValueTaskFunc<string, int>();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(async () => await option.Match(whenSome, static () => ValueTask.FromResult(0)));

        // Assert
        Assert.Equal("whenSome", exception.ParamName);
    }
}
