namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Pins the Node.js runtime and the Dev Container CLI package that <c>verify-devcontainer</c> downloads.
/// </summary>
/// <param name="NodeVersion">The Node.js version, without the <c>v</c> prefix.</param>
/// <param name="NodeArchive">The <c>linux-x64</c> <c>.tar.gz</c> archive of that version.</param>
/// <param name="NodeSha256">The lowercase hexadecimal SHA-256 of the archive.</param>
/// <param name="CliVersion">The <c>@devcontainers/cli</c> version.</param>
/// <param name="CliArchive">The npm tarball of that version.</param>
/// <param name="CliIntegrity">The npm <c>dist.integrity</c> of the tarball, as <c>sha512-&lt;base64&gt;</c>.</param>
internal sealed record DevcontainerCliPins(
    string NodeVersion,
    Uri NodeArchive,
    string NodeSha256,
    string CliVersion,
    Uri CliArchive,
    string CliIntegrity)
{
    /// <summary>
    /// Gets the released pins. The Node.js hash is the one in the release's <c>SHASUMS256.txt.asc</c>, signed by a
    /// Node.js release key; the CLI integrity is the registry's <c>dist.integrity</c> for the version.
    /// </summary>
    public static DevcontainerCliPins Default
    {
        get;
    } = new(
        "24.22.1",
        new Uri("https://nodejs.org/dist/v24.22.1/node-v24.22.1-linux-x64.tar.gz"),
        "eb6728e77c576a35162c122c45ccadf224d89ff00abc8dae46cc2897c6002d7c",
        "0.84.1",
        new Uri("https://registry.npmjs.org/@devcontainers/cli/-/cli-0.84.1.tgz"),
        "sha512-r+JR/4R8lznPQNwLyHPIzHJ1mj3p2l5lGyHeq2FetEfpe6s6BVLE9mFl7MxQI4wKNqfWCIO7DSokoCWRlzQSIg==");
}
