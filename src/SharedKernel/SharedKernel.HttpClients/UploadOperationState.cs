namespace PandaBear.HttpClients;

/// <summary>Tracks one caller-owned upload lifecycle, byte progress, and accessible status announcements.</summary>
/// <remarks>
/// This mutable state is not thread-safe. The caller must serialize mutations and reads on its rendering or
/// synchronization context. State transitions do not start, retry, cancel, or dispose transport operations;
/// the caller owns those operations and marshals progress callbacks to the same context.
/// Announcements contain the supplied file name and are intended for local presentation, not telemetry.
/// </remarks>
public sealed class UploadOperationState
{
    private const int AnnouncementPercentageStep = 10;
    private long _nextOperationId;
    private int _lastAnnouncedPercentage;
    private bool _uploadStartedAnnounced;

    /// <summary>Gets the identifier of the latest begun operation; resetting state does not rewind this identifier.</summary>
    public long OperationId
    {
        get; private set;
    }

    /// <summary>Gets the current lifecycle status, initially <see cref="UploadOperationStatus.None" />.</summary>
    public UploadOperationStatus Status
    {
        get; private set;
    }

    /// <summary>Gets the current caller-supplied display file name, or an empty string after reset.</summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>Gets cumulative file bytes reported as written to the HTTP transport.</summary>
    public long BytesTransferred
    {
        get; private set;
    }

    /// <summary>Gets the declared positive file length for the current operation, or zero after reset.</summary>
    public long TotalBytes
    {
        get; private set;
    }

    /// <summary>Gets the current English status announcement, with upload percentages announced in ten-percent steps.</summary>
    public string Announcement { get; private set; } = string.Empty;

    /// <summary>Gets the caller-supplied failure message, or <see langword="null" /> before failure or after reset.</summary>
    public string? FailureMessage
    {
        get; private set;
    }

    /// <summary>Gets whether the operation is queued, retrying, uploading, or awaiting server processing.</summary>
    public bool IsActive => Status is UploadOperationStatus.Queued
        or UploadOperationStatus.Retrying
        or UploadOperationStatus.Uploading
        or UploadOperationStatus.Processing;

    /// <summary>Gets whether the status is uploading, where reported transport byte progress is determinate.</summary>
    public bool IsDeterminate => Status is UploadOperationStatus.Uploading;

    /// <summary>Gets whether a canceled or failed operation can begin a retry.</summary>
    public bool CanRetry => Status is UploadOperationStatus.Canceled or UploadOperationStatus.Failed;

    /// <summary>Begins a new operation or a retry and clears the previous progress and failure message.</summary>
    /// <param name="fileName">The nonblank display file name.</param>
    /// <param name="totalBytes">The positive declared file length.</param>
    /// <param name="isRetry">Whether this operation retries the current canceled or failed operation.</param>
    /// <returns>A new monotonically increasing identifier for correlating callbacks.</returns>
    /// <exception cref="ArgumentException">The file name is null or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The declared file length is not positive.</exception>
    /// <exception cref="InvalidOperationException">An operation is active, or a retry is not currently allowed.</exception>
    /// <exception cref="OverflowException">The operation identifier counter is exhausted.</exception>
    public long Begin(string fileName, long totalBytes, bool isRetry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalBytes);

        if (IsActive)
        {
            throw new InvalidOperationException("An upload operation is already active.");
        }

        if (isRetry && !CanRetry)
        {
            throw new InvalidOperationException("Only a canceled or failed upload can be retried.");
        }

