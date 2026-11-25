using System.CommandLine;

namespace SharedKernel.CI.Tool;

internal static partial class RepositoryCommandLine
{
    private static class WorkflowLintCommand
    {
        public static Command Create(string workingDirectory, CiMachine machine)
        {
            var rootOption = CreateRootOption(workingDirectory);
            var baseOption = new Option<string>("--base") { Description = "Full Git object ID for the base of the comparison.", HelpName = "object-id" };
            var headOption = new Option<string>("--head") { Description = "Full Git object ID for the proposed head.", HelpName = "object-id" };
            var actionlintOption = PathOption("--actionlint", "Actionlint executable name or path relative to the root. Defaults to actionlint on PATH.", "executable");
            actionlintOption.DefaultValueFactory = static _ => "actionlint";
            var workflowsOption = PathOption("--workflows", "Workflow directory relative to the root. Defaults to .github/workflows.", "directory");
            var reportOption = PathOption("--report", "Optional JSON evidence path relative to the root.", "path");
            var command = new Command("verify-workflows", "Lint workflows changed between two Git revisions, or lint the full workspace when no comparison is verified.")
            {
                rootOption,
                baseOption,
                headOption,
                actionlintOption,
                workflowsOption,
                reportOption,
            };
            command.SetAction((parseResult, ct) => Execute(
                parseResult,
                (output, _) => (parseResult.GetValue(workflowsOption) is { } workflows ? new WorkflowLint(machine, workflows) : new WorkflowLint(machine)).Run(
                    rootOption.GetExistingDirectory(parseResult),
                    parseResult.GetValue(baseOption),
                    parseResult.GetValue(headOption),
                    parseResult.GetRequiredValue(actionlintOption),
                    parseResult.GetValue(reportOption),
                    output,
                    ct),
                ct));
            return command;
        }
    }
}
