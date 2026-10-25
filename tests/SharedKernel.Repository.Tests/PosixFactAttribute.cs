using System.Runtime.CompilerServices;

namespace PandaBear.Repository.Tests;

/// <summary>Marks a test that starts a fake process written as a POSIX shell script; Windows skips it.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class PosixFactAttribute : FactAttribute
{
    public PosixFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "The fake process is a POSIX shell script.";
        }
    }
}
