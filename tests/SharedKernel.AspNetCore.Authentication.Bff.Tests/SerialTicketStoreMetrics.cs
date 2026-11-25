using PandaBear.Testing;

namespace PandaBear.AspNetCore.Authentication.Bff.Tests;

/// <summary>Isolates observations of the package-wide ticket store meter.</summary>
[SerialTestJustification("MeterListener observes the process-wide BFF meter, so observations must exclude other ticket store tests.")]
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SerialTicketStoreMetrics
{
    /// <summary>The collection name.</summary>
    public const string Name = "BFF ticket store metrics";
}
