namespace PandaBear.Testing.AspNetCore.Tests;

/// <summary>Tests the prerendered state check against a real Blazor Web App page with an Interactive Server component.</summary>
[Trait(TestTraitNames.ScopeName, "Integration")]
[Trait(TestTraitNames.CapabilityName, "PrerenderedComponentState")]
public sealed class PrerenderedComponentStateHostTests
{
    [Fact]
    public async Task A_small_persisted_state_is_found_in_the_prerendered_page_and_passes()
    {
        // Arrange
        await using var host = KestrelTestHost.Start<Program>();
        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        // Act
        var html = await client.GetStringAsync(new Uri("/state?size=100", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        html.ShouldContain(PrerenderedComponentState.ServerStateMarker, StringComparison.Ordinal);
        PrerenderedComponentState.ServerStateSize(html).ShouldBeInRange(100, 2_000);
        PrerenderedComponentState.WebAssemblyStateSize(html).ShouldBe(0);
        PrerenderedComponentState.ShouldKeepServerStateBelow(html);
    }

    [Fact]
    public async Task A_persisted_state_above_the_hub_receive_limit_fails_the_check()
    {
        // Arrange
        await using var host = KestrelTestHost.Start<Program>();
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        var html = await client.GetStringAsync(new Uri("/state?size=40000", UriKind.Relative), TestContext.Current.CancellationToken);
        var check = () => PrerenderedComponentState.ShouldKeepServerStateBelow(html);

        // Act
        var failure = check.ShouldThrow<InvalidOperationException>();

        // Assert
        PrerenderedComponentState.ServerStateSize(html).ShouldBeGreaterThan(40_000);
        failure.Message.ShouldContain("limit of 32768 bytes", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_page_without_interactive_components_carries_no_state()
    {
        // Arrange
        await using var host = KestrelTestHost.Start<Program>();
        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        // Act
        var text = await client.GetStringAsync(new Uri("/ping", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        PrerenderedComponentState.ServerStateSize(text).ShouldBe(0);
        PrerenderedComponentState.ShouldKeepServerStateBelow(text);
    }
}
