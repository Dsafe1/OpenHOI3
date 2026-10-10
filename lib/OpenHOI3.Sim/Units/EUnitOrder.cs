namespace OpenHOI3.Sim.Units;

/// <summary>
/// What a unit has been told to do. The original has some forty orders; these are the
/// ones that only send a unit somewhere, and a fleet's missions. See
/// <c>docs/mechanics/fleetsandair.md</c>, section 5, and
/// <c>docs/mechanics/navalmissions.md</c>.
/// </summary>
public enum EUnitOrder
{
	/// <summary>Nothing. A unit with a path and no order is simply on its way.</summary>
	None,

	/// <summary>Go to a province and stay.</summary>
	Move,

	/// <summary>A fleet: make a port its base and go there.</summary>
	Rebase,

	/// <summary>An air unit: make an airfield its base and go there.</summary>
	RebaseAir,

	/// <summary>A fleet: lie in a port and give its sound ships for the worn ones of the fleets beside it.</summary>
	Reserve,

	/// <summary>A fleet: go round a sea province and the ones about it, and shadow what it sights.</summary>
	Patrol,

	/// <summary>A fleet: wait in port for an enemy fleet in a province or beside it, and go for it.</summary>
	Intercept,

	/// <summary>A fleet: sail to a province and home again, going for enemy fleets beside the way.</summary>
	Sortie,

	/// <summary>A fleet: sail up and down the part of a convoy's route within its range.</summary>
	ConvoyEscort,

	/// <summary>A fleet: go round a sea province and the ones about it, sinking convoys.</summary>
	ConvoyRaid,
}
