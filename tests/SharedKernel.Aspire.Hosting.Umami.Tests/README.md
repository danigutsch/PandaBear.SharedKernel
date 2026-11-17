# SharedKernel.Aspire.Hosting.Umami.Tests

End-to-end tests of `SharedKernel.Aspire.Hosting.Umami` with `SharedKernel.AspNetCore.WebAnalytics`.

## Scope

The end-to-end test sends a page view through the Umami page-view target with the connection string of a website that the website
resource set up in a real Umami, and reads it back from Umami's statistics. It gets the website from `UmamiWebsiteFixture`, the
assembly fixture of [SharedKernel.Aspire.Hosting.Umami.Testing](../SharedKernel.Aspire.Hosting.Umami.Testing/README.md): when
`SHAREDKERNEL_TESTING_UMAMI_CONNECTION_STRING` is set it borrows the website of the Umami that another process owns, otherwise it starts
Umami and its PostgreSQL server once for the assembly through the Aspire application model, as an AppHost does. Because a borrowed
Umami may hold page views of earlier runs, the test compares the count before and after its visit. Starting an Umami of its own needs a
working container runtime (Docker or Podman) and pulls the pinned Umami and PostgreSQL images on the first run.

The other tests check the fixture itself without containers: how the variable is borrowed, that a set variable never starts a second
Umami, that one start serves the whole assembly, and what the owner of a shared Umami hands over when Umami does not start.

## See Also

- [tests/README.md](../README.md)