        OperationId = checked(++_nextOperationId);
        Status = isRetry ? UploadOperationStatus.Retrying : UploadOperationStatus.Queued;
        FileName = fileName;
        BytesTransferred = 0;
        TotalBytes = totalBytes;
        FailureMessage = null;
        _lastAnnouncedPercentage = 0;
        _uploadStartedAnnounced = false;
        Announcement = isRetry ? $"Retrying {fileName}." : $"{fileName} queued.";
        return OperationId;
    }

    /// <summary>Accepts cumulative transport byte progress for the current active upload.</summary>
    /// <param name="operationId">The identifier returned by <see cref="Begin" />.</param>
    /// <param name="bytesTransferred">A nonnegative, nondecreasing count no greater than the declared length.</param>
    /// <returns>Whether the report updates state; stale, inactive, processing, and duplicate uploading callbacks are ignored.</returns>
    /// <remarks>Reporting the declared length transitions to indeterminate server processing, not completion.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">An applicable report is outside the declared byte range.</exception>
    /// <exception cref="InvalidOperationException">An applicable report moves byte progress backwards.</exception>
    public bool ReportBytes(long operationId, long bytesTransferred)
    {
        if (!IsCurrentActive(operationId))
        {
            return false;
        }

        if (Status is UploadOperationStatus.Processing)
        {
            return false;
        }

        ArgumentOutOfRangeException.ThrowIfNegative(bytesTransferred);
        if (bytesTransferred > TotalBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesTransferred), bytesTransferred, "Transferred bytes cannot exceed the declared file length.");
        }

        if (bytesTransferred < BytesTransferred)
        {
            throw new InvalidOperationException("Transferred bytes cannot move backwards.");
        }

        if (bytesTransferred == BytesTransferred && Status is UploadOperationStatus.Uploading)
        {
            return false;
        }

        BytesTransferred = bytesTransferred;
        if (bytesTransferred == TotalBytes)
        {
            Status = UploadOperationStatus.Processing;
            Announcement = $"Upload complete. Server processing {FileName}.";
            return true;
        }

        Status = UploadOperationStatus.Uploading;
        UpdateUploadAnnouncement();
        return true;
    }

    /// <summary>Marks the current server-processing operation as complete.</summary>
    /// <param name="operationId">The identifier returned by <see cref="Begin" />.</param>
    /// <returns>Whether the current active operation completed; stale and inactive callbacks return false.</returns>
    /// <exception cref="InvalidOperationException">The active operation has not reported all declared bytes.</exception>
    public bool Complete(long operationId)
    {
        if (!IsCurrentActive(operationId))
        {
            return false;
        }

        if (Status is not UploadOperationStatus.Processing)
        {
            throw new InvalidOperationException("An upload cannot complete before all declared bytes reach the transport.");
        }

        Status = UploadOperationStatus.Complete;
        Announcement = $"{FileName} complete.";
        return true;
    }

    /// <summary>Marks the current active operation as canceled without canceling the caller-owned transport.</summary>
    /// <param name="operationId">The identifier returned by <see cref="Begin" />.</param>
    /// <returns>Whether cancellation updated state; stale and inactive callbacks return false.</returns>
    public bool Cancel(long operationId)
    {
        if (!IsCurrentActive(operationId))
        {
            return false;
        }

        Status = UploadOperationStatus.Canceled;
        Announcement = $"Canceled {FileName}.";
        return true;
    }

    /// <summary>Marks the current active operation as failed and records its presentation message.</summary>
    /// <param name="operationId">The identifier returned by <see cref="Begin" />.</param>
    /// <param name="failureMessage">A nonblank, user-appropriate failure message supplied by the caller.</param>
    /// <returns>Whether failure updated state; stale and inactive callbacks return false.</returns>
    /// <exception cref="ArgumentException">The failure message is null or blank, including for stale callbacks.</exception>
    public bool Fail(long operationId, string failureMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);

        if (!IsCurrentActive(operationId))
        {
            return false;
        }

        Status = UploadOperationStatus.Failed;
        FailureMessage = failureMessage;
        Announcement = $"{FileName} failed.";
        return true;
    }

    /// <summary>Checks whether a callback belongs to the latest active operation.</summary>
    /// <param name="operationId">The callback operation identifier.</param>
    /// <returns>Whether the identifier matches and the current status is active.</returns>
    public bool IsCurrentActive(long operationId) => OperationId == operationId && IsActive;

    /// <summary>Clears visible lifecycle state while preserving the operation identifier sequence.</summary>
    /// <remarks>This does not cancel transport work. Old callbacks are ignored after reset and after a subsequent begin.</remarks>
    public void Reset()
    {
        Status = UploadOperationStatus.None;
        FileName = string.Empty;
        BytesTransferred = 0;
        TotalBytes = 0;
        Announcement = string.Empty;
        FailureMessage = null;
        _lastAnnouncedPercentage = 0;
        _uploadStartedAnnounced = false;
    }

    private void UpdateUploadAnnouncement()
    {
        var percentage = (int)((Int128)BytesTransferred * 100 / TotalBytes);
        var announcementPercentage = percentage / AnnouncementPercentageStep * AnnouncementPercentageStep;
        if (announcementPercentage >= AnnouncementPercentageStep && announcementPercentage > _lastAnnouncedPercentage)
        {
            _lastAnnouncedPercentage = announcementPercentage;
            _uploadStartedAnnounced = true;
            Announcement = $"{FileName} upload is {announcementPercentage}% complete.";
            return;
        }

        if (!_uploadStartedAnnounced)
        {
            _uploadStartedAnnounced = true;
            Announcement = $"Uploading {FileName}.";
        }
    }
}
