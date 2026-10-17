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

	/// <summary>A fleet: carry its land units to a friendly port, put them off, and go home (<c>transport.md</c>, section 7).</summary>
	Transport,

	/// <summary>A fleet: land its land units on a province from the sea beside it (<c>transport.md</c>, section 10).</summary>
	Invasion,

	/// <summary>An air unit: go round the provinces of its mission, flying at enemy air units in them.</summary>
	AirSuperiority,

	/// <summary>An air unit: wait at its base for an enemy air unit in or near the provinces of its mission, and fly to meet it.</summary>
	AirIntercept,

	// ---- the bombing missions: docs/mechanics/bombing.md

	/// <summary>An air unit: bomb the enemy's land units, those that are fighting before the others.</summary>
	GroundAttack,

	/// <summary>An air unit: bomb the enemy's land units, those on the move before the others.</summary>
	Interdiction,

	/// <summary>An air unit: bomb enemy fleets at sea.</summary>
	NavalStrike,

	/// <summary>An air unit: bomb the industry of a province and what its mines and wells yield.</summary>
	StrategicBombing,

	/// <summary>An air unit: bomb the roads of a province and the supplies and fuel lying in it.</summary>
	LogisticalStrike,

	/// <summary>An air unit: bomb an air base and the aircraft at it.</summary>
	RunwayCratering,

	/// <summary>An air unit: bomb the radar stations of a province.</summary>
	InstallationStrike,

	/// <summary>An air unit: bomb a naval base and the ships lying in it.</summary>
	PortStrike,

	// ---- carrying by air and the carrier: docs/mechanics/transport.md, section 13

	/// <summary>An air unit of carrier air groups: make a ship of a fleet of its own country its base, and fly to it.</summary>
	RebaseCarrier,

	/// <summary>An air unit aboard a carrier: keep over the carrier, fight in the air there and bomb the fleets it fights.</summary>
	CarrierProtection,

	/// <summary>An air unit: fly the land units it carries to a land province and drop them there.</summary>
	Paradrop,

	/// <summary>An air unit: carry supplies and fuel from its base to a friend's province of its area that is short of them.</summary>
	AirSupply,

	// ---- the AI's regrouping: docs/mechanics/agents.md, section 5.4
	/// <summary>A fleet: sail to another fleet and be merged into it.</summary>
	JoinFleet,

	/// <summary>An air unit: fly to another air unit and be merged into it.</summary>
	JoinAir,
}
