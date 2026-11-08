namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Business time zones used by the time provider tests.</summary>
internal static class BusinessTimeZones
{
    /// <summary>Gets the America/Sao_Paulo zone, or a fixed UTC-3 zone when the machine does not know it.</summary>
    /// <returns>The zone.</returns>
    public static TimeZoneInfo SaoPaulo()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("America/Sao_Paulo", TimeSpan.FromHours(-3), "Sao Paulo", "Sao Paulo");
        }
    }
}
