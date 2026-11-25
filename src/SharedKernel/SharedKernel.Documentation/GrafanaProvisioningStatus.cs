namespace SharedKernel.Documentation;

/// <summary>Whether observed dashboard provisioning matches its expected targets.</summary>
public enum GrafanaProvisioningStatus
{
    /// <summary>The file-provisioned dashboard has the expected target sequence.</summary>
    Current,

    /// <summary>The dashboard is absent or its provisioning could not be confirmed.</summary>
    NotProvisioned,

    /// <summary>The target sequence differs from the expected one.</summary>
    TargetsDiffer,
}
