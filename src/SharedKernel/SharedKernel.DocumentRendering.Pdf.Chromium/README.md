# Chromium tagged PDF rendering

This optional library renders structured document requests through the semantic
HTML renderer, Playwright Chromium and the qpdf normalizer. Provision the pinned
browser process, fonts and qpdf binary in the consuming application. NativeAOT
is outside this adapter's supported runtime because Playwright's transport uses
runtime JSON serialization.

```csharp
var renderer = new ChromiumDocumentRenderer(chromium, qpdfPath,
    maxHtmlBytes: 65_536, maxPdfBytes: 262_144, timeout: TimeSpan.FromSeconds(20));
var pdf = await renderer.Render(request, immutableDocumentDate, cancellationToken);
```

To show running page numbers, call the overload with `includePageNumbers: true`.
It reserves a 20 mm bottom margin, centers each page number and marks it as
a pagination artifact. The number marker uses vector strokes within that
margin. The same document, date and qualified runtime produce deterministic
numbered output.

Use trusted, application-bounded structured templates. The HTML size limit
applies during HTML emission, before browser transfer. PDF transport is limited
before accumulating the result, and the qpdf rewrite has the same byte budget
and deadline.
Chromium still owns its internal rendering resources. Set process and memory
limits appropriate to the consuming workload.

Each document uses a private offline browser context with scripts and service
workers disabled. The renderer rejects attempted external page requests.
Supply branding logos as bounded, previously validated PNG, JPEG or WebP data
URIs. An invalid, unavailable or URL-based logo fails the render. The caller
owns the borrowed browser process, operating-system egress controls and font
inventory. Dispose the browser after its callers finish.

Use a fixed template, immutable date and verified runtime and font artifacts for
reproducible output. Application eligibility, locale, document title, approval,
storage and retention policy stay in the caller.
