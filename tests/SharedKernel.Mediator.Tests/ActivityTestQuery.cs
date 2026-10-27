namespace PandaBear.Mediator.Tests;

/// <summary>
/// Query request carrying an integer id that the activity behavior tests dispatch through the behavior under test.
/// </summary>
/// <param name="Id">The integer the test handlers read to compute their response.</param>
internal sealed record ActivityTestQuery(int Id) : IQuery<int>;
