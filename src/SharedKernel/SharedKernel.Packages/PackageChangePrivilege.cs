namespace SharedKernel.Packages;

/// <summary>The caller's privilege classification for one proposed change.</summary>
public enum PackageChangePrivilege
{
    /// <summary>The backend cannot safely perform the change.</summary>
    Unsupported,

    /// <summary>The change uses the standard execution boundary.</summary>
    Unprivileged,

    /// <summary>The change requires the isolated elevated execution boundary.</summary>
    Elevated,
}
