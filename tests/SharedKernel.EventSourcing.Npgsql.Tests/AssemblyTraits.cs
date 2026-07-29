[assembly: Trait(SharedKernel.Testing.TestTraitNames.ScopeName, SharedKernel.EventSourcing.Npgsql.Tests.TestTraits.IntegrationScope)]
[assembly: Trait(SharedKernel.EventSourcing.Npgsql.Tests.TestTraits.ComponentName, SharedKernel.EventSourcing.Npgsql.Tests.TestTraits.PostgreSqlEventSourcingComponent)]
[assembly: Trait(SharedKernel.Testing.TestTraitNames.HostName, SharedKernel.EventSourcing.Npgsql.Tests.TestTraits.AspireHost)]
[assembly: AssemblyFixture(typeof(SharedKernel.EventSourcing.Npgsql.Tests.PostgreSqlTestServerFixture))]
