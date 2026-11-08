namespace PandaBear.Packages;

/// <summary>How an installed package state differs from a requirement.</summary>
public enum PackageChangeKind
{
    /// <summary>The package is not installed.</summary>
    Install,

    /// <summary>The package is installed at a different version than the exact one that is required.</summary>
    ChangeVersion,
}
