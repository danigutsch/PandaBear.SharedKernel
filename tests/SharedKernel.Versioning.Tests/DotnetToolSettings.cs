namespace PandaBear.Versioning.Tests;

/// <summary>Writes the tool settings file that a framework-dependent .NET tool package carries.</summary>
internal static class DotnetToolSettings
{
    public const string EntryPath = "tools/net10.0/any/DotnetToolSettings.xml";

    /// <summary>Returns tool settings that declare <paramref name="commands" />, each run by <c>dotnet</c> from <c>Probe.Tool.dll</c>.</summary>
    public static string Declaring(params string[] commands) =>
        """<?xml version="1.0" encoding="utf-8"?>""" + Environment.NewLine
        + """<DotNetCliTool Version="1">""" + Environment.NewLine
        + "  <Commands>" + Environment.NewLine
        + string.Concat(commands.Select(static command => $"""    <Command Name="{command}" EntryPoint="Probe.Tool.dll" Runner="dotnet" />""" + Environment.NewLine))
        + "  </Commands>" + Environment.NewLine
        + "</DotNetCliTool>" + Environment.NewLine;
}
