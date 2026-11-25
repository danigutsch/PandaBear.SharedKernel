namespace SharedKernel.Mediator.Tests;

/// <summary>
/// Query request whose response is a generic list, so the activity behavior tests can check how generic type names are tagged.
/// </summary>
/// <param name="Count">The value the test handler puts into the list it returns.</param>
internal sealed record ActivityTestListQuery(int Count) : IQuery<List<int>>;
