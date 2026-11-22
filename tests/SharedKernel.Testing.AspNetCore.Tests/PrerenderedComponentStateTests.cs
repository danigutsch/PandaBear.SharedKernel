using Microsoft.AspNetCore.SignalR;

namespace PandaBear.Testing.AspNetCore.Tests;

/// <summary>Tests reading and bounding the component state of a prerendered Blazor page.</summary>
[Trait(SharedKernelTestTraitNames.ScopeName, SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "PrerenderedComponentState")]
public sealed class PrerenderedComponentStateTests
{
    [Fact]
    public void A_page_without_state_has_size_zero_and_passes()
    {
        // Arrange
        const string html = "<!DOCTYPE html><html><body><!--Blazor:{\"type\":\"server\"}--><p>Hi</p></body></html>";

        // Act
        var server = PrerenderedComponentState.ServerStateSize(html);
        var webAssembly = PrerenderedComponentState.WebAssemblyStateSize(html);

        // Assert
        server.ShouldBe(0);
        webAssembly.ShouldBe(0);
        PrerenderedComponentState.ShouldKeepServerStateBelow(html);
    }

    [Fact]
    public void Each_kind_of_state_is_measured_between_its_marker_and_the_comment_end()
    {
        // Arrange
        var html = "<body><p>x</p>" + PrerenderedComponentState.ServerStateMarker + "AAAA" + "-->"
            + PrerenderedComponentState.WebAssemblyStateMarker + "BBBBBB" + "--></body>";

        // Act
        var server = PrerenderedComponentState.ServerStateSize(html);
        var webAssembly = PrerenderedComponentState.WebAssemblyStateSize(html);

        // Assert
        server.ShouldBe(4);
        webAssembly.ShouldBe(6);
    }

    [Fact]
    public void Several_state_comments_of_one_kind_are_added_up_in_encoded_bytes()
    {
        // Arrange
        var html = PrerenderedComponentState.ServerStateMarker + "AB-->" + "<p>between</p>"
            + PrerenderedComponentState.ServerStateMarker + "é-->";

        // Act
        var size = PrerenderedComponentState.ServerStateSize(html);

        // Assert
        size.ShouldBe(4);
    }

    [Fact]
    public void An_unclosed_state_comment_is_reported()
    {
        // Arrange
        var html = "<body>" + PrerenderedComponentState.ServerStateMarker + "AAAA";
        Action measure = () => PrerenderedComponentState.ServerStateSize(html);

        // Act
        var failure = measure.ShouldThrow<FormatException>();

        // Assert
        failure.Message.ShouldContain(PrerenderedComponentState.ServerStateMarker, StringComparison.Ordinal);
    }

    [Fact]
    public void State_at_or_above_the_limit_fails_with_the_size_the_limit_and_the_guidance()
    {
        // Arrange
        var html = PrerenderedComponentState.ServerStateMarker + new string('A', 100) + "-->";
        var atLimit = () => PrerenderedComponentState.ShouldKeepServerStateBelow(html, 100);

        // Act
        var failure = atLimit.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("100 bytes, which is not below the limit of 100 bytes", StringComparison.Ordinal);
        failure.Message.ShouldContain(PrerenderedComponentState.GuidanceUrl, StringComparison.Ordinal);
        PrerenderedComponentState.ShouldKeepServerStateBelow(html, 101);
    }

    [Fact]
    public void The_client_side_state_does_not_count_against_the_circuit_limit()
    {
        // Arrange
        var html = PrerenderedComponentState.WebAssemblyStateMarker + new string('A', 100) + "-->";

        // Act & Assert
        PrerenderedComponentState.ShouldKeepServerStateBelow(html, 10);
    }

    [Fact]
    public void The_default_limit_is_the_framework_default_receive_size_of_32768_bytes()
    {
        // Arrange
        var options = new HubOptions();

        // Act
        new HubOptionsSetup([]).Configure(options);

        // Assert
        PrerenderedComponentState.DefaultMaximumReceiveMessageSize.ShouldBe(32 * 1024);
        PrerenderedComponentState.DefaultMaximumReceiveMessageSize.ShouldBe(options.MaximumReceiveMessageSize!.Value);
    }

    [Fact]
    public void Invalid_arguments_are_rejected()
    {
        // Arrange
        Action noHtml = () => PrerenderedComponentState.ServerStateSize(null!);
        var noHtmlAssert = () => PrerenderedComponentState.ShouldKeepServerStateBelow(null!);
        var zeroLimit = () => PrerenderedComponentState.ShouldKeepServerStateBelow("<p></p>", 0);

        // Act & Assert
        noHtml.ShouldThrow<ArgumentNullException>();
        noHtmlAssert.ShouldThrow<ArgumentNullException>();
        zeroLimit.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
