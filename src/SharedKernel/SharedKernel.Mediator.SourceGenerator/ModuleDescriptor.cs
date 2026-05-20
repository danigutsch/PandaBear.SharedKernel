namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Describes an assembly that participated in mediator discovery.
/// </summary>
internal sealed record ModuleDescriptor(
    string AssemblyName,
    bool IsPrimaryAssembly,
    bool HasModuleMarker);
