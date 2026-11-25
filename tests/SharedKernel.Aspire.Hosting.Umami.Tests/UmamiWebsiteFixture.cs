using System.Runtime.CompilerServices;
using Aspire.Hosting.Testing;
using SharedKernel.IntegrationTesting;

namespace SharedKernel.Aspire.Hosting.Umami.Tests;

/// <summary>Creates the Aspire builder in this test project's assembly.</summary>
public sealed class UmamiWebsiteFixture : Testing.UmamiTestWebsiteFixture
{
    /// <summary>Creates the Aspire testing builder from this assembly, never inlined so that Aspire finds this assembly's DCP metadata on the call stack.</summary>
    /// <returns>The builder.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override IDistributedApplicationTestingBuilder CreateBuilder() => AspireTestApplication.CreateBuilder();
}
