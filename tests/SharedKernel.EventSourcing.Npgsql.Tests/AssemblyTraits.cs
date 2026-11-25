[assembly: TestScope(TestScope.Integration)]
[assembly: Trait(SharedKernel.EventSourcing.Npgsql.Tests.TestTraits.ComponentName, SharedKernel.EventSourcing.Npgsql.Tests.TestTraits.PostgreSqlEventSourcingComponent)]
[assembly: TestHost(TestHost.Aspire)]
[assembly: AssemblyFixture(typeof(SharedKernel.EventSourcing.Npgsql.Tests.PostgreSqlTestServerFixture))]
