namespace SharedKernel.Repository.Tool;

internal sealed record ProjectSelection(string[] BuildProjects, string[] TestProjects, string[] Reasons);
