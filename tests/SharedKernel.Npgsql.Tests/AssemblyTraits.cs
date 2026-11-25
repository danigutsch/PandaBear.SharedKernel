[assembly: TestScope(TestScope.Integration)]
[assembly: TestHost(TestHost.Aspire)]
[assembly: AssemblyFixture(typeof(SharedKernel.Npgsql.Tests.PostgreSqlTestServerFixture))]
