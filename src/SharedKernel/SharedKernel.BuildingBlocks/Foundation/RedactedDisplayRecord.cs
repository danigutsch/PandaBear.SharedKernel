namespace SharedKernel.BuildingBlocks;

/// <summary>Provides a redacted text representation for a record that carries sensitive values.</summary>
/// <remarks>Pass explicitly selected scalar metadata to structured loggers.</remarks>
public abstract record RedactedDisplayRecord
{
    /// <summary>Returns the concrete record type name followed by the redaction marker.</summary>
    public sealed override string ToString() => GetType().Name + " [redacted]";
}
