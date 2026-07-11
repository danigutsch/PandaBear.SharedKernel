# SharedKernel.AspNetCore

Reusable ASP.NET Core helpers for applications that explicitly opt into shared hosting and security
mechanics.

The package does not define application policy names, origin lists, rate limits, roles, claims, or
content security policies. Each consuming application owns those decisions.

Use `MapRobotsTxt` to expose an application-owned crawler policy at `/robots.txt` with
`text/plain; charset=utf-8`.

Use `SitemapXmlSerializer` to generate sitemap XML while retaining URL inclusion policy in the
consuming application.
