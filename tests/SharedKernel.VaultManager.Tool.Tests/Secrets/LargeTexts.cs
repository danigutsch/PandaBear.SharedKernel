using System.Globalization;
using System.Text;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>
/// Builds large synthetic attachment texts of a given length that stress the scanner, with a known number of findings.
/// Values come from a fixed linear congruential sequence, so every run sees the same text and nothing in the repository
/// is secret-shaped.
/// </summary>
internal static class LargeTexts
{
    private const string Base32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>Question lines, each followed by its answer line: one security question finding per pair.</summary>
    public static (string Text, int Findings) Questions(int length)
    {
        var builder = new StringBuilder(length + 64);
        uint state = 1;
        int pairs = 0;
        while (builder.Length < length - 64)
        {
            builder.Append(CultureInfo.InvariantCulture, $"Which door code opens room {Next(ref state) % 900_000 + 100_000}?\n");
            builder.Append(CultureInfo.InvariantCulture, $"Code {Next(ref state) % 900_000 + 100_000}\n");
            pairs++;
        }

        return (builder.ToString(), pairs);
    }

    /// <summary>Log lines that each name one AWS access key ID: one finding per line.</summary>
    public static (string Text, int Findings) AccessKeyLog(int length)
    {
        var builder = new StringBuilder(length + 64);
        uint state = 2;
        int lines = 0;
        while (builder.Length < length - 64)
        {
            builder.Append("access key AKIA");
            for (int index = 0; index < 16; index++)
            {
                builder.Append(Base32[(int)(Next(ref state) >> 27)]);
            }

            builder.Append(" was used.\n");
            lines++;
        }

        return (builder.ToString(), lines);
    }

    /// <summary>
    /// Question and answer pairs, each followed by a long run of lines that hold only spaces and tabs: one security
    /// question finding per pair.
    /// </summary>
    public static (string Text, int Findings) QuestionsBetweenBlankLines(int length, int pairs)
    {
        var builder = new StringBuilder(length + 64);
        uint state = 3;
        int blockLength = length / pairs;
        for (int pair = 0; pair < pairs; pair++)
        {
            builder.Append(CultureInfo.InvariantCulture, $"Which door code opens room {pair + 100_000}?\nCode {pair + 200_000}\n");
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

        return (builder.ToString(), pairs);
    }

    private static uint Next(ref uint state)
    {
        state = unchecked((state * 1_664_525) + 1_013_904_223);
        return state;
    }
}
