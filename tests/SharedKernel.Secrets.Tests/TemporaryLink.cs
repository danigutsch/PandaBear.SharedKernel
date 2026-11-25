namespace SharedKernel.Secrets.Tests;

/// <summary>A symbolic link to a directory, removed on disposal.</summary>
internal sealed class TemporaryLink : IDisposable
{
    /// <summary>Creates a link under <paramref name="parent"/> that points at <paramref name="target"/>.</summary>
    public TemporaryLink(string parent, string target)
    {
        Path = System.IO.Path.Combine(parent, "sharedkernel-secrets-link-" + Guid.NewGuid().ToString("N"));
        Directory.CreateSymbolicLink(Path, target);
    }

    /// <summary>Gets the link's path.</summary>
    public string Path
    {
        get;
    }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(Path);
}
