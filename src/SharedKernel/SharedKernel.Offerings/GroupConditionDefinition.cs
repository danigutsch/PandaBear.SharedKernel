namespace PandaBear.Offerings;

/// <summary>Unvalidated group condition: all three values together, or none.</summary>
/// <param name="MinimumParticipants">The group size the prices assume.</param>
/// <param name="SurchargePerMissing">The surcharge per missing participant, as typed.</param>
/// <param name="DueDays">How many days before the start the group is confirmed.</param>
public sealed record GroupConditionDefinition(int? MinimumParticipants, decimal? SurchargePerMissing, int? DueDays);
