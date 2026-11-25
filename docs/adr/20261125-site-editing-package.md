# Site editing package

Date: 2026-11-25. Status: accepted.

The administration editors of the data a site owner changes for the public site, starting with the theme editor, live in a new package,
`PandaBear.SharedKernel.Blazor.Site.Editing` (project, assembly and namespace `SharedKernel.Blazor.Site.Editing`), which references
`SharedKernel.Blazor` and `SharedKernel.Blazor.Site`. Neither existing package takes them.

## Why a separate package

- `SharedKernel.Blazor.Site` renders static, script-free public sites. Its consumers (a public site host, page-section renderers) need the
  site components, the theme model, its stylesheet and its storage document, but no form machinery. It references only BuildingBlocks,
  ContentRights and InputNormalization.
- An accessible editor needs `SharedKernel.Blazor`'s form pieces: `NamedFormField` for labels, help and messages wired with
  `aria-describedby` and `aria-invalid`, `ParsedTextInputState` for numbers typed as text, `FieldIds` for the control ids and
  `ServerFormSubmission` for placing a save's rule errors and linking the error summary to them. Writing the editor without them would fork
  that logic.
- `SharedKernel.Blazor` references `SharedKernel.HttpClients`, so a reference from Blazor.Site would bring
  `Microsoft.Extensions.Http.Resilience` (Polly), service discovery, OpenTelemetry and Idempotency.Abstractions to every public site. A
  reference the other way would bring Blazor.Site's static web assets (`site.css`) and ContentRights to every admin form.

So Blazor.Site stays free of HttpClients and Polly, and an admin editor that needs both the site model and the form pieces lives in
`.Editing`. A public site host takes Blazor.Site alone; an admin host adds `.Editing`.

## Dependencies and cost

- One more package and preview surface, with a public API baseline, a README and a unit test project
  (`SharedKernel.Blazor.Site.Editing.Tests.Unit`, bUnit, in process).
- No new package version in the repository: the package takes the shared framework (`Microsoft.AspNetCore.App`), as Blazor.Site does,
  and the two project references; its consumers receive their existing dependency trees. It keeps no lock file
  (`lockExemptProjects`), because every package it resolves is already locked elsewhere.
- Blazor.Site gains the small public lists the editor offers, so the options follow the rules instead of copying them:
  `TypographyStyle.Colors`, `TypographyStyle.TakesColor(role)`, `SiteShape.DividerColors` and `ThemeFont.Weights`.

## Rules the editors follow

- Texts are parameters with English defaults; the application supplies its own wording or language.
- Every field path an editor shows is listed, so a host's `ServerFormSubmission` places every rule error on its control.
- An editor binds the model the site package defines (`ThemeEditorModel` for the theme) and stores nothing itself: the host owns the form,
  the save, the preview and application data such as a font library.
- No style attributes and no script, so a strict Content-Security-Policy holds; an editor needs an interactive render mode.

Registry publication, releases, and consumer adoption remain separate actions.
