using SharedKernel.BuildingBlocks;

namespace SharedKernel.Offerings;

/// <summary>
/// The minimum-group condition of an offering's prices: the prices hold for at least <paramref name="MinimumParticipants"/>, and each missing
/// participant adds <paramref name="SurchargePerMissing"/> per person, confirmed <paramref name="DueDays"/> days before the start.
/// </summary>
/// <param name="MinimumParticipants">The group size the prices assume.</param>
/// <param name="SurchargePerMissing">The surcharge per missing participant, in the offering's currency.</param>
/// <param name="DueDays">How many days before the start the group is confirmed.</param>
public sealed record GroupCondition(int MinimumParticipants, Money SurchargePerMissing, int DueDays);
