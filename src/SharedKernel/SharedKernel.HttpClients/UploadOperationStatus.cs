namespace SharedKernel.HttpClients;

/// <summary>Describes the transport and server-processing stages of a caller-owned upload operation.</summary>
public enum UploadOperationStatus
{
    /// <summary>No operation is currently displayed.</summary>
    None,
    /// <summary>A new operation is waiting to report transport progress.</summary>
    Queued,
    /// <summary>A retry is waiting to report transport progress.</summary>
    Retrying,
    /// <summary>File bytes are being reported as written to the transport.</summary>
    Uploading,
    /// <summary>All declared bytes reached the transport and the server result is pending.</summary>
    Processing,
    /// <summary>The caller reported successful completion after server processing.</summary>
    Complete,
    /// <summary>The caller canceled the operation; a retry is allowed.</summary>
    Canceled,
    /// <summary>The caller reported failure; a retry is allowed.</summary>
    Failed,
}
