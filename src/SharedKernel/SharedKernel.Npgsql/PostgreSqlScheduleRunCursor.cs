namespace PandaBear.Npgsql;

/// <summary>Identifies an exclusive position in a scheduling run history.</summary>
/// <param name="StartedAt">The last row's start observation timestamp.</param>
/// <param name="RunId">The last row's run identifier.</param>
public readonly record struct PostgreSqlScheduleRunCursor(DateTimeOffset StartedAt, Guid RunId);
