namespace SharedKernel.Packages;

/// <summary>The reported disposition of one change in an explicit apply run.</summary>
public enum PackageChangeOutcome
{
    /// <summary>The selected executor reported success.</summary>
    Applied,

    /// <summary>The change could not be applied because an operation failed.</summary>
    Failed,

    /// <summary>The caller classified the change as unsupported.</summary>
    Unsupported,

    /// <summary>The requested elevated boundary was unavailable.</summary>
    ElevationUnavailable,

    /// <summary>The caller did not authorize elevation for this change.</summary>
    ElevationDenied,
}
