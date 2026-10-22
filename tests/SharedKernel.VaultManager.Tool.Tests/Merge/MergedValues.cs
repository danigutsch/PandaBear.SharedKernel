using PandaBear.VaultManager.Tool.Model;

namespace PandaBear.VaultManager.Tool.Tests.Merge;

/// <summary>Lists the values an item carries, wherever they are stored.</summary>
internal static class MergedValues
{
    /// <summary>Every non-blank value of an input item that a merge must keep.</summary>
    public static IEnumerable<string> Inputs(VaultItem item) => All(item).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!);

    /// <summary>Every value of a merged item, including field values, history, notes, and conversion notes.</summary>
    public static IReadOnlyList<string> Collect(VaultItem item) => [.. All(item).Concat(item.ConversionNotes).OfType<string>()];

    private static IEnumerable<string?> All(VaultItem item)
    {
        yield return item.Name;
        yield return item.FolderPath;
        yield return item.Notes;
        foreach (var field in item.Fields)
        {
            yield return field.Value;
        }

        foreach (var entry in item.PasswordHistory)
        {
            yield return entry.Password;
        }

        if (item.Login is { } login)
        {
            yield return login.Username;
            yield return login.Password;
            yield return login.Totp;
            foreach (var uri in login.Uris)
            {
                yield return uri.Value;
            }

            foreach (var passkey in login.Fido2Credentials)
            {
                yield return passkey.CredentialId;
            }
        }
    }
}
