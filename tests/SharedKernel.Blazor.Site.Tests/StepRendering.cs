namespace PandaBear.Blazor.Site.Tests;

/// <summary>Builds the steps and renders the step lists that the step list tests share.</summary>
internal static class StepRendering
{
    /// <summary>Renders a step list of the steps with a number format, "Day {0}" unless another is given, then applies further parameters.</summary>
    /// <param name="context">The test context.</param>
    /// <param name="steps">The steps.</param>
    /// <param name="configure">Further parameters, applied last.</param>
    /// <param name="numberFormat">The number format.</param>
    /// <returns>The rendered list.</returns>
    public static IRenderedComponent<StepList> RenderSteps(
        this BunitContext context,
        StepItem[] steps,
        Action<ComponentParameterCollectionBuilder<StepList>>? configure = null,
        string numberFormat = "Day {0}") =>
        context.Render<StepList>(parameters =>
        {
            parameters.Add(component => component.Steps, steps).Add(component => component.NumberFormat, numberFormat);
            configure?.Invoke(parameters);
        });

    /// <summary>Creates a step with one paragraph, "Text.", no note and no facts.</summary>
    /// <param name="title">The title.</param>
    /// <param name="image">Whether the step has a described 400 by 300 image at <c>/{title}.jpg</c>.</param>
    /// <returns>The step.</returns>
    public static StepItem Step(string title, bool image = false) =>
        new(title, ["Text."], null, image ? new GalleryImage(SiteFixtures.Image("/" + title + ".jpg")) : null, []);
}
