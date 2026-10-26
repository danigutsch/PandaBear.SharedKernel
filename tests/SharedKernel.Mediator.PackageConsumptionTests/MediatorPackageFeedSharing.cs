namespace PandaBear.Mediator.PackageConsumptionTests;

/// <summary>
/// Shares one packed feed between every consumption test class, so the packages are built once and never packed concurrently.
/// </summary>
[CollectionDefinition(Name)]
public sealed class MediatorPackageFeedSharing : ICollectionFixture<MediatorPackageFeedFixture>
{
    /// <summary>
    /// The name of the collection.
    /// </summary>
    public const string Name = "Mediator package feed";
}
