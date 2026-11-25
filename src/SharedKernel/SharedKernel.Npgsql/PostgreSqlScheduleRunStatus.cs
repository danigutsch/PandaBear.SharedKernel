namespace SharedKernel.Npgsql;

/// <summary>Describes the evidence available for an observed scheduling run.</summary>
public enum PostgreSqlScheduleRunStatus
{
    /// <summary>The run has no current observation or recorded completion.</summary>
    Unknown = 0,

    /// <summary>The run was reported active within its observation window.</summary>
    Running = 1,

    /// <summary>The observer recorded a successful completion.</summary>
    Succeeded = 2,

    /// <summary>The observer recorded cancellation.</summary>
    Cancelled = 3,

    /// <summary>The observer recorded failure with retry and terminal state unspecified.</summary>
    Failed = 4,

    /// <summary>The provider confirmed a separate retry is scheduled.</summary>
    RetryScheduled = 5,

    /// <summary>The provider confirmed the run failed and will not be retried.</summary>
    TerminalFailure = 6,
}
