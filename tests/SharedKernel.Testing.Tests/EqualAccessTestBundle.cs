using System.Security.Cryptography;
using System.Text;

namespace SharedKernel.Testing.Tests;

/// <summary>Owns a synthetic Equal Access browser bundle for one test.</summary>
internal sealed class EqualAccessTestBundle : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("sk-equal-access-").FullName;

    /// <summary>Writes the supplied engine double to its isolated directory.</summary>
    /// <param name="script">Synthetic browser engine content.</param>
    internal EqualAccessTestBundle(string script)
    {
        Path = System.IO.Path.Combine(directory, "ace.js");
        File.WriteAllText(Path, script);
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
    }

    /// <summary>Gets the local engine double.</summary>
    internal string Path { get; }

    /// <summary>Gets the digest of the local engine double.</summary>
    internal string Sha256 { get; }

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
