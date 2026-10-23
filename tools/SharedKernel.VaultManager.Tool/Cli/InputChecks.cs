using PandaBear.VaultManager.Tool.Formats;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>Checks the input files before a command asks for a password or reads any vault.</summary>
internal static class InputChecks
{
    /// <summary>
    /// Detects the format of each input, so an input that cannot be read is named before any password is asked. Writes one
    /// line per input that cannot be read to <paramref name="error"/>.
    /// </summary>
    /// <param name="inputs">Input files.</param>
    /// <param name="error">Receives the problems, each starting with the input file name.</param>
    /// <returns>The format of each input, or <see langword="null"/> for an input that cannot be read.</returns>
    public static VaultFileFormat?[] Detect(IReadOnlyList<string> inputs, TextWriter error)
    {
        var formats = new VaultFileFormat?[inputs.Count];
        for (int index = 0; index < inputs.Count; index++)
        {
            string input = inputs[index];
            try
            {
                VaultFileFormat format = FormatDetector.Detect(input);
                if (format is VaultFileFormat.Unknown or VaultFileFormat.BitwardenJsonAccountRestricted)
                {
                    // The reader rejects these formats with the explanation the user needs; no password is asked for them.
                    _ = VaultFileReader.Read(input, static _ => string.Empty);
                }

                formats[index] = format;
            }
            catch (Exception exception) when (CommandAction.IsFileProblem(exception))
            {
                error.WriteLine(CommandText.OneLine($"{Path.GetFileName(input)}: {exception.Message}"));
            }
        }

        return formats;
    }

    /// <summary>
    /// Checks that the password of every password-protected input can be asked: standard input must be a terminal. Writes
    /// one line per input that needs a password otherwise.
    /// </summary>
    /// <param name="inputs">Input files.</param>
    /// <param name="formats">Detected formats, aligned with <paramref name="inputs"/>.</param>
    /// <param name="isInteractive">Whether standard input is a terminal.</param>
    /// <param name="error">Receives the problems.</param>
    /// <returns><see langword="true"/> when every password the inputs need can be asked.</returns>
    public static bool PasswordsCanBeAsked(IReadOnlyList<string> inputs, IReadOnlyList<VaultFileFormat?> formats, bool isInteractive, TextWriter error)
    {
        if (isInteractive)
        {
            return true;
        }

        bool canAsk = true;
        for (int index = 0; index < inputs.Count; index++)
        {
            if (formats[index] == VaultFileFormat.BitwardenJsonPasswordProtected)
            {
                error.WriteLine(CommandText.OneLine($"{Path.GetFileName(inputs[index])} is password-protected, and standard input is not a terminal to type its password in. Run the command in a terminal."));
                canAsk = false;
            }
        }

        return canAsk;
    }
}
