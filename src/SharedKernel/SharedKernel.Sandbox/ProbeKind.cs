namespace SharedKernel.Sandbox;

/// <summary>Which way a probe's outcome is good.</summary>
public enum ProbeKind
{
    /// <summary>Something the environment should allow; <see cref="ProbeOutcome.Allowed"/> is good.</summary>
    Capability,

    /// <summary>Something the environment should block, such as reading a credential folder; <see cref="ProbeOutcome.Denied"/> is good.</summary>
    Protection,
}
