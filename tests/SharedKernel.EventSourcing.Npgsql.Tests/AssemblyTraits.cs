[assembly: PandaBear.Testing.TestScope(PandaBear.Testing.TestScope.Integration)]
[assembly: Trait(PandaBear.EventSourcing.Npgsql.Tests.TestTraits.ComponentName, PandaBear.EventSourcing.Npgsql.Tests.TestTraits.PostgreSqlEventSourcingComponent)]
[assembly: PandaBear.Testing.TestHost(PandaBear.Testing.TestHost.Aspire)]
[assembly: AssemblyFixture(typeof(PandaBear.EventSourcing.Npgsql.Tests.PostgreSqlTestServerFixture))]
