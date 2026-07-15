using System.Collections;

namespace SharedKernel.AspNetCore.Tests;

internal sealed class ExactAudienceEnumerable : IEnumerable<string>
{
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
