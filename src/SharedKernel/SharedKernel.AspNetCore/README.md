# SharedKernel.AspNetCore

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

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

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
