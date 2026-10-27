using System.Collections;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Audience sequence that yields "admin-api" and "catalog-api" and then throws if enumerated further, proving a validator stops at the second value.</summary>
internal sealed class ExactAudienceEnumerable : IEnumerable<string>
{
    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator()
    {
        yield return "admin-api";
        yield return "catalog-api";
        throw new InvalidOperationException("Audience validation must not enumerate past the second value.");
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
