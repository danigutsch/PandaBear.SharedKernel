namespace SharedKernel.Blazor.Site.Tests.Browser;

/// <summary>A node that Chromium exposes to assistive technology.</summary>
/// <param name="Role">The Chromium role, such as <c>navigation</c> or <c>DisclosureTriangle</c>.</param>
/// <param name="Name">The accessible name, or an empty string.</param>
/// <param name="Expanded">The expanded state, or <see langword="null" /> for a node without one.</param>
internal sealed record AccessibleNode(string Role, string Name, bool? Expanded);
