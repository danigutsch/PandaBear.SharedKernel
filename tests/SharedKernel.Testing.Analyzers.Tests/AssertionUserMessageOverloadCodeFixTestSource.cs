namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Builds source files for the user-message overload code fix tests, together with the WithUserMessage helper they forward to.</summary>
internal static class AssertionUserMessageOverloadCodeFixTestSource
{
    /// <summary>Source of UserMessageAssertions with WithUserMessage overloads for Action, Func of T, Func of Task and Func of Task of T.</summary>
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
    /// <summary>Source of UserMessageAssertions with only the Action and Func of T WithUserMessage overloads, so task-returning lambdas bind to Func of T.</summary>
    public const string HelperWithoutTaskShapes = """
        internal static class UserMessageAssertions
        {
            public static void WithUserMessage(string userMessage, Action assertion) => assertion();

            public static T WithUserMessage<T>(string userMessage, Func<T> assertion) => assertion();
        }
        """;

    // Members are written without indentation; each of their lines is indented one level inside the sample class.
    /// <summary>Returns a source file with a documented SampleAssertions class holding the members, indented one level, plus the helper and a using static import for it when the helper is not empty.</summary>
    /// <param name="members">Unindented member declarations placed inside SampleAssertions.</param>
    /// <param name="helper">Helper source appended to the file, or an empty string for none.</param>
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
