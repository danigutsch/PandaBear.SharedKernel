# SharedKernel.AspNetCore

Reusable ASP.NET Core helpers for applications that explicitly opt into shared hosting and security
mechanics.

The package does not define application policy names, origin lists, rate limits, roles, claims, or
content security policies. Each consuming application owns those decisions.

Use `MapRobotsTxt` to expose an application-owned crawler policy at `/robots.txt` with
`text/plain; charset=utf-8`.

Use `SitemapXmlSerializer` to generate sitemap XML while retaining URL inclusion policy in the
consuming application.

Use `IdempotencyKeyHeader.Create(value)` to create a required `IdempotencyKey` from HTTP input as a
`Result<IdempotencyKey>`. The value object owns token rules and normalization; this adapter associates
missing or invalid input with the standard `Idempotency-Key` validation field. It does not generate
keys, coordinate operations, or log header values.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.AspNetCore`. C# namespace: `PandaBear.AspNetCore`. Assembly/project identity: `SharedKernel.AspNetCore`.

Consume a validated private candidate at an explicitly selected version. Registry publication is deferred; no public NuGet availability is implied.
