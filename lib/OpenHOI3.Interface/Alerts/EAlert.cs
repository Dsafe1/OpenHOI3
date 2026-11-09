namespace OpenHOI3.Interface.Alerts;

/// <summary>The sixteen alerts, by the original's numbers; an icon's frame is its number and one.</summary>
public enum EAlert
{
	NapExpiry = 0,
	DeployableUnits = 1,
	TroopsInForeignTerritory = 2,

	/// <summary>Worked out in the original and never shown (<c>docs/mechanics/alerts.md</c>, section 3).</summary>
	RevoltRisk = 3,

	CanInviteToFaction = 4,
	ResourcePanic = 5,
	PossibleLaw = 6,
	FreeTechSlots = 7,
	WastedIc = 8,
	TechFarAhead = 9,
	LackOfTroops = 10,
	BadSupply = 11,
	NationalDecisions = 12,
	MobilizedInPeace = 13,
	VictoryConditions = 14,
	AlliedObjectives = 15,
}
