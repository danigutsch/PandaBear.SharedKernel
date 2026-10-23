using System.Globalization;
using System.Text;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>
/// Builds large synthetic attachment texts of a given length that stress the scanner, with the location of every secret
/// the scanner should report. Values come from a fixed linear congruential sequence, so every run sees the same text and
/// nothing in the repository is secret-shaped.
/// </summary>
internal static class LargeTexts
{
    /// <summary>
    /// Time limit, in milliseconds, of a test that scans one of these texts. A scan takes seconds on an idle machine and a
    /// few minutes with every core 32 times oversubscribed, so the limit leaves room for a busy machine; work that grows
    /// with the square of a text this large takes far longer.
    /// </summary>
    public const int ScanBudget = 600_000;

    private const string Base32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>
    /// Question lines, each followed by its answer line: one security question finding per pair, located at the answer.
    /// </summary>
    public static (string Text, IReadOnlyList<(int Start, int Length)> Answers) Questions(int length)
    {
        var builder = new StringBuilder(length + 64);
        var answers = new List<(int Start, int Length)>();
        uint state = 1;
        while (builder.Length < length - 64)
        {
            builder.Append(CultureInfo.InvariantCulture, $"Which door code opens room {Next(ref state) % 900_000 + 100_000}?\n");
            string answer = string.Create(CultureInfo.InvariantCulture, $"Code {Next(ref state) % 900_000 + 100_000}");
            answers.Add((builder.Length, answer.Length));
            builder.Append(answer).Append('\n');
        }

        return (builder.ToString(), answers);
    }

    /// <summary>Log lines that each name one AWS access key ID: one finding per line, located at the key.</summary>
    public static (string Text, IReadOnlyList<(int Start, int Length)> Keys) AccessKeyLog(int length)
    {
        const string Before = "access key ";
        const int KeyLength = 20;
        var builder = new StringBuilder(length + 64);
        var keys = new List<(int Start, int Length)>();
        uint state = 2;
        while (builder.Length < length - 64)
        {
            builder.Append(Before);
            keys.Add((builder.Length, KeyLength));
            builder.Append("AKIA");
            for (int index = 0; index < KeyLength - 4; index++)
            {
                builder.Append(Base32[(int)(Next(ref state) >> 27)]);
            }

            builder.Append(" was used.\n");
        }

        return (builder.ToString(), keys);
    }

    /// <summary>
    /// Question and answer pairs, each followed by a long run of lines that hold only spaces and tabs: one security
    /// question finding per pair, located at the answer.
    /// </summary>
    public static (string Text, IReadOnlyList<(int Start, int Length)> Answers) QuestionsBetweenBlankLines(int length, int pairs)
    {
        var builder = new StringBuilder(length + 64);
        var answers = new List<(int Start, int Length)>(pairs);
        uint state = 3;
        int blockLength = length / pairs;
        for (int pair = 0; pair < pairs; pair++)
        {
            builder.Append(CultureInfo.InvariantCulture, $"Which door code opens room {pair + 100_000}?\n");
            string answer = string.Create(CultureInfo.InvariantCulture, $"Code {pair + 200_000}");
            answers.Add((builder.Length, answer.Length));
            builder.Append(answer).Append('\n');
            int blockEnd = builder.Length + blockLength - 64;
            while (builder.Length < blockEnd)
            {
                builder.Append((Next(ref state) >> 30) switch
                {
                    0 => "\n",
                    1 => " \n",
                    2 => "\t\n",
                    _ => " \t\n",
                });
            }
        }

        return (builder.ToString(), answers);
    }

    private static uint Next(ref uint state)
    {
        state = unchecked((state * 1_664_525) + 1_013_904_223);
        return state;
    }
}
