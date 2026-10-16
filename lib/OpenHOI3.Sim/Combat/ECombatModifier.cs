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

	/// <summary>A unit dropped from the air, attacking (<c>transport.md</c>, section 13.4).</summary>
	Paratroop,

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

	/// <summary>The unit's leader: his skill, and the traits for attacking or defending and for the ground.</summary>
	Leader,

	Envelopment,
	TerritorialPride,

	/// <summary>At sea: a capital ship with too few screens about it.</summary>
	PoorScreens,

	/// <summary>At sea: an attacking submarine that was not seen coming.</summary>
	Surprise,

	/// <summary>How good the unit's country is at the mission it is on.</summary>
	MissionEfficiency,

	/// <summary>In the air: fighting in or beside the province of the unit's base.</summary>
	BaseProximity,

	/// <summary>In the air: a wing on an intercept, or on air superiority.</summary>
	Intercept,

	/// <summary>A bomber: what being in an air battle of its own costs its bombing.</summary>
	AirCombat,

	/// <summary>What its country's radio technology gives a unit.</summary>
	Radio,

	/// <summary>In the air: a friend's radar station in the province or beside it.</summary>
	Radar,
}
