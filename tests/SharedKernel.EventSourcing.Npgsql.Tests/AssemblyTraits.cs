[assembly: TestScope(TestScope.Integration)]
[assembly: Trait(PandaBear.EventSourcing.Npgsql.Tests.TestTraits.ComponentName, PandaBear.EventSourcing.Npgsql.Tests.TestTraits.PostgreSqlEventSourcingComponent)]
[assembly: TestHost(TestHost.Aspire)]
[assembly: AssemblyFixture(typeof(PandaBear.EventSourcing.Npgsql.Tests.PostgreSqlTestServerFixture))]
