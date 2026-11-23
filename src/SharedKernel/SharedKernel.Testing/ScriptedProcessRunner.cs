using System.Diagnostics;
using PandaBear.Processes;

namespace PandaBear.Testing;

/// <summary>
/// An <see cref="IProcessRunner"/> that answers every run from a script instead of starting a process, and records each run
/// in <see cref="Runs"/>. The constructor takes the answer as a delegate; the factories cover a fixed output, a failure,
/// outputs in run order, and outputs routed by executable.
/// </summary>
public sealed class ScriptedProcessRunner : IProcessRunner
{
    private readonly Func<ProcessStartInfo, TextWriter?, CancellationToken, Task<ProcessOutput>> _respond;
    private readonly List<RecordedProcessRun> _runs = [];
    private readonly Lock _gate = new();

    /// <summary>Creates a runner that hands each run to <paramref name="respond"/>.</summary>
    /// <param name="respond">
    /// Answers a run from its start information, the writer its output goes to, and its token. It is called synchronously, so an
    /// exception it throws reaches the caller of <see cref="Run"/> directly.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="respond"/> is <see langword="null"/>.</exception>
    public ScriptedProcessRunner(Func<ProcessStartInfo, TextWriter?, CancellationToken, Task<ProcessOutput>> respond)
    {
        ArgumentNullException.ThrowIfNull(respond);
        _respond = respond;
    }

    /// <summary>Gets every run so far, in the order the runs started.</summary>
    public IReadOnlyList<RecordedProcessRun> Runs
    {
        get
        {
            lock (_gate)
            {
                return [.. _runs];
            }
        }
    }

    /// <summary>Creates a runner that answers every run with <paramref name="output"/>.</summary>
    /// <param name="output">The output of every run.</param>
    /// <returns>The runner.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public static ScriptedProcessRunner Returning(ProcessOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new((_, _, _) => Task.FromResult(output));
    }

    /// <summary>Creates a runner that answers each run with what <paramref name="answer"/> returns for its executable and arguments.</summary>
    /// <param name="answer">Produces the output of a run from the run; it may throw, and the exception then reaches the caller directly.</param>
    /// <returns>The runner.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="answer"/> is <see langword="null"/>.</exception>
    public static ScriptedProcessRunner Answering(Func<RecordedProcessRun, ProcessOutput> answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        return new((startInfo, forward, _) => Task.FromResult(answer(new RecordedProcessRun(startInfo, forward))));
    }

    /// <summary>Creates a runner whose every run throws <paramref name="exception"/> synchronously, as a runner that cannot start the process does.</summary>
    /// <param name="exception">The exception every run throws.</param>
    /// <returns>The runner.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    public static ScriptedProcessRunner Throwing(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new((_, _, _) => throw exception);
    }

    /// <summary>Creates a runner that answers the first run with the first output, the second run with the second, and so on.</summary>
    /// <param name="outputs">The output of each run, in run order.</param>
    /// <returns>The runner. A run after the last output throws <see cref="InvalidOperationException"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="outputs"/> is <see langword="null"/>.</exception>
    public static ScriptedProcessRunner InOrder(params ProcessOutput[] outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        return AnswersInOrder([.. outputs.Select(static output => (Func<ProcessStartInfo, CancellationToken, ProcessOutput>)((_, _) => output))]);
    }

    /// <summary>Creates a runner that answers the first run with the first answer, the second run with the second, and so on.</summary>
    /// <param name="answers">Produce the output of each run, in run order, from its start information and token; an answer may throw.</param>
    /// <returns>The runner. A run after the last answer throws <see cref="InvalidOperationException"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="answers"/> is <see langword="null"/>.</exception>
    public static ScriptedProcessRunner AnswersInOrder(params Func<ProcessStartInfo, CancellationToken, ProcessOutput>[] answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        Func<ProcessStartInfo, CancellationToken, ProcessOutput>[] script = [.. answers];
        var next = 0;
        return new((startInfo, _, cancellationToken) =>
        {
            var index = Interlocked.Increment(ref next) - 1;
            return index < script.Length
                ? Task.FromResult(script[index](startInfo, cancellationToken))
                : throw new InvalidOperationException($"Run {index + 1} of {startInfo.FileName} has no scripted answer; the script has {script.Length}.");
        });
    }

    /// <summary>Creates a runner that answers each run with the output scripted for its executable.</summary>
    /// <param name="outputs">The output for each executable path, compared ordinally with <see cref="ProcessStartInfo.FileName"/>.</param>
    /// <returns>The runner. A run of an executable without an output throws <see cref="KeyNotFoundException"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="outputs"/> is <see langword="null"/>.</exception>
    public static ScriptedProcessRunner ByFileName(IEnumerable<KeyValuePair<string, ProcessOutput>> outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        var byFileName = new Dictionary<string, ProcessOutput>(outputs, StringComparer.Ordinal);
        return new((startInfo, _, _) => byFileName.TryGetValue(startInfo.FileName, out var output)
            ? Task.FromResult(output)
            : throw new KeyNotFoundException($"No output is scripted for {startInfo.FileName}."));
    }

    /// <summary>Records the run and returns the scripted answer.</summary>
    /// <param name="startInfo">The process that the code under test would start.</param>
    /// <param name="forward">The writer the output would go to, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token of the run, passed to the script.</param>
    /// <returns>The scripted output.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="startInfo"/> is <see langword="null"/>.</exception>
    public Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        lock (_gate)
        {
            _runs.Add(new RecordedProcessRun(startInfo, forward));
        }

        return _respond(startInfo, forward, cancellationToken);
    }
}
