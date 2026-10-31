namespace PandaBear.Observability;

/// <summary>Collects the host-owned attribute predicates the privacy processors apply on top of the shared baseline.</summary>
internal sealed class TelemetryPrivacyPolicy
{
    private Func<string, bool>? additionalSensitiveAttribute;

    /// <summary>Adds a host predicate; an attribute is removed when any added predicate matches.</summary>
    public void Add(Func<string, bool> predicate)
    {
        var existing = additionalSensitiveAttribute;
        additionalSensitiveAttribute = existing is null ? predicate : name => existing(name) || predicate(name);
    }

    /// <summary>Returns whether a host predicate marks the attribute as sensitive.</summary>
    public bool IsSensitive(string name)
    {
        return additionalSensitiveAttribute?.Invoke(name) == true;
    }
}
