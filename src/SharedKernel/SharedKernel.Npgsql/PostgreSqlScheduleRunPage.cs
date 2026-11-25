namespace PandaBear.Npgsql;

/// <summary>Contains one page of scheduling run metadata, newest first.</summary>
/// <param name="Runs">The selected observations.</param>
/// <param name="Next">The continuation when more observations are available.</param>
public sealed record PostgreSqlScheduleRunPage(IReadOnlyList<PostgreSqlScheduleRun> Runs, PostgreSqlScheduleRunCursor? Next);
