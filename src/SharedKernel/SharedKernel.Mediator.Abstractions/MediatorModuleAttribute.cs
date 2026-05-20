namespace SharedKernel.Mediator;

/// <summary>
/// Marks an assembly as a mediator module for cross-assembly discovery.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class MediatorModuleAttribute : Attribute;
