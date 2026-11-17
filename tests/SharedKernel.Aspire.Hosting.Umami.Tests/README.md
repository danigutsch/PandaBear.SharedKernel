# SharedKernel.Aspire.Hosting.Umami.Tests

End-to-end tests of `SharedKernel.Aspire.Hosting.Umami` with `SharedKernel.AspNetCore.WebAnalytics`.

## Scope

The tests start Umami and its PostgreSQL server through the Aspire application model, as an AppHost does, let the website resource
set the website up, send page views through the Umami page-view target with the website's connection string, and read them back
from Umami. They need a working container runtime (Docker or Podman) and pull the pinned Umami and PostgreSQL images on the first run.
CI runs them in the default slice like any other test project; they start and remove their own containers.

## See Also

- [tests/README.md](../README.md)
