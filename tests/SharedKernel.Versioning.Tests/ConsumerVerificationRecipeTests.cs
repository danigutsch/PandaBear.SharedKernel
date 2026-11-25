using SharedKernel.Processes;

namespace SharedKernel.Versioning.Tests;

/// <summary>Guards the explicitly selected ordered consumer validation recipe.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public sealed class ConsumerVerificationRecipeTests
{
    private const string Recipe = "consumer-recipe.json";

    [Fact]
    public async Task A_recipe_runs_literal_arguments_in_order_in_the_same_clean_clone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.WriteFile(Recipe, """
            [
              ["git", "config", "--local", "--add", "test.recipe", "first value with \"quotes\" and ; literal"],
              ["git", "config", "--local", "--add", "test.recipe", ""],
              ["git", "config", "--local", "--get-all", "test.recipe"],
              ["git", "rev-parse", "--show-toplevel"],
              ["git", "rev-parse", "HEAD"]
            ]
            """);
        var revision = await scope.Commit(ct);

        // Act
        var result = await scope.VerifyRecipe(Recipe, ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var scratch = ConsumerVerificationScope.ScratchDirectory(result.Error);
        result.Error.ReplaceLineEndings("\n").ShouldContain("first value with \"quotes\" and ; literal\n\n", StringComparison.Ordinal);
        result.Error.ShouldContain(revision, StringComparison.Ordinal);
        Directory.Exists(scratch).ShouldBeFalse();
        scope.Evidence["consumerCompatibility"]!.GetValue<string>().ShouldBe("passed");
    }

    [Theory]
    [InlineData("/mnt/read only/images=cache")]
    [InlineData(@"C:\Program Files\candidate=cache\images")]
    [InlineData("-literal value=a=b")]
    [InlineData("")]
    public async Task Named_inputs_supply_one_argument_and_leave_literal_strings_untouched(string value)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.WriteFile(Recipe, """
            [
              ["git", "config", "--local", "--add", "--", "test.recipe", {"input":"host-path"}],
              ["git", "config", "--local", "--add", "--", "test.recipe", "${host-path}"],
              ["git", "config", "--local", "--get-all", "test.recipe"],
              ["git", "rev-parse", "--show-toplevel"],
              ["git", "rev-parse", "HEAD"]
            ]
            """);
        var revision = await scope.Commit(ct);

        // Act
        var result = await scope.VerifyRecipe(Recipe, ct, "--recipe-input", "host-path=" + value);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        result.Error.ReplaceLineEndings("\n").ShouldContain(value + "\n${host-path}\n", StringComparison.Ordinal);
        result.Error.ShouldContain(revision, StringComparison.Ordinal);
        Directory.Exists(ConsumerVerificationScope.ScratchDirectory(result.Error)).ShouldBeFalse();
        scope.Evidence["consumerCompatibility"]!.GetValue<string>().ShouldBe("passed");
    }

    [Fact]
    public async Task Repeated_references_can_write_a_caller_owned_report_that_survives_cleanup()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        var imageStore = Path.Combine(scope.Root, "read-only images=cache");
        var report = Path.Combine(Path.GetDirectoryName(scope.Manifest)!, "retained report=runner.ini");
        scope.WriteFile(Recipe, """
            [
              ["git", "config", "--file", {"input":"report"}, "--add", "--", "verification.image-store", {"input":"images2"}],
              ["git", "config", "--file", {"input":"report"}, "--add", "--", "verification.status", "finished"],
              ["git", "rev-parse", "--show-toplevel"]
            ]
            """);
        await scope.Commit(ct);

        // Act
        var result = await scope.VerifyRecipe(Recipe, ct, "--recipe-input", "images2=" + imageStore, "--recipe-input", "report=" + report);
        var reportedPath = await SystemCommands.Instance.Run("git", ["config", "--file", report, "--get", "verification.image-store"], scope.Root, ct);
        var status = await SystemCommands.Instance.Run("git", ["config", "--file", report, "--get", "verification.status"], scope.Root, ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        reportedPath.TrimEnd('\r', '\n').ShouldBe(imageStore);
        status.TrimEnd('\r', '\n').ShouldBe("finished");
        Directory.Exists(ConsumerVerificationScope.ScratchDirectory(result.Error)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_failed_step_keeps_the_caller_value_out_of_verifier_diagnostics()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        const string value = "protected fixture value=with spaces";
        scope.WriteFile(Recipe, """
            [
              ["git", "rev-parse", "--show-toplevel"],
              ["git", "check-ref-format", {"input":"ref-name"}],
              ["git", "rev-parse", "HEAD"]
            ]
            """);
        var revision = await scope.Commit(ct);

        // Act
        var result = await scope.VerifyRecipe(Recipe, ct, "--recipe-input", "ref-name=" + value);

        // Assert
        scope.AssertRejected(result, "Recipe step 2 failed: Command failed (1): git");
        result.Error.ShouldNotContain(value, StringComparison.Ordinal);
        result.Error.ShouldNotContain(revision, StringComparison.Ordinal);
        scope.Evidence.ToJsonString().ShouldNotContain(value, StringComparison.Ordinal);
        Directory.Exists(ConsumerVerificationScope.ScratchDirectory(result.Error)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, null, "missing recipe input 'host-path'")]
    [InlineData("host-path=private first", "host-path=private second", "Duplicate recipe input 'host-path'")]
    [InlineData("host-path=private first", "extra=private second", "Unused recipe input 'extra'")]
    [InlineData("Bad=name=private first", null, "Recipe inputs must use name=value")]
    [InlineData("host-path=private first\0value", null, "must not contain null characters")]
    public async Task Invalid_bindings_stop_before_the_first_recipe_step(string? first, string? second, string reason)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.WriteFile(Recipe, """
            [
              ["git", "rev-parse", "--show-toplevel"],
              ["git", "check-ref-format", {"input":"host-path"}]
            ]
            """);
        await scope.Commit(ct);
        var options = new[] { first, second }.OfType<string>().SelectMany(static binding => new[] { "--recipe-input", binding }).ToArray();

        // Act
        var result = await scope.VerifyRecipe(Recipe, ct, options);

        // Assert
        scope.AssertRejected(result, reason);
        result.Error.ShouldNotContain("/consumer\n", StringComparison.Ordinal);
        result.Error.ShouldNotContain("private first", StringComparison.Ordinal);
        result.Error.ShouldNotContain("private second", StringComparison.Ordinal);
        scope.Evidence.ToJsonString().ShouldNotContain("private ", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_recipe_reports_the_failed_step_stops_and_cleans_up()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.WriteFile(Recipe, """
            [
              ["git", "rev-parse", "--show-toplevel"],
              ["git", "config", "--local", "--get", "test.missing"],
              ["git", "rev-parse", "HEAD"]
            ]
            """);
        var revision = await scope.Commit(ct);

        // Act
        var result = await scope.VerifyRecipe(Recipe, ct);

        // Assert
        result.ExitCode.ShouldBe(2, result.Error);
        result.Error.ShouldNotContain(revision, StringComparison.Ordinal);
        Directory.Exists(ConsumerVerificationScope.ScratchDirectory(result.Error)).ShouldBeFalse();
        scope.Evidence["consumerCompatibility"]!.GetValue<string>().ShouldBe("failed");
        scope.Evidence["reason"]!.GetValue<string>().ShouldStartWith("Recipe step 2 failed: Command failed (1): git config", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}", "must be a nonempty array of commands")]
    [InlineData("[]", "must be a nonempty array of commands")]
    [InlineData("[[\"git\",\"rev-parse\",\"--show-toplevel\"],[]]", "Recipe step 2 must be a nonempty array of strings")]
    [InlineData("[[\"git\",true]]", "Recipe step 1 must contain only strings")]
    [InlineData("[[\"git\",null]]", "Recipe step 1 must contain only strings")]
    [InlineData("[[\"git\",\"x\\u0000y\"]]", "Recipe step 1 must contain only strings without null characters")]
    [InlineData("[[\" \" ]]", "Recipe step 1 needs an executable name")]
    [InlineData("[[{\"input\":\"command\"}]]", "Recipe step 1 must have a literal executable")]
    [InlineData("[[\"git\",{}]]", "Recipe step 1 has an invalid input reference")]
    [InlineData("[[\"git\",{\"input\":true}]]", "Recipe step 1 has an invalid input reference")]
    [InlineData("[[\"git\",{\"input\":\"\"}]]", "Recipe step 1 has an invalid input reference")]
    [InlineData("[[\"git\",{\"input\":\"bad_name\"}]]", "Recipe step 1 has an invalid input reference")]
    [InlineData("[[\"git\",{\"input\":\"Bad Name\"}]]", "Recipe step 1 has an invalid input reference")]
    [InlineData("[[\"git\",{\"input\":\"a\",\"input\":\"b\"}]]", "Recipe step 1 has an invalid input reference")]
    public async Task Invalid_recipes_never_run_any_step(string recipe, string reason)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.WriteFile(Recipe, recipe);
        await scope.Commit(ct);

        // Act
        var result = await scope.VerifyRecipe(Recipe, ct);

        // Assert
        scope.AssertRejected(result, reason);
        result.Error.ShouldNotContain("/consumer\n", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_recipe_outside_the_consumer_repository_is_rejected()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        await scope.Commit(ct);

        // Act
        var result = await scope.VerifyRecipe(scope.Manifest, ct);

        // Assert
        scope.AssertRejected(result, "--recipe must name a file inside the consumer repository");
    }

    [Fact]
    public async Task An_ignored_recipe_in_the_working_copy_is_unavailable_in_the_clone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        const string ignored = ".local/ignored-recipe.json";
        scope.WriteFile(ignored, """[["git","rev-parse","--show-toplevel"]]""");
        await scope.Commit(ct);

        // Act
        var result = await scope.VerifyRecipe(ignored, ct);

        // Assert
        scope.AssertRejected(result, "ignored-recipe.json");
        scope.Evidence["consumerDirty"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task An_oversize_recipe_fails_before_execution()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        scope.WriteFile(Recipe, new string(' ', (1024 * 1024) + 1));
        await scope.Commit(ct);

        // Act
        var result = await scope.VerifyRecipe(Recipe, ct);

        // Assert
        scope.AssertRejected(result, "recipe exceeds the 1 MiB configuration size limit");
    }

    [Fact]
    public async Task Canceling_a_recipe_prevents_later_steps_and_evidence_and_cleans_up()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new ConsumerVerificationScope();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        scope.WriteFile(Recipe, """
            [
              ["git", "rev-parse", "--show-toplevel"],
              ["git", "config", "--local", "test.after-cancel", "ran"]
            ]
            """);
        await scope.Commit(ct);
        var system = new SystemProcessRunner();
        var started = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new ScriptedProcessRunner(async (start, forward, token) =>
        {
            if (start.ArgumentList.SequenceEqual(["rev-parse", "--show-toplevel"]))
            {
                started.SetResult(start.WorkingDirectory);
                await blocked.Task.WaitAsync(token);
            }

            return await system.Run(start, forward, token);
        });
        var verifier = new ConsumerVerifier(runner, PathExecutableResolver.Default);
        var options = new ConsumerVerificationOptions(scope.Root, scope.Manifest, null, scope.EvidencePath, []) { Recipe = Recipe };

        // Act
        var verification = verifier.Verify(options, TextWriter.Null, TextWriter.Null, cancellation.Token);
        var first = await Task.WhenAny(started.Task, verification).WaitAsync(ct);
        first.ShouldBeSameAs(started.Task);
        var clone = await started.Task;
        await cancellation.CancelAsync();

        // Assert
        Func<Task> observe = () => verification;
        await observe.ShouldThrowAssignableTo<OperationCanceledException>();
        runner.Runs.SelectMany(static run => run.StartInfo.ArgumentList).ShouldNotContain("test.after-cancel");
        File.Exists(scope.EvidencePath).ShouldBeFalse();
        Directory.Exists(Path.GetDirectoryName(clone)!).ShouldBeFalse();
    }
}
