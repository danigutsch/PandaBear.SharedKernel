namespace SharedKernel.Blazor.Site;

/// <summary>
/// The class names of a <see cref="HeadingGroup" />, named after the component that renders it: <c>{block}__heading</c> on the
/// <c>hgroup</c>, <c>{block}__{eyebrow}</c> on the eyebrow <c>p</c> and <c>{block}__subtitle</c> on the subtitle <c>p</c>.
/// </summary>
/// <param name="Block">The component's class, such as <c>site-card</c>.</param>
/// <param name="EyebrowElement">The element name of the eyebrow, <c>eyebrow</c> unless the component names it after its content.</param>
internal sealed record HeadingGroupClasses(string Block, string EyebrowElement = "eyebrow")
{
    /// <summary>Gets the classes of a <see cref="Site.Card" />'s heading group.</summary>
    public static HeadingGroupClasses Card { get; } = new("site-card");

    /// <summary>Gets the classes of a <see cref="Site.Hero" />'s heading group.</summary>
    public static HeadingGroupClasses Hero { get; } = new("site-hero");

    /// <summary>Gets the classes of a <see cref="Site.Section" />'s heading group.</summary>
    public static HeadingGroupClasses Section { get; } = new("site-section");

    /// <summary>Gets the classes of a <see cref="StepList" /> step's heading group, whose eyebrow is the step's number.</summary>
    public static HeadingGroupClasses Step { get; } = new("site-step-list", "number");

    /// <summary>Gets the class of the <c>hgroup</c>.</summary>
    public string Group => Block + "__heading";

    /// <summary>Gets the class of the eyebrow <c>p</c>.</summary>
    public string Eyebrow => Block + "__" + EyebrowElement;

    /// <summary>Gets the class of the subtitle <c>p</c>.</summary>
    public string Subtitle => Block + "__subtitle";
}
