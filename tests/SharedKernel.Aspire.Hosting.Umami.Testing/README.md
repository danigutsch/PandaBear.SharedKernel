# SharedKernel.Aspire.Hosting.Umami.Testing

Test support for the Umami end-to-end tests of this repository; not a package.

`UmamiTestWebsiteFixture` gives the tests of an assembly one Umami website, resolved when the first test asks for it and never
before, so a run that selects no such test starts no container:

- Borrow: when `SHAREDKERNEL_TESTING_UMAMI_CONNECTION_STRING` is set, its value is the connection string
  `Endpoint={Umami's base address};WebsiteId={id}` of a website in an Umami that another process owns. The fixture only sends page
  views to it and reads them back; it never starts or stops Umami. A value that is not such a connection string fails every test that
  needs the website with a message that names the variable and never repeats the value. So does `Unavailable`, which the owner hands
  over when its Umami did not start; the owner's standard error says why. A set variable never falls back to an Umami of the assembly's
  own, because that would hide a broken owner and start a second Umami and a second PostgreSQL server.
- Own: when the variable is not set or is blank, the fixture starts Umami with a PostgreSQL server of its own and the website `site`
  (`site.example`) through its `CreateBuilder` override, once for the assembly, and stops them when the assembly is done. A failed
  start is cached, so it fails every test that needs the website instead of repeating the five-minute startup timeout.

The `CreateBuilder` override must stay in the executable test assembly with `NoInlining`, because Aspire finds its DCP metadata by
walking the call stack.

`UmamiTestWebsite` composes Umami and the website for both owners: the fixture, and the shared test-services host of this repository,
which keeps Umami's data in a database on its one PostgreSQL server. Both make a wait for a resource stop as soon as it fails to start,
so a website that cannot be set up fails at once. The host waits for the website with `HandOver`: when Umami does not start, it writes
the failure to standard error and hands over `Unavailable`, so the tests that borrow PostgreSQL still run and only the tests that
borrow Umami fail. See `tests/README.md` in the repository.

## See Also

- [SharedKernel.Aspire.Hosting.Umami](../../src/SharedKernel/SharedKernel.Aspire.Hosting.Umami/README.md)
- [SharedKernel.Aspire.Hosting.Umami.Tests](../SharedKernel.Aspire.Hosting.Umami.Tests/README.md)
