# SharedKernel Terminal.Gui

`PandaBear.SharedKernel.Terminal.Gui` supplies list, detail, and diff views for Terminal.Gui applications. It uses the
repository's exact Terminal.Gui version pin. The library defines no vault or secrets-provider dependencies.

Use `TerminalViews.CreateList` to populate a list and `TerminalViews.CreateDetail` or `TerminalViews.CreateDiff` to
display rows with `RowTableSource`. The list and tables leave type-to-select key bindings available for command
shortcuts. `ListDetailLayout.Position` positions a frame, heading, and detail view as siblings and keeps two rows
for status or command information.

Pass untrusted labels through `TerminalText.OneLine`. Use `TerminalText.Value` with masking enabled until the user
explicitly reveals the value. Masked values render without exposing their length. The caller owns reveal state and
chooses which data may be visible.

Before opening a modal, check `TerminalDialogs.Unavailable` against its minimum size and display the reason in
the window if it is too small. `TerminalDialogs.Confirm` defaults to Cancel. The caller chooses the action and
runs it only after confirmation.

Vault Manager uses these controls for review. The independent file-browser fixture in
`SharedKernel.Terminal.Gui.Tests` uses the same primitives and test-only headless driver without referencing Vault
Manager. Test collections serialize Terminal.Gui applications within each test process and use a virtual clock,
buffer-only ANSI driver, and fake clipboard.
