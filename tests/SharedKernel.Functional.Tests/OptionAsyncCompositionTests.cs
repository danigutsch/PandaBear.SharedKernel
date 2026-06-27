namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
public sealed class OptionAsyncCompositionTests
{
    [Fact]
    public async Task Maps_A_Value_With_A_Task_Delegate()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var mapped = await option.Map(static value => Task.FromResult(value.Length));

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public async Task Maps_A_Value_With_A_ValueTask_Delegate()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var mapped = await option.Map(static value => ValueTask.FromResult(value.Length));

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public async Task Binds_A_Value_With_A_ValueTask_Delegate()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var bound = await option.Bind(static value => ValueTask.FromResult(Option.Some(value.Length)));

        // Assert
        Assert.Equal(Option.Some(5), bound);
    }

    [Fact]
    public async Task Matches_A_None_With_Asynchronous_Delegates()
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
    public async Task Maps_An_Asynchronous_Task_Option()
    {
        // Arrange
        var optionTask = Task.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => value.Length);

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public async Task Binds_An_Asynchronous_ValueTask_Option()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => Task.FromResult(Option.Some(value.Length)));

        // Assert
        Assert.Equal(Option.Some(5), bound);
    }

    [Fact]
    public async Task Matches_An_Asynchronous_Task_Option()
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
    public async Task Maps_An_Asynchronous_Task_Option_With_A_Task_Delegate()
    {
        // Arrange
        var optionTask = Task.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => Task.FromResult(value.Length));

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public async Task Maps_An_Asynchronous_ValueTask_Option_With_A_ValueTask_Delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => ValueTask.FromResult(value.Length));

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public async Task Binds_An_Asynchronous_Task_Option_With_A_Task_Delegate()
    {
        // Arrange
        var optionTask = Task.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => Task.FromResult(Option.Some(value.Length)));

        // Assert
        Assert.Equal(Option.Some(5), bound);
    }

    [Fact]
    public async Task Maps_An_Asynchronous_ValueTask_Option_With_A_Sync_Delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => value.Length);

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public async Task Maps_An_Asynchronous_ValueTask_Option_With_A_Task_Delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var mapped = await optionTask.Map(static value => Task.FromResult(value.Length));

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public async Task Binds_An_Asynchronous_Task_Option_With_A_ValueTask_Delegate()
    {
        // Arrange
        var optionTask = Task.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => ValueTask.FromResult(Option.Some(value.Length)));

        // Assert
        Assert.Equal(Option.Some(5), bound);
    }

    [Fact]
    public async Task Binds_An_Asynchronous_ValueTask_Option_With_A_Sync_Delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => Option.Some(value.Length));

        // Assert
        Assert.Equal(Option.Some(5), bound);
    }

    [Fact]
    public async Task Binds_An_Asynchronous_ValueTask_Option_With_A_ValueTask_Delegate()
    {
        // Arrange
        var optionTask = ValueTask.FromResult(Option.Some("porto"));

        // Act
        var bound = await optionTask.Bind(static value => ValueTask.FromResult(Option.Some(value.Length)));

        // Assert
        Assert.Equal(Option.Some(5), bound);
    }

    [Fact]
    public async Task Matches_An_Asynchronous_Task_Option_With_A_Task_Delegate()
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
    public async Task Matches_An_Asynchronous_ValueTask_Option_With_A_Sync_Delegate()
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
    public async Task Matches_An_Asynchronous_ValueTask_Option_With_A_ValueTask_Delegate()
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
    public async Task Matches_An_Asynchronous_ValueTask_Option_With_A_Task_Delegate()
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
    public async Task Rejects_A_Null_Task_Option_Source_For_Map()
    {
        // Arrange
        var source = NullArgumentData.Task<Option<string>>();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => source.Map(static value => value.Length));

        // Assert
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_A_Null_Task_Option_Source_For_Bind()
    {
        // Arrange
        var source = NullArgumentData.Task<Option<string>>();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => source.Bind(static value => Option.Some(value.Length)));

        // Assert
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_A_Null_Task_Option_Source_For_Match()
    {
        // Arrange
        var source = NullArgumentData.Task<Option<string>>();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => source.Match(static value => value.Length, static () => 0));

        // Assert
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_A_Null_Task_Map_Delegate()
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
    public async Task Rejects_A_Null_ValueTask_Map_Delegate()
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
    public async Task Rejects_A_Null_Task_Bind_Delegate()
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
    public async Task Rejects_A_Null_ValueTask_Bind_Delegate()
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
    public async Task Rejects_A_Null_Task_Match_Delegate()
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
    public async Task Rejects_A_Null_ValueTask_Match_Delegate()
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
