namespace SharedKernel.Packages;

/// <summary>The disposition and privilege classification of one proposed package change.</summary>
public sealed class PackageChangeExecution
{
    /// <summary>Creates a change's execution record.</summary>
    internal PackageChangeExecution(
        PackageManagerKind manager, PackageChange change, PackageChangePrivilege? privilege, PackageChangeOutcome outcome, string? detail)
    {
        Manager = manager;
        Change = change;
        Privilege = privilege;
        Outcome = outcome;
        Detail = detail;
    }

    /// <summary>Gets the package manager.</summary>
    public PackageManagerKind Manager { get; }

    /// <summary>Gets the proposed change.</summary>
    public PackageChange Change { get; }

    /// <summary>Gets the recorded privilege classification, when the caller could supply one.</summary>
    public PackageChangePrivilege? Privilege { get; }

    /// <summary>Gets the disposition of the change.</summary>
    public PackageChangeOutcome Outcome { get; }

    /// <summary>Gets any failure detail, which can include the executor's diagnostic text.</summary>
    public string? Detail { get; }
}
