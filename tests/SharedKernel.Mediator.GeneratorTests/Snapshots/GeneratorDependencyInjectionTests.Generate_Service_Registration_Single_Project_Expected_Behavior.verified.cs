namespace SharedKernel.Mediator;

public static partial class SharedKernelMediatorServiceCollectionExtensions
{
    public static IServiceCollection AddSharedKernelMediator(this IServiceCollection services)
    {
        global::System.ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<global::Demo.CreateTourHandler>();
        services.AddTransient<Mediator.ICommandHandler<global::Demo.CreateTour, int>, global::Demo.CreateTourHandler>();
        services.AddTransient<global::Demo.ValidationBehavior>();
        services.AddTransient<Mediator.IPipelineBehavior<global::Demo.CreateTour, int>, global::Demo.ValidationBehavior>();

        services.AddTransient<global::Demo.TourCreatedHandler>();
        services.AddTransient<Mediator.INotificationHandler<global::Demo.TourCreated>, global::Demo.TourCreatedHandler>();

        services.AddTransient<global::Demo.StreamToursHandler>();
        services.AddTransient<Mediator.IStreamRequestHandler<global::Demo.StreamTours, string>, global::Demo.StreamToursHandler>();

        return services;
    }
}
