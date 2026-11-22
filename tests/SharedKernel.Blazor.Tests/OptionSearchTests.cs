namespace PandaBear.Blazor.Tests;

/// <summary>Tests the bounded search state of a picker over a large option set.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class OptionSearchTests
{
    private const string Shown = "{0} of {1} images for ‘{2}’";
    private const string NoMatches = "No images for ‘{0}’";

    private static CancellationToken Token => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task Nothing_is_fetched_before_the_search_starts_and_starting_searches_the_latest_query()
    {
        // Arrange
        var queries = new List<string>();
        using var search = new OptionSearch<string>((query, _, _) =>
        {
            queries.Add(query);
            return Task.FromResult(new ListSlice<string>(["beach.jpg"], 1));
        }, 5);

        // Act
        await search.Search("bea", Token);
        var fetchedBeforeStart = queries.Count;
        var startedBeforeStart = search.IsStarted;
        await search.Start(Token);
        await search.Start(Token);

        // Assert
        fetchedBeforeStart.ShouldBe(0);
        startedBeforeStart.ShouldBeFalse();
        search.IsStarted.ShouldBeTrue();
        queries.ShouldBe(["bea"]);
        search.Results.ShouldBe(["beach.jpg"]);
    }

    [Fact]
    public async Task A_search_keeps_the_trimmed_query_the_results_and_the_total()
    {
        // Arrange
        var calls = new List<(string Query, int Take)>();
        using var search = await OptionSearchFixtures.Started((query, take, _) =>
        {
            calls.Add((query, take));
            return Task.FromResult(new ListSlice<string>(["beach.jpg", "bay.jpg"], 40));
        }, 2);

        // Act
        await search.Search("  b  ", Token);

        // Assert
        calls.ShouldBe([("", 2), ("b", 2)]);
        search.Query.ShouldBe("b");
        search.Results.ShouldBe(["beach.jpg", "bay.jpg"]);
        search.Total.ShouldBe(40);
        search.IsTruncated.ShouldBeTrue();
        search.IsSearching.ShouldBeFalse();
        search.HasResults.ShouldBeTrue();
    }

    [Fact]
    public async Task Results_that_hold_every_match_are_not_truncated()
    {
        // Arrange
        using var search = await OptionSearchFixtures.Started((_, _, _) => Task.FromResult(new ListSlice<string>(["a", "b"], 2)), 5);

        // Act
        await search.Search("a", Token);

        // Assert
        search.IsTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task An_empty_or_missing_query_is_passed_as_empty_text_so_the_application_chooses_its_first_options()
    {
        // Arrange
        var queries = new List<string>();
        using var search = await OptionSearchFixtures.Started((query, _, _) =>
        {
            queries.Add(query);
            return Task.FromResult(new ListSlice<string>([], 0));
        }, 5);

        // Act
        await search.Search(null, Token);
        await search.Search("   ", Token);

        // Assert
        queries.ShouldBe(["", "", ""]);
        search.Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_query_shorter_than_the_minimum_fetches_nothing_and_clears_the_results()
    {
        // Arrange
        var queries = new List<string>();
        using var search = await OptionSearchFixtures.Started((query, _, _) =>
        {
            queries.Add(query);
            return Task.FromResult(new ListSlice<string>(["a"], 1));
        }, 5, minQueryLength: 2);
        await search.Search("ab", Token);

        // Act
        await search.Search(" a ", Token);

        // Assert
        queries.ShouldBe(["ab"]);
        search.Query.ShouldBe("a");
        search.IsQueryTooShort.ShouldBeTrue();
        search.Results.ShouldBeEmpty();
        search.HasResults.ShouldBeFalse();
        search.IsSearching.ShouldBeFalse();
        search.Status(Shown, NoMatches).ShouldBeNull();
    }

    [Fact]
    public async Task A_long_query_is_cut_to_the_maximum_length()
    {
        // Arrange
        string? received = null;
        using var search = await OptionSearchFixtures.Started((query, _, _) =>
        {
            received = query;
            return Task.FromResult(new ListSlice<string>([], 0));
        }, 5);

        // Act
        await search.Search(" " + HostileText.VeryLong(OptionSearch<string>.MaxQueryLength * 50), Token);

        // Assert
        received.ShouldNotBeNull();
        received.Length.ShouldBe(OptionSearch<string>.MaxQueryLength);
        search.Query.ShouldBe(received);
    }

    [Fact]
    public async Task A_cut_query_never_ends_in_half_a_surrogate_pair()
    {
        // Arrange
        string? received = null;
        using var search = await OptionSearchFixtures.Started((query, _, _) =>
        {
            received = query;
            return Task.FromResult(new ListSlice<string>([], 0));
        }, 5);
        var text = new string('x', OptionSearch<string>.MaxQueryLength - 1) + "\U0001F600";

        // Act
        await search.Search(text, Token);

        // Assert
        received.ShouldBe(new string('x', OptionSearch<string>.MaxQueryLength - 1));
    }

    [Fact]
    public async Task More_results_than_the_limit_are_cut_to_the_limit()
    {
        // Arrange
        using var search = await OptionSearchFixtures.Started((_, _, _) => Task.FromResult(new ListSlice<int>([1, 2, 3, 4], 9)), 2);

        // Act
        await search.Search("x", Token);

        // Assert
        search.Results.ShouldBe([1, 2]);
        search.Total.ShouldBe(9);
    }

    [Fact]
    public async Task Typing_waits_for_the_debounce_and_only_the_last_text_is_searched()
    {
        // Arrange
        var time = new ManualTimeProvider();
        var queries = new List<string>();
        using var search = new OptionSearch<string>(
            (query, _, _) =>
            {
                queries.Add(query);
                return Task.FromResult(new ListSlice<string>([query], 1));
            },
            5)
        {
            TimeProvider = time,
        };
        await search.Start(Token);

        // Act
        var first = search.Search("e", Token);
        time.Advance(TimeSpan.FromMilliseconds(299));
        var second = search.Search("es", Token);
        var third = search.Search("esl", Token);
        var waitingBeforeDebounce = search.IsSearching;
        time.Advance(TimeSpan.FromMilliseconds(OptionSearch<string>.DefaultDebounceMilliseconds));
        await Task.WhenAll(first, second, third);

        // Assert
        search.Debounce.ShouldBe(TimeSpan.FromMilliseconds(300));
        waitingBeforeDebounce.ShouldBeTrue();
        queries.ShouldBe(["", "esl"]);
        search.Results.ShouldBe(["esl"]);
        time.PendingTimers.ShouldBe(0);
    }

    [Fact]
    public async Task A_running_search_is_visible_and_keeps_the_previous_results_until_it_completes()
    {
        // Arrange
        var gate = new TaskCompletionSource<ListSlice<string>>();
        using var search = await OptionSearchFixtures.Started((query, _, _) =>
            query == "n" ? gate.Task : Task.FromResult(new ListSlice<string>(["old"], 1)), 5);

        // Act
        var running = search.Search("n", Token);

        // Assert
        search.IsSearching.ShouldBeTrue();
        search.Query.ShouldBe("n");
        search.Results.ShouldBe(["old"]);
        search.HasResults.ShouldBeFalse();
        search.Status(Shown, NoMatches).ShouldBeNull();
        gate.SetResult(new ListSlice<string>(["new"], 1));
        await running;
        search.IsSearching.ShouldBeFalse();
        search.Results.ShouldBe(["new"]);
    }

    [Fact]
    public async Task A_newer_search_cancels_the_older_one_and_a_late_older_result_is_never_applied()
    {
        // Arrange
        var older = new TaskCompletionSource<ListSlice<string>>();
        CancellationToken olderToken = default;
        using var search = await OptionSearchFixtures.Started((query, _, token) =>
        {
            if (query == "a")
            {
                olderToken = token;
                return older.Task;
            }

            return Task.FromResult(new ListSlice<string>(["ab"], 1));
        }, 5);
        var first = search.Search("a", Token);

        // Act
        await search.Search("ab", Token);
        older.SetResult(new ListSlice<string>(["stale"], 7));
        await first;

        // Assert
        olderToken.IsCancellationRequested.ShouldBeTrue();
        search.Query.ShouldBe("ab");
        search.Results.ShouldBe(["ab"]);
        search.Total.ShouldBe(1);
        search.IsSearching.ShouldBeFalse();
    }

    [Fact]
    public async Task An_older_result_that_arrives_before_the_newer_one_is_not_applied_and_the_search_keeps_running()
    {
        // Arrange
        var older = new TaskCompletionSource<ListSlice<string>>();
        var newer = new TaskCompletionSource<ListSlice<string>>();
        using var search = await OptionSearchFixtures.Started((query, _, _) => query switch
        {
            "a" => older.Task,
            "ab" => newer.Task,
            _ => Task.FromResult(new ListSlice<string>([], 0)),
        }, 5);
        var first = search.Search("a", Token);
        var second = search.Search("ab", Token);

        // Act
        older.SetResult(new ListSlice<string>(["stale"], 1));
        await first;

        // Assert
        search.Results.ShouldBeEmpty();
        search.IsSearching.ShouldBeTrue();
        newer.SetResult(new ListSlice<string>(["fresh"], 1));
        await second;
        search.Results.ShouldBe(["fresh"]);
        search.IsSearching.ShouldBeFalse();
    }

    [Fact]
    public async Task A_superseded_search_that_observes_its_cancellation_completes_quietly()
    {
        // Arrange
        using var search = await OptionSearchFixtures.Started(async (query, _, token) =>
        {
            if (query == "a")
            {
                await Task.Delay(Timeout.Infinite, token);
            }

            return new ListSlice<string>(["ab"], 1);
        }, 5);
        var first = search.Search("a", Token);

        // Act
        await search.Search("ab", Token);
        await first;

        // Assert
        first.IsCompletedSuccessfully.ShouldBeTrue();
        search.Results.ShouldBe(["ab"]);
    }

    [Fact]
    public async Task A_failing_search_propagates_and_leaves_no_results_for_its_query()
    {
        // Arrange
        using var search = await OptionSearchFixtures.Started((query, _, _) => query == "b"
            ? Task.FromException<ListSlice<string>>(new InvalidOperationException("database down"))
            : Task.FromResult(new ListSlice<string>(["a"], 1)), 5);
        var act = () => search.Search("b", Token);

        // Act
        var failure = await act.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldBe("database down");
        search.IsSearching.ShouldBeFalse();
        search.HasResults.ShouldBeFalse();
        search.Results.ShouldBeEmpty();
        search.Total.ShouldBe(0);
        search.Status(Shown, NoMatches).ShouldBeNull();
    }

    [Fact]
    public async Task A_superseded_search_that_fails_still_propagates_its_failure_without_touching_the_newer_state()
    {
        // Arrange
        var older = new TaskCompletionSource<ListSlice<string>>();
        using var search = await OptionSearchFixtures.Started((query, _, _) =>
            query == "a" ? older.Task : Task.FromResult(new ListSlice<string>(["ab"], 1)), 5);
        var first = search.Search("a", Token);
        await search.Search("ab", Token);

        // Act
        older.SetException(new InvalidOperationException("database down"));

        // Assert
        var observe = () => first;
        await observe.ShouldThrow<InvalidOperationException>();
        search.Results.ShouldBe(["ab"]);
        search.HasResults.ShouldBeTrue();
    }

    [Fact]
    public async Task A_search_cancelled_by_the_caller_propagates_the_cancellation_and_stops_running()
    {
        // Arrange
        using var caller = new CancellationTokenSource();
        using var search = await OptionSearchFixtures.Started(async (query, _, token) =>
        {
            if (query.Length > 0)
            {
                await Task.Delay(Timeout.Infinite, token);
            }

            return new ListSlice<string>([], 0);
        }, 5);
        var running = search.Search("a", caller.Token);

        // Act
        await caller.CancelAsync();

        // Assert
        var observe = () => running;
        await observe.ShouldThrowAssignableTo<OperationCanceledException>();
        search.IsSearching.ShouldBeFalse();
        search.HasResults.ShouldBeFalse();
    }

    [Fact]
    public async Task A_search_that_returns_no_slice_is_reported_as_a_failure()
    {
        // Arrange
        using var search = new OptionSearch<string>((_, _, _) => Task.FromResult<ListSlice<string>>(null!), 5) { Debounce = TimeSpan.Zero };
        var act = () => search.Start(Token);

        // Act & Assert
        await act.ShouldThrow<InvalidOperationException>();
        search.IsSearching.ShouldBeFalse();
    }

    [Fact]
    public async Task The_status_names_the_counts_and_the_query_or_says_that_nothing_matches()
    {
        // Arrange
        using var search = await OptionSearchFixtures.Started((query, _, _) =>
            Task.FromResult(query == "zzz" ? new ListSlice<string>([], 0) : new ListSlice<string>(["a", "b"], 40)), 2);

        // Act
        await search.Search("praia", Token);
        var some = search.Status(Shown, NoMatches);
        await search.Search("zzz", Token);
        var none = search.Status(Shown, NoMatches);

        // Assert
        some.ShouldNotBeNull();
        some.TrimEnd(' ').ShouldBe("2 of 40 images for ‘praia’");
        none.ShouldNotBeNull();
        none.TrimEnd(' ').ShouldBe("No images for ‘zzz’");
    }

    [Fact]
    public async Task The_same_announcement_after_a_new_search_still_changes_so_the_live_region_speaks_again()
    {
        // Arrange
        using var search = await OptionSearchFixtures.Started((_, _, _) => Task.FromResult(new ListSlice<string>(["a"], 1)), 5);
        await search.Search("a", Token);
        var firstKey = search.AnnouncementKey;
        var first = search.Status(Shown, NoMatches);

        // Act
        await search.Search("a ", Token);
        var second = search.Status(Shown, NoMatches);
        var unchanged = search.Status(Shown, NoMatches);

        // Assert
        second.ShouldNotBe(first);
        second.ShouldBe(unchanged);
        second!.Trim(' ').ShouldBe(first!.Trim(' '));
        search.AnnouncementKey.ShouldBe(firstKey + 1);
    }

    [Fact]
    public async Task Disposing_cancels_the_running_search_and_drops_its_result()
    {
        // Arrange
        var gate = new TaskCompletionSource<ListSlice<string>>();
        CancellationToken token = default;
        using var search = await OptionSearchFixtures.Started((query, _, cancellation) =>
        {
            if (query.Length == 0)
            {
                return Task.FromResult(new ListSlice<string>([], 0));
            }

            token = cancellation;
            return gate.Task;
        }, 5);
        var running = search.Search("a", Token);

        // Act
        search.Dispose();
        gate.SetResult(new ListSlice<string>(["late"], 1));
        await running;

        // Assert
        token.IsCancellationRequested.ShouldBeTrue();
        search.Results.ShouldBeEmpty();
        search.IsSearching.ShouldBeFalse();
        var searchAgain = () => search.Search("b", Token);
        await searchAgain.ShouldThrow<ObjectDisposedException>();
    }

    [Fact]
    public void Invalid_construction_settings_and_status_arguments_are_rejected()
    {
        // Arrange
        using var valid = new OptionSearch<string>(OptionSearchFixtures.NoOptions, 1);
        var noSearch = () => new OptionSearch<string>(null!, 1);
        var zeroLimit = () => new OptionSearch<string>(OptionSearchFixtures.NoOptions, 0);
        var negativeLimit = () => new OptionSearch<string>(OptionSearchFixtures.NoOptions, -1);
        var negativeDebounce = () => new OptionSearch<string>(OptionSearchFixtures.NoOptions, 1) { Debounce = TimeSpan.FromTicks(-1) };
        var negativeMinimum = () => new OptionSearch<string>(OptionSearchFixtures.NoOptions, 1) { MinQueryLength = -1 };
        var longMinimum = () => new OptionSearch<string>(OptionSearchFixtures.NoOptions, 1) { MinQueryLength = OptionSearch<string>.MaxQueryLength + 1 };
        var noTime = () => new OptionSearch<string>(OptionSearchFixtures.NoOptions, 1) { TimeProvider = null! };
        var noFormat = () => valid.Status(null!, NoMatches);
        var noText = () => valid.Status(Shown, null!);

        // Act & Assert
        noSearch.ShouldThrow<ArgumentNullException>();
        zeroLimit.ShouldThrow<ArgumentOutOfRangeException>();
        negativeLimit.ShouldThrow<ArgumentOutOfRangeException>();
        negativeDebounce.ShouldThrow<ArgumentOutOfRangeException>();
        negativeMinimum.ShouldThrow<ArgumentOutOfRangeException>();
        longMinimum.ShouldThrow<ArgumentOutOfRangeException>();
        noTime.ShouldThrow<ArgumentNullException>();
        noFormat.ShouldThrow<ArgumentNullException>();
        noText.ShouldThrow<ArgumentNullException>();
    }
}
