namespace PandaBear.Testing.Analyzers.Tests;

internal static class AssertionUserMessageOverloadCodeFixTestSource
{
    public const string Helper = """
        internal static class UserMessageAssertions
        {
            public static void WithUserMessage(string userMessage, Action assertion) => assertion();

            public static T WithUserMessage<T>(string userMessage, Func<T> assertion) => assertion();

            public static Task WithUserMessage(string userMessage, Func<Task> assertion) => assertion();

            public static Task<T> WithUserMessage<T>(string userMessage, Func<Task<T>> assertion) => assertion();
        }
        """;

    // Without the Func<Task> and Func<Task<T>> shapes, a lambda that returns a task binds to Func<T>.
    public const string HelperWithoutTaskShapes = """
        internal static class UserMessageAssertions
        {
            public static void WithUserMessage(string userMessage, Action assertion) => assertion();

            public static T WithUserMessage<T>(string userMessage, Func<T> assertion) => assertion();
        }
        """;

    // Members are written without indentation; each of their lines is indented one level inside the sample class.
    public static string Create(string members, string helper = Helper)
    {
        var helperImport = helper.Length == 0 ? string.Empty : "using static Sample.UserMessageAssertions;";
        var indentedMembers = string.Join(
            "\n",
            members.ReplaceLineEndings("\n").Split('\n').Select(static line => line.Length == 0 ? line : "    " + line));

        return $$"""
            #nullable enable
            using System;
            using System.Collections.Generic;
            using System.Diagnostics.CodeAnalysis;
            using System.Threading;
            using System.Threading.Tasks;
            {{helperImport}}

            namespace Sample;

            /// <summary>Sample assertions.</summary>
            public static class SampleAssertions
            {
            {{indentedMembers}}
            }

            {{helper}}

            """;
    }
}
