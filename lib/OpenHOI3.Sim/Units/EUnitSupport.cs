namespace OpenHOI3.Sim.Units;

/// <summary>
/// The three things a unit's owner may switch for it in its panel, each of which the
/// day's upkeep reads. See <c>docs/mechanics/upkeep.md</c>.
/// </summary>
public enum EUnitSupport
{
	/// <summary>Seen to before the others, for reinforcement and for upgrades.</summary>
	Priority,

	/// <summary>Its brigades are brought back to strength.</summary>
	Reinforcement,

	/// <summary>Its brigades are re-equipped.</summary>
	Upgrade,
}
