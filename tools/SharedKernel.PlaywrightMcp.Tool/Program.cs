using System.Runtime.InteropServices;
using SharedKernel.PlaywrightMcp.Tool;

using var cancellation = new CancellationTokenSource();
void CancelHandler(object? sender, ConsoleCancelEventArgs eventArgs)
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
}
Console.CancelKeyPress += CancelHandler;

using var terminationSignal = OperatingSystem.IsWindows()
    ? null
    : PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
    {
        context.Cancel = true;
        cancellation.Cancel();
    });

try
{
    return await PlaywrightMcpApplication.Run(args, Console.Error, cancellation.Token).ConfigureAwait(false);
}
finally
{
    Console.CancelKeyPress -= CancelHandler;
}
