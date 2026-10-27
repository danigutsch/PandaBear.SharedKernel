using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>
/// Deterministic locations in two fields: a fifth are empty, most are short, and a tenth are long enough to cross index
/// block boundaries.
/// </summary>
internal static class OverlapSamples
{
    /// <summary>Returns <paramref name="count"/> deterministic locations that start below <paramref name="maxStart"/>, with lengths up to <paramref name="maxLength"/> for the long ones.</summary>
    public static IEnumerable<SecretLocation> Locations(int count, int maxStart, int maxLength)
    {
        uint state = 7;
        for (int number = 0; number < count; number++)
        {
            string fieldPath = Next(ref state) % 4 == 0 ? "notes" : "attachment:notes.txt";
            int start = (int)(Next(ref state) % (uint)maxStart);
            uint kind = Next(ref state) % 10;
            int length = kind switch
            {
                < 2 => 0,
                < 9 => 1 + (int)(Next(ref state) % 40),
                _ => 1 + (int)(Next(ref state) % (uint)maxLength),
            };
            yield return new SecretLocation(fieldPath, start, length);
        }
    }

    private static uint Next(ref uint state)
    {
        state = unchecked((state * 1_664_525) + 1_013_904_223);
        return state >> 8;
    }
}
