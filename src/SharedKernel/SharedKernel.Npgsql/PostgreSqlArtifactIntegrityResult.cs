namespace SharedKernel.Npgsql;

/// <summary>Reports the integrity of one selected artifact without its contents or digest.</summary>
/// <param name="Id">The opaque metadata identifier and page cursor.</param>
/// <param name="Status">The reconciliation outcome.</param>
public readonly record struct PostgreSqlArtifactIntegrityResult(Guid Id, PostgreSqlArtifactIntegrityStatus Status);
