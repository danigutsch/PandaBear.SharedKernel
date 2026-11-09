namespace PandaBear.Packages;

/// <summary>Whether one call may contact the network. Every network source takes it per call and refuses when it is <see cref="Denied"/>.</summary>
public enum NetworkAccess
{
    /// <summary>No outbound call may be made. This is the default value.</summary>
    Denied = 0,

    /// <summary>The call may contact the source's remote service.</summary>
    Allowed = 1,
}
