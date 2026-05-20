using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Mediator;

/// <summary>
/// Represents a notification published to one or more handlers.
/// </summary>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Marker notification contract for generated mediator dispatch.")]
public interface INotification;
