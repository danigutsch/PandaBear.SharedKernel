using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.Time;

namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>Runs Terminal.Gui without a terminal for view tests: virtual time, the ANSI driver, and injected keys.</summary>
internal static class HeadlessTerminal
{
    /// <summary>
    /// Test collection of every Terminal.Gui test. The tests of one collection run one after another, so a test process
    /// never runs two Terminal.Gui applications at once; other collections still run in parallel.
    /// </summary>
    public const string Collection = "Terminal.Gui";

    /// <summary>Creates an initialized application that never touches the real terminal or the system clipboard.</summary>
    public static IApplication Start()
    {
        // Terminal.Gui uses the controlling terminal through /dev/tty even when standard input and output are redirected.
        // This variable keeps the driver in its buffer-only mode, so a test run never takes over the terminal it runs in.
        Environment.SetEnvironmentVariable("DisableRealDriverIO", "1");
        IApplication app = Application.Create(new VirtualTimeProvider());
        app.Init(DriverRegistry.Names.ANSI);

        // The driver otherwise copies and pastes through the system clipboard, on WSL by starting powershell.exe.
        app.Driver!.Clipboard = new FakeClipboard();
        return app;
    }

    /// <summary>
    /// Presses keys in order. Each press injects a copy, because a key is also the event that handlers mark as handled, as
    /// a terminal delivers a new key for every press.
    /// </summary>
    public static void Press(IApplication app, params Key[] keys)
    {
        foreach (Key key in keys)
        {
            app.InjectKey(new Key(key));
        }
    }

    /// <summary>Presses a key several times.</summary>
    public static void Press(IApplication app, Key key, int times)
    {
        for (int count = 0; count < times; count++)
        {
            app.InjectKey(new Key(key));
        }
    }

    /// <summary>Types text one character at a time.</summary>
    public static void Type(IApplication app, string text)
    {
        foreach (char character in text)
        {
            app.InjectKey(new Key(character));
        }
    }
}
