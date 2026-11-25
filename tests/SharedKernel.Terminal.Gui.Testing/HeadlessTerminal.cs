using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.Time;

namespace SharedKernel.Terminal.Gui.Testing;

/// <summary>Runs Terminal.Gui without a terminal for view tests: virtual time, the ANSI driver, and injected keys.</summary>
public static class HeadlessTerminal
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
    /// <param name="app">The initialized headless application.</param>
    /// <param name="keys">The keys to press.</param>
    /// <exception cref="ArgumentNullException">Thrown when the application or keys are <see langword="null"/>.</exception>
    public static void Press(IApplication app, params Key[] keys)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(keys);

        foreach (Key key in keys)
        {
            app.InjectKey(new Key(key));
        }
    }

    /// <summary>Presses a key several times.</summary>
    /// <param name="app">The initialized headless application.</param>
    /// <param name="key">The key to press.</param>
    /// <param name="times">The number of presses.</param>
    /// <exception cref="ArgumentNullException">Thrown when the application or key is <see langword="null"/>.</exception>
    public static void Press(IApplication app, Key key, int times)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(key);

        for (int count = 0; count < times; count++)
        {
            app.InjectKey(new Key(key));
        }
    }

    /// <summary>Types text one character at a time.</summary>
    /// <param name="app">The initialized headless application.</param>
    /// <param name="text">The text to type.</param>
    /// <exception cref="ArgumentNullException">Thrown when the application or text is <see langword="null"/>.</exception>
    public static void Type(IApplication app, string text)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(text);

        foreach (char character in text)
        {
            app.InjectKey(new Key(character));
        }
    }
}
