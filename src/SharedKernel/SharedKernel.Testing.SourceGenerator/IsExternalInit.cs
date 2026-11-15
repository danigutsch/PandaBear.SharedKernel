namespace System.Runtime.CompilerServices;

/// <summary>
/// Enables record initialization support for the netstandard generator target.
/// </summary>
internal static class IsExternalInit
{
    internal static string Marker => nameof(IsExternalInit);
}
