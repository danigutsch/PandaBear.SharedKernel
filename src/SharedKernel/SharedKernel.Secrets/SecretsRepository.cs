using PandaBear.InputNormalization;

namespace PandaBear.Secrets;

/// <summary>
/// The age and SOPS secrets of one repository: the creation rules in <c>.config/sharedkernel-secrets.json</c> under its
/// root and the encrypted files they cover. Every operation returns one of <see cref="SecretsExitCodes"/>, writes what it
/// reports with control characters escaped, and writes a refusal or failure to the error writer instead of throwing it.
/// Plaintext travels only through the streams that the settings open, never through an argument or a writer.
/// </summary>
public sealed class SecretsRepository
{
    private readonly string _root;
    private readonly SecretsPrograms _programs;

    /// <summary>Creates the repository.</summary>
    /// <param name="root">The absolute repository root; relative paths in settings resolve under it.</param>
    /// <param name="programs">The programs that the operations run.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="root"/> is not an absolute path.</exception>
    public SecretsRepository(string root, SecretsPrograms programs)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(programs);
        if (!Path.IsPathFullyQualified(root))
        {
            throw new ArgumentException("The root must be an absolute path.", nameof(root));
        }

        _root = root;
        _programs = programs;
    }

    /// <summary>
    /// Creates an age identity file with <c>age-keygen</c>, which writes it with mode 0600, and reports only the public
    /// recipient. It never overwrites a file, and without <see cref="CreateIdentitySettings.Force"/> it only previews.
    /// </summary>
    /// <param name="settings">The identity file to create.</param>
    /// <param name="output">Receives the preview or the public recipient.</param>
    /// <param name="error">Receives a refusal or failure.</param>
    /// <param name="cancellationToken">Cancels the operation and ends the program.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task<int> CreateIdentity(CreateIdentitySettings settings, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(output);
        return Guard(error, () => IdentityOperation.Run(_programs, settings, output, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Encrypts plaintext to exactly the recipients of the first creation rule that matches the new file, creates the file,
    /// and reads it back to check its recipients. It never overwrites a file, and without
    /// <see cref="EncryptSettings.Force"/> it only previews and reads no plaintext.
    /// </summary>
    /// <param name="settings">The new file and the plaintext source.</param>
    /// <param name="output">Receives the preview or the result.</param>
    /// <param name="error">Receives a refusal or failure.</param>
    /// <param name="cancellationToken">Cancels the operation and ends the program.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task<int> Encrypt(EncryptSettings settings, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(output);
        return Guard(error, () => EncryptOperation.Run(_root, _programs, settings, output, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Decrypts a file or one value and writes the plaintext, unescaped, to <see cref="DecryptSettings.OpenPlaintextOutput"/>,
    /// or to a new owner-only file at <see cref="DecryptSettings.OutputPath"/> through a temporary file and a rename.
    /// </summary>
    /// <param name="settings">The target, the identity file, and the destination.</param>
    /// <param name="error">Receives a refusal or failure.</param>
    /// <param name="cancellationToken">Cancels the operation and ends the program.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task<int> Decrypt(DecryptSettings settings, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Guard(error, () => DecryptOperation.Run(_root, _programs, settings, cancellationToken), cancellationToken);
    }

    /// <summary>Lists the age recipients, any other key holder, and the concerns of a file, from its metadata.</summary>
    /// <param name="file">The encrypted YAML or JSON file, relative to the root.</param>
    /// <param name="output">Receives one line per recipient, key holder, and concern.</param>
    /// <param name="error">Receives a refusal or failure.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task<int> ListRecipients(string file, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(output);
        return Guard(error, () => RecipientsOperation.List(_root, file, output, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Compares a file's labels with the recipients of its creation rule and, for each identity in the settings, proves by a
    /// trial decryption that it can, or must not, decrypt the file. Labels alone are not proof of who can decrypt.
    /// </summary>
    /// <param name="settings">The file and the identities to prove.</param>
    /// <param name="output">Receives the differences, the proof results, and a note on what labels prove.</param>
    /// <param name="error">Receives a refusal or failure.</param>
    /// <param name="cancellationToken">Cancels the operation and ends the program.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task<int> CheckRecipients(CheckRecipientsSettings settings, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(output);
        return Guard(error, () => RecipientsOperation.Check(_root, _programs, settings, output, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Checks every SOPS-encrypted file under the root against the first creation rule whose expression matches it, and
    /// rejects every other file in a rule's scope. It proves labels and structure, not who can decrypt; with
    /// <see cref="VerifySettings.BaseRevision"/> it also reports every label whose <c>enc</c> changed since that revision.
    /// </summary>
    /// <param name="settings">What the run accepts.</param>
    /// <param name="output">Receives the result of each file and a summary.</param>
    /// <param name="error">Receives what cannot be judged, a refusal, or a failure.</param>
    /// <param name="cancellationToken">Cancels the operation and ends git.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task<int> Verify(VerifySettings settings, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        return Guard(error, () => VerifyOperation.Run(_root, _programs, settings, output, error, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Makes a file's recipients exactly those of its creation rule while keeping its data key, as <c>sops updatekeys</c>
    /// does. To change who can read a file, change its rule in the configuration and run this. A removed recipient may
    /// already know the data key; <see cref="Rotate"/> replaces it. Without <see cref="UpdateRecipientsSettings.Force"/> it
    /// only previews.
    /// </summary>
    /// <param name="settings">The file and the identity that can decrypt it.</param>
    /// <param name="output">Receives the recipients that are added and removed, and the result.</param>
    /// <param name="error">Receives a refusal or failure.</param>
    /// <param name="cancellationToken">Cancels the operation and ends sops.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task<int> UpdateRecipients(UpdateRecipientsSettings settings, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(output);
        return Guard(error, () => RewriteOperation.UpdateRecipients(_root, _programs, settings, output, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Encrypts a file again with a new data key for exactly the recipients of its creation rule, as <c>sops rotate</c>
    /// does, so that a removed recipient's copy of the old data key no longer reads it. Without
    /// <see cref="RotateSettings.Force"/> it only previews.
    /// </summary>
    /// <param name="settings">The file and the identity that can decrypt it.</param>
    /// <param name="output">Receives the recipients that are added and removed, and the result.</param>
    /// <param name="error">Receives a refusal or failure.</param>
    /// <param name="cancellationToken">Cancels the operation and ends sops.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task<int> Rotate(RotateSettings settings, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(output);
        return Guard(error, () => RewriteOperation.Rotate(_root, _programs, settings, output, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Lets a person edit a file's decrypted document in their editor through <c>sops edit</c>, which keeps the data key. The
    /// plaintext exists only in a new owner-only directory under <see cref="EditSettings.TemporaryRoot"/>, which must be on
    /// a memory-backed file system, and that directory is removed afterwards. The file must carry exactly its rule's
    /// recipients before and after.
    /// </summary>
    /// <param name="settings">The file, the identity, the editor, and the temporary root.</param>
    /// <param name="output">Receives whether the file was saved or unchanged.</param>
    /// <param name="error">Receives a refusal or failure.</param>
    /// <param name="cancellationToken">Cancels the operation and ends sops and the editor.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Task<int> Edit(EditSettings settings, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(output);
        return Guard(error, () => RewriteOperation.Edit(_root, _programs, settings, output, cancellationToken), cancellationToken);
    }

    private static async Task<int> Guard(TextWriter error, Func<Task<int>> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            foreach (var line in ControlCharacters.EscapeLines(exception.Message))
            {
                await error.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            return SecretsExitCodes.Failure;
        }
    }
}
