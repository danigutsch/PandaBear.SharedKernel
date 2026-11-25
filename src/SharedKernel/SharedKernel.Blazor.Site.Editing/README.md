# Site editing

`PandaBear.SharedKernel.Blazor.Site.Editing` holds the administration editors of the data a site owner changes for the public site that
`PandaBear.SharedKernel.Blazor.Site` renders. It builds on the form pieces of `PandaBear.SharedKernel.Blazor` (`NamedFormField`,
`ParsedTextInputState`, `FieldIds`, `ServerFormSubmission`, `ErrorSummary`, `FormActions`), so Blazor.Site itself stays free of them and
of their HTTP and resilience dependencies: a static, script-free public site takes Blazor.Site alone, an admin host adds this package.
See [the decision](../../../docs/adr/20261125-site-editing-package.md).

## Theme editor

`ThemeEditor` is the field set of the owner-editable theme (`SharedKernel.Blazor.Site.Theming`), bound to a `ThemeEditorModel`:

- Colors: a fieldset per `ThemeColorRole` with its light and dark value as text (`#rrggbb`), each with a swatch drawn as an SVG fill. Both
  values of a hover color empty means derived.
- Typography: a fieldset per `TypographyRole` with the font (the families the host offers, or the system font), the system stack after
  it, the size and the largest size (rem), the weight (`ThemeFont.Weights`), the line height, the letter spacing (em), the letter case,
  the style and, on the roles that take one (`TypographyStyle.TakesColor`: h1 to h6, the card title and the brand), the color
  (`TypographyStyle.Colors`: text, primary or muted). Then the small-text switch.
- Header: the logo height (rem).
- Shape: the corners (`ControlShape`), the divider width and thickness (rem) and the divider color (`SiteShape.DividerColors`).

Every select has an empty option that keeps the default, and every number may stay empty. Numbers are typed as text: a point or a comma
separates the decimals, the control keeps what was typed, and text that is not a number is an error on the field when the form validates
(`Line height must be a number, like 1.5.`), so it never reaches the model.

The host owns the form, the save, the preview and the font library. Render the editor inside an `EditForm` (it needs an interactive
render mode), between an `ErrorSummary` and `FormActions`:

```razor
<EditForm EditContext="_context" OnSubmit="Save" FormName="site-theme" novalidate>
    <ErrorSummary Title="There is a problem" Items="_submission.Summary" />
    <ThemeEditor Model="_model" IdPrefix="site-theme" Fonts="_fonts" Texts="_texts" OnChange="Refresh" />
    <FormActions Submission="_submission" CancelHref="site" />
</EditForm>
<ThemePreview Theme="_draft" Errors="_draftErrors" ColorLabel="_texts.ColorName" />

@code {
    private readonly ThemeEditorTexts _texts = new();
    private ThemeEditorModel _model = ThemeEditorModel.FromTheme(SiteTheme.Default);
    private EditContext _context = default!;
    private ServerFormSubmission _submission = default!;

    protected override void OnInitialized()
    {
        _context = new EditContext(_model);
        _submission = ServerFormSubmission.ForRecord(_context, ThemeEditor.Fields, "site-theme", "theme");
    }

    private Task Save() => _submission.Submit(() => themes.Save(ThemeDocument.From(_model), _texts.ColorName));
}
```

- Each control's id is `FieldIds.For(IdPrefix, path)`, such as `site-theme-typography-h2-color` for `typography.h2.color`, and its
  messages are the edit context's messages of that path. `ThemeEditor.Fields` lists every path the editor shows, in form order: the
  `colors` and `typography` sections and each color role (their fieldsets carry the id and `tabindex="-1"`, so a contrast error on
  `colors.muted` links to the color's fieldset), each value, each typography field (with `sizeMax`, and `color` only where a role takes
  one), `allowSmallText`, `header.brandHeight` and `shape.control`, `shape.dividerWidth`, `shape.dividerThickness`,
  `shape.dividerColor`. Pass it as the known fields of the `ServerFormSubmission`: every rule error of `ThemeEditorModel.ToTheme`,
  `ThemeDocument.ToTheme` or `SiteTheme.Create` then lands on its control, and the summary links to it.
- `Fonts` lists the families to offer as `ThemeFontOption(key, family)`; a role keeps a key that is not offered and shows it by its key.
  The host fills `ThemeEditorModel.Fonts` with the faces of the chosen families before it builds the theme.
- `OnChange` is raised after every change, for example to build a draft for `ThemePreview` with `ThemeEditorModel.ToDraft()`.
- `ThemeEditorTexts` holds every legend, label and help, the names of the roles and values (`ColorName`, `ColorHint`, `TypographyName`,
  `FallbackName`, `TransformName`, `ControlShapeName`) and the number message (`NotANumber`), in English by default. Pass the same
  `ColorName` to `ToTheme` and `ThemePreview.ColorLabel`, so a contrast message names the colors as the legends do.
- Accessibility: every control has a label; each color and typography role is a `fieldset` with a `legend`; help and messages are
  linked with `aria-describedby`, and a control with a message has `aria-invalid="true"`. The editor writes no style attribute and no
  script, so it works under a strict Content-Security-Policy; the swatch is `aria-hidden`.
- Class names for the host's stylesheet: `theme-editor__colors`, `theme-editor__typography`, `theme-editor__header`,
  `theme-editor__shape` (the sections), `theme-color` and `theme-role` (a role's fieldset), `theme-fields` (the controls of a role) and
  `theme-swatch`.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Blazor.Site.Editing`. C# namespace: `SharedKernel.Blazor.Site.Editing`. Assembly/project identity:
`SharedKernel.Blazor.Site.Editing`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed.
This README does not imply availability on nuget.org.
