using PandaBear.RepoConfig.Tool;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += Cancel;
try
{
    return await RepoConfigToolApplication.Run(args, Console.Out, Console.Error, Environment.CurrentDirectory, cancellation.Token).ConfigureAwait(false);
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    await Console.Error.WriteLineAsync("sharedkernel-repo: Command canceled.").ConfigureAwait(false);
    return 3;
}
finally
{
    Console.CancelKeyPress -= Cancel;
}

void Cancel(object? sender, ConsoleCancelEventArgs eventArgs)
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
}
