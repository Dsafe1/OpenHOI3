namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What a row of the list in the window of an air battle or a bombing stands for. See
/// <c>docs/mechanics/combatview.md</c>, section 11.
/// </summary>
public enum ECombatRowKind
{
	/// <summary>A wing, or under bombs a regiment or a ship.</summary>
	SubUnit,

	/// <summary>A building of the province that the bombers' mission bombs.</summary>
	Building,

	/// <summary>What lies in the province of a good whose stockpile the mission bombs.</summary>
	GoodHeld,

	/// <summary>What the province makes of a good whose production the mission bombs.</summary>
	GoodMade,
}
