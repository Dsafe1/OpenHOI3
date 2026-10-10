namespace OpenHOI3.Sim.Combat;

/// <summary>
/// The things an hour of a battle can hold for or against a unit, by the names the
/// original's texts give them (<c>BM_TERRAIN</c> and the rest). The order is the order
/// they are worked out in, which matters only to the last thousandth: each is
/// multiplied in and cut. See <c>docs/mechanics/landcombat.md</c>, section 4.
/// </summary>
public enum ECombatModifier
{
	River,
	Amphibious,
	Fort,
	MultipleCombats,
	Experience,
	CombinedArms,
	Dissent,
	LackOfSupplies,
	DugIn,
	Stacking,
	Terrain,
	Weather,
	Night,
	Envelopment,
	TerritorialPride,

	/// <summary>At sea: a capital ship with too few screens about it.</summary>
	PoorScreens,

	/// <summary>At sea: an attacking submarine that was not seen coming.</summary>
	Surprise,

	/// <summary>How good the unit's country is at the mission it is on.</summary>
	MissionEfficiency,
}
