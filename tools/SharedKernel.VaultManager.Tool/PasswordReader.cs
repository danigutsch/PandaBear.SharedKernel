using System.Text;

namespace PandaBear.VaultManager.Tool;

/// <summary>Reads passwords from the terminal without echoing them.</summary>
/// <param name="readKey">Reads one key press without displaying it.</param>
/// <param name="output">Receives prompts and messages; never the password.</param>
internal sealed class PasswordReader(Func<ConsoleKeyInfo> readKey, TextWriter output)
{
    /// <summary>Minimum length of a password that protects a new export file.</summary>
    public const int MinimumNewPasswordLength = 12;

    private const int Attempts = 3;

    /// <summary>Creates a reader for the interactive console. Prompts go to standard error.</summary>
    /// <exception cref="InvalidOperationException">Standard input is redirected.</exception>
    public static PasswordReader FromConsole()
    {
        if (Console.IsInputRedirected)
        {
            throw new InvalidOperationException("Password prompts need an interactive terminal; standard input is redirected.");
        }

        return new PasswordReader(() => Console.ReadKey(intercept: true), Console.Error);
    }

    /// <summary>Prompts once and returns what was typed.</summary>
    public string Read(string prompt)
    {
        output.Write($"{prompt}: ");
        var buffer = new StringBuilder();
        while (true)
        {
            ConsoleKeyInfo key = readKey();
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
            }
        }

        output.WriteLine();
        return buffer.ToString();
    }

    /// <summary>Prompts for a new password twice until both entries match and it is long enough.</summary>
    /// <exception cref="InvalidOperationException">No acceptable password was entered after three attempts.</exception>
    public string ReadNew(string prompt)
    {
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            string first = Read(prompt);
            if (first.Length < MinimumNewPasswordLength)
            {
                output.WriteLine($"Use at least {MinimumNewPasswordLength} characters; this password protects every secret in the file.");
                continue;
            }

            if (string.Equals(first, Read($"{prompt} (again)"), StringComparison.Ordinal))
            {
                return first;
            }

            output.WriteLine("The passwords do not match.");
        }

        throw new InvalidOperationException("No matching password was entered.");
    }
}
