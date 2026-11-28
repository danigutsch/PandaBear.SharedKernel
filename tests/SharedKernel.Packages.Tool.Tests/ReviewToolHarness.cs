using System.CommandLine;
using SharedKernel.Terminal.Gui.Testing;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace SharedKernel.Packages.Tool.Tests;

/// <summary>Creates review observations and invokes the tool through fake ports.</summary>
internal static class ReviewToolHarness
{
    /// <summary>Runs the parser and captures output without real executables, terminals, or network sources.</summary>
    internal static async Task<(int ExitCode, string Output, string Error)> Run(
        string root,
        IProcessRunner runner,
        PackageReviewTerminal terminal,
        Func<UpdateSources> sources,
        params string[] arguments)
    {
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        var exitCode = await PackagesCommandLine.Create(root, new ExecutableMap(PackagesToolHarness.ResolveAll), runner, sources, terminal)
            .Parse(arguments)
            .InvokeAsync(new InvocationConfiguration { Output = output, Error = error, ProcessTerminationTimeout = null }, TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>Fails when the command tries to create unexpected update sources.</summary>
    internal static UpdateSources RefuseNetwork() => throw new InvalidOperationException("Unexpected update sources.");

    /// <summary>Scripts observations of the Apt and Cargo packages used by the editor fixtures.</summary>
    internal static ScriptedProcessRunner AptAndCargo() => ScriptedProcessRunner.ByFileName(new Dictionary<string, ProcessOutput>
    {
        ["/fake/dpkg-query"] = new(0, "git\t2.43.0\tii \n"),
        ["/fake/cargo"] = new(0, UpdatesToolHarness.CargoList),
    });

    /// <summary>Observes a fixture declaration and an optional fake update report.</summary>
    internal static async Task<PackageReviewSession> Observe(
        string root, IProcessRunner runner, bool includeUpdates, Func<string, string?>? resolve = null)
    {
        var token = TestContext.Current.CancellationToken;
        var declaration = await PackagesConfiguration.Read(root, token);
        var inventory = new PackageInventory(runner, new ExecutableMap(resolve ?? PackagesToolHarness.ResolveAll));
        var plan = await inventory.Plan(declaration.Packages, token);
        var updates = includeUpdates
            ? await inventory.CheckUpdates(declaration.Packages,
                declaration.MinimumReleaseAge ?? throw new InvalidOperationException("The fixture requires a release age."),
                NetworkAccess.Allowed, UpdatesToolHarness.Sources().ToSources, token)
            : null;
        return new PackageReviewSession(declaration.Packages, plan, updates);
    }

    /// <summary>Edits the selected manager through the headless view and records the confirmation boundary.</summary>
    internal static (bool EditorOpened, bool ConfirmationOpened, string? ContentsAtConfirmation) Edit(
        PackageReviewSession session, string configPath, bool confirm, int managerOffset = 1, string replacement = "ripgrep=14.1.0")
    {
        using IApplication app = HeadlessTerminal.Start();
        app.Driver.ShouldNotBeNull().SetScreenSize(80, 24);
        using var window = new PackageReviewWindow(session);
        var token = app.Begin(window).ShouldNotBeNull();
        try
        {
            HeadlessTerminal.Press(app, Key.CursorDown, managerOffset);
            HeadlessTerminal.Press(app, Key.F2);
            var editor = window.SubViews.OfType<TextField>().SingleOrDefault();
            if (editor is null)
            {
                return (false, false, null);
            }

            editor.Text = replacement;
            var confirmationOpened = false;
            string? contentsAtConfirmation = null;
            app.Iteration += (_, _) =>
            {
                if (app.TopRunnable is not Dialog dialog)
                {
                    return;
                }

                if (confirmationOpened)
                {
                    app.RequestStop(dialog);
                    return;
                }

                confirmationOpened = true;
                contentsAtConfirmation = File.ReadAllText(configPath);
                if (confirm)
                {
                    dialog.Buttons[0].SetFocus();
                }

                HeadlessTerminal.Press(app, Key.Enter);
            };
            HeadlessTerminal.Press(app, Key.S.WithCtrl);
            return (true, confirmationOpened, contentsAtConfirmation);
        }
        finally
        {
            app.End(token);
        }
    }

    /// <summary>Exercises save and escape with a draft that must not open a confirmation dialog.</summary>
    internal static (bool DraftKept, bool Reset, bool Confirmation, string Status) CheckUnconfirmedEdit(
        PackageReviewSession session, string replacement, int width)
    {
        using IApplication app = HeadlessTerminal.Start();
        app.Driver.ShouldNotBeNull().SetScreenSize(width, 24);
        using var window = new PackageReviewWindow(session);
        var token = app.Begin(window).ShouldNotBeNull();
        try
        {
            var confirmation = false;
            app.Iteration += (_, _) =>
            {
                if (app.TopRunnable is Dialog dialog)
                {
                    confirmation = true;
                    app.RequestStop(dialog);
                }
            };
            HeadlessTerminal.Press(app, Key.S.WithCtrl, Key.CursorDown, Key.F2);
            var firstEditor = window.SubViews.OfType<TextField>().Single();
            firstEditor.Text = replacement;
            HeadlessTerminal.Press(app, Key.F2);
            var retainedDraft = window.SubViews.OfType<TextField>().Single();
            var draftKept = ReferenceEquals(firstEditor, retainedDraft) && retainedDraft.Text == replacement;
            HeadlessTerminal.Press(app, Key.S.WithCtrl);
            var status = string.Join('\n', window.SubViews.OfType<Label>().Select(static label => label.Text));
            HeadlessTerminal.Press(app, Key.Esc);
            var reset = !window.SubViews.OfType<TextField>().Any();
            return (draftKept, reset, confirmation, status);
        }
        finally
        {
            app.End(token);
        }
    }
}
