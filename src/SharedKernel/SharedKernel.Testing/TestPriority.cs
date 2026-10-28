namespace PandaBear.Testing;

/// <summary>
/// How important a test is for fast feedback. The value of the <c>Priority</c> trait.
/// </summary>
public enum TestPriority
{
    /// <summary>A fast check that the system is basically alive.</summary>
    Smoke,

    /// <summary>A check whose failure blocks a release.</summary>
    Critical,
}
