namespace PandaBear.Testing;

/// <summary>
/// What a test verifies. The value of the <c>Focus</c> trait. Add a member only when a repository needs to filter by it.
/// </summary>
public enum TestFocus
{
    /// <summary>The primary behavior of the unit under test.</summary>
    CoreBehavior,

    /// <summary>A rule owned by the domain model.</summary>
    DomainRule,

    /// <summary>A boundary or unusual input.</summary>
    EdgeCase,

    /// <summary>A previously fixed defect that must not return.</summary>
    Regression,

    /// <summary>A security property such as authorization, validation or data exposure.</summary>
    Security,

    /// <summary>A guard that fails when generated or published output drifts from its source.</summary>
    DriftGuard,

    /// <summary>A schema or data migration.</summary>
    Migration,
}
