namespace PandaBear.Repository.Tests;

/// <summary>A CI tool command line found in a workflow or README file.</summary>
/// <param name="File">The repository-relative path, with forward slashes, of the file that holds the command line.</param>
/// <param name="Arguments">The arguments that follow the tool name, with sample values for the known shell variables.</param>
internal sealed record RepositoryToolCaller(string File, string[] Arguments);
