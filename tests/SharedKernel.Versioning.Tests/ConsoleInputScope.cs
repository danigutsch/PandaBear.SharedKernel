namespace SharedKernel.Versioning.Tests;

internal sealed class ConsoleInputScope : IDisposable
{
    private readonly TextReader _originalInput;
    private readonly StringReader _input;

    public ConsoleInputScope(string text)
    {
        _originalInput = Console.In;
        _input = new StringReader(text);
        Console.SetIn(_input);
    }

    public void Dispose()
    {
        Console.SetIn(_originalInput);
        _input.Dispose();
    }
}
