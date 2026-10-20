using SharedKernel.Versioning.Tool;

namespace SharedKernel.Repository.Tool;

internal static class RepositoryToolApplication
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args is [] or ["--help"] or ["-h"])
        {
            output.WriteLine("Usage: repository-tool select|validate [--root path] [--base revision] [--selection path] [--pack] [--version version] [--baseline-version version] [--aot]");
            return 0;
        }

        try
        {
            var root = Environment.CurrentDirectory;
            string? baseline = null, version = null, apiBaseline = null;
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
                    case "--base": baseline = value; break;
                    case "--selection": selection = value; break;
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
                        changes = CommandRunner.Run("git", ["diff", "--name-only", "--no-renames", baseline + "...HEAD", "--"], root)
                            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    }
                    catch (InvalidOperationException) { /* Missing history deliberately selects full validation. */ }
                }

                var result = ProjectSelector.Select(root, changes);
                Directory.CreateDirectory(directory);
                File.WriteAllLines(Path.Combine(directory, "build-projects.txt"), result.BuildProjects);
                File.WriteAllLines(Path.Combine(directory, "test-projects.txt"), result.TestProjects);
                File.WriteAllLines(Path.Combine(directory, "selection-reasons.txt"), result.Reasons);
                File.WriteAllText(Path.Combine(directory, "candidate-required.txt"), result.BuildProjects.Length > 0 ? "true" : "false");
                output.WriteLine(string.Join(Environment.NewLine, result.Reasons));
                return 0;
            }

            if (args[0] != "validate") { throw new ArgumentException("Unknown command: " + args[0]); }
            foreach (var project in File.ReadAllLines(Path.Combine(directory, "build-projects.txt")))
            {
                output.Write(CommandRunner.Run("dotnet", ["restore", project, "--locked-mode"], root));
                output.Write(CommandRunner.Run("dotnet", ["build", project, "-c", "Release", "--no-restore"], root));
            }

            foreach (var project in File.ReadAllLines(Path.Combine(directory, "test-projects.txt")))
            {
                output.Write(CommandRunner.Run("dotnet", ["test", "--project", project, "-c", "Release", "--no-build", "--no-restore"], root));
            }

            if (pack)
            {
                var candidateArgs = new List<string> { "run", "--project", "tools/SharedKernel.Versioning.Tool", "-c", "Release", "--no-restore", "--", "validate-candidate", "--repo-root", root };
                if (version is not null) { candidateArgs.AddRange(["--version", version]); }
                if (apiBaseline is not null) { candidateArgs.AddRange(["--baseline-version", apiBaseline]); }
                if (aot) { candidateArgs.Add("--aot"); }
                output.Write(CommandRunner.Run("dotnet", candidateArgs, root));
            }

            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException or System.Xml.XmlException)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
    }
}
