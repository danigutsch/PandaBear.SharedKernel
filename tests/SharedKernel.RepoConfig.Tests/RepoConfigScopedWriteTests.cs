using System.Net;

namespace PandaBear.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RepoConfigScopedWriteTests
{
    [Theory]
    [InlineData("root")]
    [InlineData("roadmap")]
    [InlineData("config")]
    [InlineData("dangling-config")]
    public void Initialize_rejects_linked_paths_before_creating_files(string location)
    {
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var outside = new TemporaryRepoConfigWorkspace();
        var root = workspace.RootPath;
        if (location == "root")
        {
            root = Path.Combine(workspace.RootPath, "linked");
            Directory.CreateSymbolicLink(root, outside.RootPath);
        }
        else if (location == "roadmap")
        {
            Directory.CreateSymbolicLink(Path.Combine(root, "roadmap"), outside.RootPath);
        }
        else
        {
            Directory.CreateDirectory(Path.Combine(root, "roadmap"));
            var target = Path.Combine(outside.RootPath, "target.json");
            if (location == "config")
            {
                File.WriteAllText(target, "original");
            }

            File.CreateSymbolicLink(Path.Combine(root, "roadmap/config.json"), target);
        }

        Action initialize = () => RepoConfigInitializer.Initialize(root);
        initialize.ShouldThrow<InvalidOperationException>();
        Directory.EnumerateFiles(outside.RootPath).Select(Path.GetFileName).ShouldBe(location == "config" ? ["target.json"] : []);
        if (location == "config")
        {
            File.ReadAllText(Path.Combine(outside.RootPath, "target.json")).ShouldBe("original");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Set_rejects_linked_config_or_parent_without_mutating_target(bool linkedParent)
    {
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var outside = new TemporaryRepoConfigWorkspace();
        RepoConfigInitializer.Initialize(outside.RootPath);
        var original = outside.ReadFile("roadmap/config.json");
        if (linkedParent)
        {
            Directory.CreateSymbolicLink(Path.Combine(workspace.RootPath, "roadmap"), Path.Combine(outside.RootPath, "roadmap"));
        }
        else
        {
            Directory.CreateDirectory(Path.Combine(workspace.RootPath, "roadmap"));
            File.CreateSymbolicLink(Path.Combine(workspace.RootPath, "roadmap/config.json"), Path.Combine(outside.RootPath, "roadmap/config.json"));
        }

        Action set = () => RepoConfigSetter.Set(workspace.RootPath, "github.repository", "new/repository");
        set.ShouldThrow<InvalidOperationException>();
        outside.ReadFile("roadmap/config.json").ShouldBe(original);
    }

    [Fact]
    public async Task Sync_rejects_linked_item_before_creating_remote_issue()
    {
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var outside = new TemporaryRepoConfigWorkspace();
        RepoConfigInitializer.Initialize(workspace.RootPath);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        const string itemPath = "roadmap/items/RM-001-roadmap-gitops.json";
        var content = workspace.ReadFile(itemPath).Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": \"create\" } },\n  \"labels\": [", StringComparison.Ordinal);
        workspace.WriteFile(itemPath, content);
        var project = RoadmapProject.Load(workspace.RootPath);
        outside.WriteFile("target.json", content);
        workspace.DeleteFile(itemPath);
        File.CreateSymbolicLink(Path.Combine(workspace.RootPath, itemPath), Path.Combine(outside.RootPath, "target.json"));
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Created, "{ \"number\": 1200 }");
        using var client = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, client);

        Func<Task> sync = () => syncer.Apply(TestContext.Current.CancellationToken);
        await sync.ShouldThrow<InvalidOperationException>();
        handler.Requests.ShouldBeEmpty();
        outside.ReadFile("target.json").ShouldBe(content);
    }

    [Fact]
    public async Task Sync_does_not_follow_predictable_temporary_mapping_link()
    {
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var outside = new TemporaryRepoConfigWorkspace();
        RepoConfigInitializer.Initialize(workspace.RootPath);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        const string itemPath = "roadmap/items/RM-001-roadmap-gitops.json";
        workspace.WriteFile(itemPath, workspace.ReadFile(itemPath).Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": \"create\" } },\n  \"labels\": [", StringComparison.Ordinal));
        outside.WriteFile("target.json", "outside-original");
        File.CreateSymbolicLink(Path.Combine(workspace.RootPath, itemPath) + ".tmp", Path.Combine(outside.RootPath, "target.json"));
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Created, "{ \"number\": 1200 }");
        handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"labels\": [] }");
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        using var client = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), client);

        await syncer.Apply(TestContext.Current.CancellationToken);

        outside.ReadFile("target.json").ShouldBe("outside-original");
        workspace.ReadFile(itemPath).ShouldContain("\"issue\": 1200", StringComparison.Ordinal);
    }


    [Fact]
    public async Task Sync_retains_recovery_lock_and_issue_number_when_destination_becomes_linked()
    {
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var outside = new TemporaryRepoConfigWorkspace();
        RepoConfigInitializer.Initialize(workspace.RootPath);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        const string itemPath = "roadmap/items/RM-001-roadmap-gitops.json";
        workspace.WriteFile(itemPath, workspace.ReadFile(itemPath).Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": \"create\" } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        outside.WriteFile("target.json", workspace.ReadFile(itemPath));
        var original = outside.ReadFile("target.json");
        using var handler = new BeforeResponseHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{ \"number\": 1200 }") },
            () =>
            {
                workspace.DeleteFile(itemPath);
                File.CreateSymbolicLink(Path.Combine(workspace.RootPath, itemPath), Path.Combine(outside.RootPath, "target.json"));
            });
        using var client = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, client);

        Func<Task> sync = () => syncer.Apply(TestContext.Current.CancellationToken);
        var exception = await sync.ShouldThrow<InvalidOperationException>();

        exception.Message.ShouldContain("GitHub issue #1200 was created", StringComparison.Ordinal);
        exception.InnerException.ShouldNotBeNull();
        File.Exists(Path.Combine(workspace.RootPath, itemPath) + ".lock").ShouldBeTrue();
        outside.ReadFile("target.json").ShouldBe(original);
    }

}
