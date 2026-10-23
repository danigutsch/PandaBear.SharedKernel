namespace PandaBear.Versioning.Tests;

/// <summary>
/// Standard input that cancels the invocation and then fails, as a process that Ctrl+C also stops can fail before the
/// invocation observes its token.
/// </summary>
internal sealed class CancelingTextReader(CancellationTokenSource cancellation, Exception failure) : TextReader
{
    public override Task<string> ReadToEndAsync() => ReadToEndAsync(CancellationToken.None);

    public override async Task<string> ReadToEndAsync(CancellationToken cancellationToken)
    {
        await cancellation.CancelAsync();
        throw failure;
    }
}
