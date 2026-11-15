using System.Text.RegularExpressions;

namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Generates Pix email key candidates and holds the DICT email pattern as the reference.</summary>
internal static partial class PixKeyEmailCandidates
{
    private const string Alphabet = "ab0-.@!#$'*+/=?^_`{|}~%&A é\n";

    /// <summary>Returns seeded random candidates followed by boundary cases.</summary>
    public static IEnumerable<string> All(int seed, int count)
    {
        var random = new Random(seed);
        return Enumerable.Range(0, count).Select(_ => Random(random)).Concat(EdgeCases());
    }

    /// <summary>Returns whether the DICT email pattern, with the 77-character limit, accepts the value.</summary>
    public static bool IsDictEmail(string value) => value.Length <= 77 && ReferencePattern().IsMatch(value);

    private static string Random(Random random)
    {
        var length = random.Next(0, 80);
        var characters = new char[length];
        for (var index = 0; index < length; index++)
        {
            characters[index] = Alphabet[random.Next(Alphabet.Length)];
        }

        if (length > 2 && random.Next(2) == 0)
        {
            characters[random.Next(1, length - 1)] = '@';
        }

        return new string(characters);
    }

    private static IEnumerable<string> EdgeCases() =>
    [
        "a@b", "a@b.c", "a@b-c.d", "a@-b", "a@b-", "a@b.", "a@.b", "a@b..c", "@b", "a@", "a@@b", "a@b@c",
        "a@" + new string('b', 63), "a@" + new string('b', 64), "a@" + new string('b', 63) + ".c",
        "a@b" + new string('-', 61) + "c", "a@b" + new string('-', 62) + "c", ".@b", "a.@b", "%@b", "&@b",
    ];

    [GeneratedRegex(@"\A[a-z0-9.!#$'*+/=?^_`{|}~-]+@[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();
}
