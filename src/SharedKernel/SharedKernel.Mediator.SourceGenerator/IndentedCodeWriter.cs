using System.Text;

namespace SharedKernel.Mediator.SourceGenerator;

internal sealed class IndentedCodeWriter
{
    private readonly StringBuilder _builder = new();
    private int _indent;

    public void Line(string text = "")
    {
        if (text.Length > 0)
        {
            _builder.Append(' ', _indent * 4);
        }

        _builder.AppendLine(text);
    }

    public IDisposable Block(string header)
    {
        Line(header);
        Line("{");
        _indent++;
        return new Scope(this);
    }

    public void Indent() => _indent++;

    public void Unindent() => _indent = Math.Max(0, _indent - 1);

    public override string ToString() => _builder.ToString();

    private sealed class Scope(IndentedCodeWriter writer) : IDisposable
    {
        public void Dispose()
        {
            writer._indent = Math.Max(0, writer._indent - 1);
            writer.Line("}");
        }
    }
}
