namespace OpenHOI3.Sim.Units;

/// <summary>
/// What a unit is in the order of battle, by the keyword that declares it. The five
/// land ones are in order of command, highest first.
/// </summary>
public enum EUnitKind
{
	Theatre,
	ArmyGroup,
	Army,
	Corps,
	Division,
	Navy,
	Air,
}
