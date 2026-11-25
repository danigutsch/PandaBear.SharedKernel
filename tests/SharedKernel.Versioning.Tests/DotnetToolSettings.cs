namespace SharedKernel.Versioning.Tests;

/// <summary>Writes the tool settings file that a framework-dependent .NET tool package carries.</summary>
internal static class DotnetToolSettings
{
    /// <summary>
    /// Path of the tool settings file inside a net10.0 .NET tool package.
    /// </summary>
    public const string EntryPath = "tools/net10.0/any/DotnetToolSettings.xml";

    /// <summary>Returns the path of the tool settings file inside a .NET tool package for <paramref name="framework" />, such as <c>net11.0</c>.</summary>
    public static string EntryPathFor(string framework) => $"tools/{framework}/any/DotnetToolSettings.xml";

    /// <summary>Returns tool settings that declare <paramref name="commands" />, each run by <c>dotnet</c> from <c>Probe.Tool.dll</c>.</summary>
    public static string Declaring(params string[] commands) =>
        """<?xml version="1.0" encoding="utf-8"?>""" + Environment.NewLine
        + """<DotNetCliTool Version="1">""" + Environment.NewLine
        + "  <Commands>" + Environment.NewLine
        + string.Concat(commands.Select(static command => $"""    <Command Name="{command}" EntryPoint="Probe.Tool.dll" Runner="dotnet" />""" + Environment.NewLine))
        + "  </Commands>" + Environment.NewLine
        + "</DotNetCliTool>" + Environment.NewLine;
}
