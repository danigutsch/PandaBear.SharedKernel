namespace PandaBear.RepoConfig.Tool;

/// <summary>How a managed file compares with the content that the baseline configuration produces.</summary>
internal enum BaselineStatus
{
    /// <summary>The file equals the desired content.</summary>
    Current,

    /// <summary>The file does not exist.</summary>
    Missing,

    /// <summary>The file is managed and unedited, but the desired content changed.</summary>
    Outdated,

    /// <summary>The file carries the marker, but its content no longer matches the marker's hash.</summary>
    Modified,

    /// <summary>The file exists without the marker and differs from the desired content.</summary>
    Unmanaged,
}
