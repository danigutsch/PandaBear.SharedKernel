namespace PandaBear.HttpClients.Tests;

public sealed class IdempotentSubmissionTests
{
    [Theory]
    [InlineData(HttpOutcomeKind.Succeeded, false)]
    [InlineData(HttpOutcomeKind.ValidationProblem, false)]
    [InlineData(HttpOutcomeKind.NotFound, false)]
    [InlineData(HttpOutcomeKind.Unauthorized, false)]
    [InlineData(HttpOutcomeKind.Forbidden, false)]
    [InlineData(HttpOutcomeKind.Conflict, false)]
    [InlineData(HttpOutcomeKind.EmptyBody, true)]
    [InlineData(HttpOutcomeKind.MalformedBody, true)]
    [InlineData(HttpOutcomeKind.UnexpectedStatus, true)]
    [InlineData(HttpOutcomeKind.UnsupportedContentType, true)]
    [InlineData(HttpOutcomeKind.TimedOut, true)]
    [InlineData(HttpOutcomeKind.TransportFailure, true)]
    public async Task Only_definitive_outcomes_release_the_retained_command(HttpOutcomeKind kind, bool pending)
    {
        // Arrange
        var submission = new IdempotentSubmission<string>();
        var keys = new List<string>();

        // Act
        var first = await submission.Submit("save", (key, _) =>
        {
            keys.Add(key);
            return Task.FromResult(new HttpOutcome<string> { Kind = kind });
        }, TestContext.Current.CancellationToken);

        // Assert
        first.ShouldNotBeNull().Kind.ShouldBe(kind);
        submission.HasPending.ShouldBe(pending);
        submission.IsBusy.ShouldBeFalse();
        submission.Operation.ShouldBe("save");
        var retry = await submission.Retry(TestContext.Current.CancellationToken);
        if (pending)
        {
            retry.ShouldNotBeNull();
            keys.Count.ShouldBe(2);
            keys[1].ShouldBe(keys[0]);
        }
        else
        {
            retry.ShouldBeNull();
            keys.Count.ShouldBe(1);
        }
    }

    [Fact]
    public async Task A_pending_command_rejects_replacement_and_retries_the_original_payload_and_key()
    {
        // Arrange
        var submission = new IdempotentSubmission<string>();
        var keys = new List<string>();
        var payload = "immutable original";
        var first = true;

        // Act
        await submission.Submit("save", (key, _) =>
        {
            keys.Add(key);
            var kind = first ? HttpOutcomeKind.TransportFailure : HttpOutcomeKind.Succeeded;
            first = false;
            return Task.FromResult(new HttpOutcome<string> { Kind = kind, Value = payload });
        }, TestContext.Current.CancellationToken);
        var replacement = await submission.Submit("replacement", (_, _) => throw new InvalidOperationException("Replacement must not run."), TestContext.Current.CancellationToken);
        var retried = await submission.Retry(TestContext.Current.CancellationToken);

        // Assert
        replacement.ShouldBeNull();
        retried.ShouldNotBeNull().Value.ShouldBe(payload);
        keys.Count.ShouldBe(2);
        keys[0].ShouldBe(keys[1]);
        submission.HasPending.ShouldBeFalse();
    }

    [Fact]
    public async Task Concurrent_submission_and_retry_cannot_execute_a_second_request()
    {
        // Arrange
        var submission = new IdempotentSubmission<string>();
        var completion = new TaskCompletionSource<HttpOutcome<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var active = submission.Submit("save", (_, _) => { Interlocked.Increment(ref calls); return completion.Task; }, TestContext.Current.CancellationToken);

        // Act
        var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => submission.Submit("duplicate", (_, _) => throw new InvalidOperationException("Duplicate."), TestContext.Current.CancellationToken), TestContext.Current.CancellationToken)));
        var retry = await submission.Retry(TestContext.Current.CancellationToken);
        completion.SetResult(new HttpOutcome<string> { Kind = HttpOutcomeKind.Succeeded });
        await active;

        // Assert
        attempts.All(result => result is null).ShouldBeTrue();
        retry.ShouldBeNull();
        calls.ShouldBe(1);
        submission.IsBusy.ShouldBeFalse();
        submission.HasPending.ShouldBeFalse();
    }

    [Fact]
    public async Task A_thrown_cancellation_retains_the_key_and_releases_the_busy_flag()
    {
        // Arrange
        var submission = new IdempotentSubmission<string>();
        var keys = new List<string>();
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        Func<Task> send = async () => await submission.Submit("save", (key, token) =>
        {
            keys.Add(key);
            return token.IsCancellationRequested ? Task.FromCanceled<HttpOutcome<string>>(token)
                : Task.FromResult(new HttpOutcome<string> { Kind = HttpOutcomeKind.Succeeded });
        }, canceled.Token);

        // Act
        await send.ShouldThrowAssignableTo<OperationCanceledException>();
        var pending = submission.HasPending;
        var busy = submission.IsBusy;
        await submission.Retry(TestContext.Current.CancellationToken);

        // Assert
        pending.ShouldBeTrue();
        busy.ShouldBeFalse();
        keys[0].ShouldBe(keys[1]);
        submission.HasPending.ShouldBeFalse();
    }
}
