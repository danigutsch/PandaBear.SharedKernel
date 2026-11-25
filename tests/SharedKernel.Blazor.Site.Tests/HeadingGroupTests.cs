using Microsoft.AspNetCore.Components;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the one heading group that a card, a hero, a section and a step render: an eyebrow, the heading and a subtitle.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class HeadingGroupTests : BunitContext
{
    private static readonly RenderFragment Title = builder =>
    {
        builder.OpenElement(0, "h2");
        builder.AddContent(1, "Tours");
        builder.CloseElement();
    };

    private static readonly Dictionary<string, HeadingGroupClasses> ClassesByComponent = new(StringComparer.Ordinal)
    {
        ["Card"] = HeadingGroupClasses.Card,
        ["Hero"] = HeadingGroupClasses.Hero,
        ["Section"] = HeadingGroupClasses.Section,
        ["StepList"] = HeadingGroupClasses.Step,
    };

    [Theory]
    [InlineData(null, null)]
    [InlineData("", " ")]
    [InlineData(" ", "\t\n")]
    public void Without_an_eyebrow_or_a_subtitle_the_heading_stands_alone(string? eyebrow, string? subtitle)
    {
        // Arrange & Act
        var cut = Render(HeadingGroup.Render(HeadingGroupClasses.Hero, eyebrow, Title, subtitle));

        // Assert
        cut.Markup.ShouldBe("<h2>Tours</h2>");
    }

    [Theory]
    [InlineData("Card", "site-card__heading", "site-card__eyebrow", "site-card__subtitle")]
    [InlineData("Hero", "site-hero__heading", "site-hero__eyebrow", "site-hero__subtitle")]
    [InlineData("Section", "site-section__heading", "site-section__eyebrow", "site-section__subtitle")]
    [InlineData("StepList", "site-step-list__heading", "site-step-list__number", "site-step-list__subtitle")]
    public void An_eyebrow_and_a_subtitle_frame_the_heading_in_one_hgroup_named_after_its_component(string component, string group, string eyebrow, string subtitle)
    {
        // Arrange & Act
        var cut = Render(HeadingGroup.Render(ClassesByComponent[component], "Adventure", Title, "Ten days"));

        // Assert
        cut.Markup.ShouldBe($"<hgroup class=\"{group}\"><p class=\"{eyebrow}\">Adventure</p><h2>Tours</h2><p class=\"{subtitle}\">Ten days</p></hgroup>");
    }

    [Fact]
    public void An_eyebrow_alone_opens_the_group_before_the_heading()
    {
        // Arrange & Act
        var cut = Render(HeadingGroup.Render(HeadingGroupClasses.Card, "Cycling", Title));

        // Assert
        cut.Markup.ShouldBe("<hgroup class=\"site-card__heading\"><p class=\"site-card__eyebrow\">Cycling</p><h2>Tours</h2></hgroup>");
    }

    [Fact]
    public void A_subtitle_alone_follows_the_heading_in_the_group()
    {
        // Arrange & Act
        var cut = Render(HeadingGroup.Render(HeadingGroupClasses.Hero, null, Title, "Since 2004"));

        // Assert
        cut.Markup.ShouldBe("<hgroup class=\"site-hero__heading\"><h2>Tours</h2><p class=\"site-hero__subtitle\">Since 2004</p></hgroup>");
    }

    [Fact]
    public void The_eyebrow_and_the_subtitle_are_encoded()
    {
        // Arrange & Act
        var cut = Render(HeadingGroup.Render(HeadingGroupClasses.Section, "<b>New</b>", Title, "a & b"));

        // Assert
        cut.FindAll("b").ShouldBeEmpty();
        cut.Find(".site-section__eyebrow").TextContent.ShouldBe("<b>New</b>");
        cut.Find(".site-section__subtitle").TextContent.ShouldBe("a & b");
    }
}
