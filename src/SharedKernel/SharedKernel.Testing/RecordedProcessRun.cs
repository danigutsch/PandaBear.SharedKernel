using System.Diagnostics;

namespace PandaBear.Testing;

/// <summary>One run that a <see cref="ScriptedProcessRunner"/> answered instead of starting a process.</summary>
public sealed class RecordedProcessRun
{
    /// <summary>Records a run, copying the file name and arguments as they were when the run started.</summary>
    /// <param name="startInfo">The start information of the run.</param>
    /// <param name="forward">The writer the run forwarded its output to, or <see langword="null"/>.</param>
    internal RecordedProcessRun(ProcessStartInfo startInfo, TextWriter? forward)
    {
        StartInfo = startInfo;
        Forward = forward;
        FileName = startInfo.FileName;
        Arguments = [.. startInfo.ArgumentList];
    }

    /// <summary>Gets the start information that the code under test passed.</summary>
    public ProcessStartInfo StartInfo { get; }

    /// <summary>Gets the writer that the run forwarded its output to, or <see langword="null"/> when it collected the output.</summary>
    public TextWriter? Forward { get; }

    /// <summary>Gets the executable that the run started.</summary>
    public string FileName { get; }

    /// <summary>Gets the arguments that the run passed.</summary>
    public IReadOnlyList<string> Arguments { get; }
}
