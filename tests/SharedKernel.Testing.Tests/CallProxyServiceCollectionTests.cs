using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Testing.Tests;

/// <summary>Tests replacing the registrations of a service with <see cref="CallProxy"/> wrappers.</summary>
public sealed class CallProxyServiceCollectionTests
{
    private static readonly ServiceProviderOptions Validated = new() { ValidateScopes = true, ValidateOnBuild = true };

    [Theory]
    [InlineData(ServiceLifetime.Singleton, true, true)]
    [InlineData(ServiceLifetime.Scoped, true, false)]
    [InlineData(ServiceLifetime.Transient, false, false)]
    public async Task A_wrapped_registration_keeps_its_lifetime(ServiceLifetime lifetime, bool sameWithinScope, bool sameAcrossScopes)
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();
        services.Add(new ServiceDescriptor(typeof(ICallProbe), typeof(CallProbe), lifetime));
        services.WrapWithCallProxy<ICallProbe>(hooks => hooks.Twice.Returns(99));
        await using var provider = services.BuildServiceProvider(Validated);
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();

        // Act
        var proxy = first.ServiceProvider.GetRequiredService<ICallProbe>();
        var again = first.ServiceProvider.GetRequiredService<ICallProbe>();
        var other = second.ServiceProvider.GetRequiredService<ICallProbe>();
        var replaced = await proxy.Twice(1);

        // Assert
        proxy.ShouldNotBeOfType<CallProbe>();
        replaced.ShouldBe(99);
        ReferenceEquals(proxy, again).ShouldBe(sameWithinScope);
        ReferenceEquals(proxy, other).ShouldBe(sameAcrossScopes);
    }

    [Fact]
    public async Task The_resolved_service_runs_the_hooks_around_the_real_implementation()
    {
        // Arrange
        var seen = new List<string>();
        var services = new ServiceCollection();
        services.AddScoped<ICallProbe, CallProbe>();
        services.WrapWithCallProxy<ICallProbe>(hooks =>
        {
            hooks.Thrice.Before(call => seen.Add($"before {call.Value}"));
            hooks.Twice.Returns(99);
        });
        await using var provider = services.BuildServiceProvider(Validated);
        await using var scope = provider.CreateAsyncScope();
        var proxy = scope.ServiceProvider.GetRequiredService<ICallProbe>();

        // Act
        var replaced = await proxy.Twice(1);
        var forwarded = await proxy.Thrice(2);

        // Assert
        replaced.ShouldBe(99);
        forwarded.ShouldBe(6);
        seen.ShouldBe(["before 2"]);
    }

    [Fact]
    public async Task Configure_runs_once_when_the_registration_is_wrapped()
    {
        // Arrange
        var configured = 0;
        var services = new ServiceCollection();
        services.AddTransient<ICallProbe, CallProbe>();

        // Act
        services.WrapWithCallProxy<ICallProbe>(_ => configured++);
        await using var provider = services.BuildServiceProvider(Validated);
        _ = provider.GetRequiredService<ICallProbe>();
        _ = provider.GetRequiredService<ICallProbe>();

        // Assert
        configured.ShouldBe(1);
    }

    [Fact]
    public async Task Every_proxy_of_a_wrapped_registration_shares_the_hooks()
    {
        // Arrange
        var calls = 0;
        var services = new ServiceCollection();
        services.AddTransient<ICallProbe, CallProbe>();
        services.WrapWithCallProxy<ICallProbe>(hooks => hooks.Run.After(_ => calls++));
        await using var provider = services.BuildServiceProvider(Validated);

        // Act
        await provider.GetRequiredService<ICallProbe>().Run();
        await provider.GetRequiredService<ICallProbe>().Run();

        // Assert
        calls.ShouldBe(2);
    }

    [Fact]
    public async Task The_real_implementation_of_a_factory_registration_is_disposed_with_its_scope()
    {
        // Arrange
        var created = new List<TrackedGreeter>();
        var services = new ServiceCollection();
        services.AddScoped<IGreeter>(_ =>
        {
            var greeter = new TrackedGreeter();
            created.Add(greeter);
            return greeter;
        });
        services.WrapWithCallProxy<IGreeter>(hooks => hooks.Greet.Returns("Hi"));
        await using var provider = services.BuildServiceProvider(Validated);
        var scope = provider.CreateAsyncScope();
        var greeting = scope.ServiceProvider.GetRequiredService<IGreeter>().Greet("Ana");

        // Act
        await scope.DisposeAsync();

        // Assert
        greeting.ShouldBe("Hi");
        created.ShouldHaveSingleItem().IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task An_instance_registration_is_wrapped_and_left_undisposed()
    {
        // Arrange
        string? result = null;
        var greeter = new TrackedGreeter();
        var services = new ServiceCollection();
        services.AddSingleton<IGreeter>(greeter);
        services.WrapWithCallProxy<IGreeter>(hooks => hooks.Greet.After(call => result = $"{call.Name}: {call.Result}"));
        var provider = services.BuildServiceProvider(Validated);
        var greeting = provider.GetRequiredService<IGreeter>().Greet("Ana");

        // Act
        await provider.DisposeAsync();

        // Assert
        greeting.ShouldBe("Hello Ana");
        result.ShouldBe("Ana: Hello Ana");
        greeter.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task Every_registration_without_a_key_is_wrapped_and_keyed_ones_are_left_alone()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ICallProbe, CallProbe>();
        services.AddScoped<ICallProbe>(_ => new CallProbe());
        services.AddKeyedScoped<ICallProbe, CallProbe>("keyed");
        services.WrapWithCallProxy<ICallProbe>(hooks => hooks.Twice.Returns(99));
        await using var provider = services.BuildServiceProvider(Validated);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var all = scope.ServiceProvider.GetServices<ICallProbe>().ToList();
        var keyed = scope.ServiceProvider.GetRequiredKeyedService<ICallProbe>("keyed");
        var replaced = await all[1].Twice(1);

        // Assert
        all.ShouldHaveCount(2);
        all.ShouldAllSatisfy(static service => service.ShouldNotBeOfType<CallProbe>());
        replaced.ShouldBe(99);
        keyed.ShouldBeOfType<CallProbe>();
    }

    [Fact]
    public void Wrapping_a_service_that_is_not_registered_throws_and_names_it()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeyedScoped<ICallProbe, CallProbe>("keyed");
        Action wrap = () => services.WrapWithCallProxy<ICallProbe>(hooks => hooks.Twice.Returns(99));

        // Act
        var failure = wrap.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldBe("CallProxy found no registration of ICallProbe to wrap; register the service before wrapping it.");
    }

    [Fact]
    public void Wrapping_validates_arguments()
    {
        // Arrange
        var services = new ServiceCollection();
        IServiceCollection missing = null!;
        Action nullServices = () => missing.WrapWithCallProxy<ICallProbe>(_ => { });
        Action nullConfigure = () => services.WrapWithCallProxy<ICallProbe>(null!);

        // Act, Assert
        nullServices.ShouldThrow<ArgumentNullException>();
        nullConfigure.ShouldThrow<ArgumentNullException>();
    }
}
