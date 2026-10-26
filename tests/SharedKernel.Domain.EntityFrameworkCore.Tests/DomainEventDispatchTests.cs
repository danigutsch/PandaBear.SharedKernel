using Microsoft.Extensions.DependencyInjection;
using PandaBear.EntityFrameworkCore;

namespace PandaBear.Domain.EntityFrameworkCore.Tests;

public sealed class DomainEventDispatchTests
{
    [Fact]
    public async Task Save_changes_dispatches_the_events_of_changed_aggregates_then_clears_them()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var dispatcher = new RecordingDomainEventDispatcher();
        await using var provider = OrderServices.BuildProvider(dispatcher);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = new Order { Id = 7 };
        order.Place();
        context.Orders.Add(order);

        // Act
        _ = await context.SaveChangesAsync(ct);

        // Assert
        dispatcher.Dispatched.ShouldBe([new OrderPlacedDomainEvent(7)]);
        order.GetDomainEvents().ShouldBeEmpty();
    }

    [Fact]
    public async Task Save_changes_without_domain_events_dispatches_nothing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var dispatcher = new RecordingDomainEventDispatcher();
        await using var provider = OrderServices.BuildProvider(dispatcher);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        context.Orders.Add(new Order { Id = 1 });

        // Act
        _ = await context.SaveChangesAsync(ct);

        // Assert
        dispatcher.Dispatched.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_retried_save_after_a_failed_save_does_not_dispatch_the_same_event_again()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var dispatcher = new RecordingDomainEventDispatcher();
        await using var provider = OrderServices.BuildProvider(dispatcher, new FailOnceSaveInterceptor());
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = new Order { Id = 9 };
        order.Place();
        context.Orders.Add(order);
        var firstSave = async () => await context.SaveChangesAsync(ct);
        _ = await firstSave.ShouldThrow<InvalidOperationException>();

        // Act
        _ = await context.SaveChangesAsync(ct);

        // Assert
        dispatcher.Dispatched.ShouldBe([new OrderPlacedDomainEvent(9)]);
    }

    [Fact]
    public async Task Synchronous_save_changes_is_rejected()
    {
        // Arrange
        await using var provider = OrderServices.BuildProvider(new RecordingDomainEventDispatcher());
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        context.Orders.Add(new Order { Id = 2 });
        Action save = () => context.SaveChanges();

        // Act
        var exception = save.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("SaveChangesAsync", StringComparison.Ordinal);
    }

    [Fact]
    public void Registering_the_dispatch_twice_adds_one_configuration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _ = services.AddDomainEventDispatch<OrderDbContext>().AddDomainEventDispatch<OrderDbContext>();

        // Assert
        services.Count(descriptor => descriptor.ServiceType == typeof(IDbContextConfiguration<OrderDbContext>))
            .ShouldBe(1);
    }

    [Fact]
    public void Registration_rejects_a_missing_service_collection()
    {
        // Arrange
        Action register = () => DomainEventDispatchServiceCollectionExtensions.AddDomainEventDispatch<OrderDbContext>(null!);

        // Act
        var exception = register.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("services");
    }
}
