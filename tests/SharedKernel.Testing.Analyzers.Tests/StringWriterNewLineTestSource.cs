namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Builds sources that create a text writer, for the SKTEST022 tests.</summary>
internal static class StringWriterNewLineTestSource
{
    /// <summary>Returns a class whose method has a string builder in scope, creates the writer and returns it.</summary>
    /// <param name="creation">The expression that creates the writer.</param>
    /// <returns>The source.</returns>
    public static string Create(string creation) => $$"""
        using System.Globalization;
        using System.Text;

        namespace Demo
        {
            public sealed class Capture
            {
                public TextWriter Run(StringBuilder builder)
                {
                    TextWriter writer = {{creation}};
                    return writer;
                }
            }

            public sealed class ConsoleCapture : StringWriter
            {
            }
        }
        """;
}
