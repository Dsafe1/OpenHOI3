using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// A battle that is over, as the list of finished battles keeps it: the hour it ended, its
/// kind, its province, the first country still in the fight on each side - none for a
/// side that was beaten - and whether it was a raid on a convoy. See <c>docs/mechanics/airmap.md</c>, section 2.
/// </summary>
public sealed record CFinishedCombat(long Hour, ECombatKind Kind, int Province, CCountryTag? Attacker, CCountryTag? Defender,
	bool IsConvoy = false);
