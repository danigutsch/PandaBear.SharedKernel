using System.Runtime.CompilerServices;

namespace PandaBear.Repository.Tests;

/// <summary>Marks a test that starts a fake process written as a POSIX shell script; Windows skips it.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class PosixFactAttribute : FactAttribute
{
    /// <summary>Records the caller's source file and line for the test report and sets <c>Skip</c> when the tests run on Windows.</summary>
    public PosixFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "The fake process is a POSIX shell script.";
        }
    }
}
