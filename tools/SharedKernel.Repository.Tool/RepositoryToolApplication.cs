using System.Text;
using System.Text.Json;
using PandaBear.Versioning.Tool;

namespace PandaBear.Repository.Tool;

internal static class RepositoryToolApplication
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
        => Run(args, output, error, static (executable, arguments, root) => CommandRunner.Run(executable, arguments, root));

    internal static int Run(string[] args, TextWriter output, TextWriter error, Func<string, string[], string, string> run)
    {
        if (args is [] or ["--help"] or ["-h"])
        {
            output.WriteLine("Usage: repository-tool select|validate [--test-group all|ordinary|hosted-project-path] [--root path] [--base revision] [--selection path] [--github-output path] [--pack] [--version version] [--baseline-version version] [--aot]");
            return 0;
        }

        try
        {
            if (args is ["verify-pr-title", .. var titleArgs]) { return PrTitleCommand.Run(titleArgs); }
            var root = Environment.CurrentDirectory;
            string? baseline = null, version = null, apiBaseline = null, githubOutput = null;
            var testGroup = "all";
            var selection = "TestResults/selection";
            var pack = false;
            var aot = false;
            var index = 1;
            while (index < args.Length)
            {
                var option = args[index++];
                if (option == "--pack") { pack = true; continue; }
                if (option == "--aot") { aot = true; continue; }
                if (index == args.Length) { throw new ArgumentException("Missing value for " + option); }
                var value = args[index++];
                switch (option)
                {
                    case "--root": root = Path.GetFullPath(value); break;
                    case "--test-group": testGroup = value; break;
                    case "--base": baseline = value; break;
                    case "--selection": selection = value; break;
                    case "--github-output": githubOutput = value; break;
                    case "--version": version = value; break;
                    case "--baseline-version": apiBaseline = value; break;
                    default: throw new ArgumentException("Unknown option: " + option);
                }
            }

            var directory = Path.GetFullPath(selection, root);
            if (args[0] == "select")
            {
                string[]? changes = null;
                if (baseline is not null)
                {
                    try
                    {
                        changes = run("git", ["diff", "--name-only", "--no-renames", baseline + "...HEAD", "--"], root)
                            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    }
                    catch (InvalidOperationException) { /* Missing history deliberately selects full validation. */ }
                }

                var result = ProjectSelector.Select(root, changes);
                Directory.CreateDirectory(directory);
                var revision = "unavailable";
                try { revision = run("git", ["rev-parse", "HEAD"], root).Trim(); }
                catch (InvalidOperationException) { output.WriteLine("Selection revision unavailable; execution requires a Git checkout."); }
                File.WriteAllText(Path.Combine(directory, "revision.txt"), revision);
                File.WriteAllLines(Path.Combine(directory, "build-projects.txt"), result.BuildProjects);
                File.WriteAllLines(Path.Combine(directory, "test-projects.txt"), result.TestProjects);
                File.WriteAllLines(Path.Combine(directory, "selection-reasons.txt"), result.Reasons);
                using (var buffer = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
                    {
                        writer.WriteStartObject();
                        writer.WriteString("revision", revision);
                        writer.WriteString("comparisonBase", baseline);
                        writer.WriteStartArray("buildProjects");
                        foreach (var project in result.BuildProjects) { writer.WriteStringValue(project); }
                        writer.WriteEndArray();
                        writer.WriteStartArray("testProjects");
                        foreach (var project in result.TestProjects) { writer.WriteStringValue(project); }
                        writer.WriteEndArray();
                        writer.WriteStartArray("reasons");
                        foreach (var reason in result.Reasons) { writer.WriteStringValue(reason); }
                        writer.WriteEndArray();
                        writer.WriteEndObject();
                    }
                    File.WriteAllBytes(Path.Combine(directory, "selection.json"), buffer.ToArray());
                }
                var candidateRequired = result.BuildProjects.Length > 0 ? "true" : "false";
                File.WriteAllText(Path.Combine(directory, "candidate-required.txt"), candidateRequired);
                if (githubOutput is not null)
                {
                    using var buffer = new MemoryStream();
                    var hosted = result.TestProjects.Where(project => RepositoryValidation.IsHosted(root, project)).ToArray();
                    using (var writer = new Utf8JsonWriter(buffer))
                    {
                        writer.WriteStartArray();
                        foreach (var project in hosted)
                        {
                            writer.WriteStartObject();
                            writer.WriteString("project", project);
                            writer.WriteString("name", Path.GetFileNameWithoutExtension(project));
                            writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                    }
                    File.AppendAllText(Path.GetFullPath(githubOutput, root), "candidate_required=" + candidateRequired + Environment.NewLine
                        + "hosted_required=" + (hosted.Length > 0 ? "true" : "false") + Environment.NewLine
                        + "hosted_matrix=" + Encoding.UTF8.GetString(buffer.ToArray()) + Environment.NewLine);
                }
                output.WriteLine(string.Join(Environment.NewLine, result.Reasons));
                return 0;
            }

            if (args[0] != "validate") { throw new ArgumentException("Unknown command: " + args[0]); }
            RepositoryValidation.Run(root, directory, testGroup, run, output);

            if (pack)
            {
                var candidateArgs = new List<string> { "run", "--project", "tools/SharedKernel.Versioning.Tool", "-c", "Release", "--no-restore", "--", "validate-candidate", "--repo-root", root };
                if (version is not null) { candidateArgs.AddRange(["--version", version]); }
                if (apiBaseline is not null) { candidateArgs.AddRange(["--baseline-version", apiBaseline]); }
                if (aot) { candidateArgs.Add("--aot"); }
                output.Write(run("dotnet", candidateArgs.ToArray(), root));
            }

            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException or System.Xml.XmlException or JsonException or KeyNotFoundException)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
    }
}
