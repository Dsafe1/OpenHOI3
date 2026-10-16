using OpenHOI3.Sim.Combat;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// The group a unit type falls in, as the original's ration of a type counts it - the original's
/// <c>SubUnitTypeGroup</c>, answering the group's representative type. Every figure and key is the original's own. See
/// <c>reversing/findings/FINDINGS-ministers.md</c> C.5.
/// </summary>
public static class CUnitTypeGroup
{
	/// <summary>Under this softness a land brigade is in the armour's group.</summary>
	private const long ArmourSoftness = 500;

	private const string Paradrop = "can_paradrop";

	/// <summary>
	/// A ship: a carrier's group where it carries aircraft, the heavy cruiser its own, a capital ship the battleship's,
	/// a transport or a submarine its own, and any other the destroyer's. Anything else: its own where it is no land
	/// type, has no width or is the headquarters; the armour's where it is softer than a half; its own where it may
	/// drop or is the mountain or marine brigade; and the infantry's otherwise. A representative the files do not have
	/// leaves the type its own.
	/// </summary>
	public static CSubUnitType Of(CGameState state, CSubUnitType type)
	{
		string? group;
		if (type.Branch == EUnitBranch.Naval)
		{
			if (type.Stats[CShipCensus.CarrierSize].Raw > 0)
			{
				group = "carrier";
			}
			else if (type.Key == "heavy_cruiser")
			{
				group = null;
			}
			else if (type.Flag(CShipCensus.CapitalFlag))
			{
				group = "battleship";
			}
			else if (type.Flag(CShipCensus.TransportFlag) || type.Flag(CShipCensus.SubmarineFlag))
			{
				group = null;
			}
			else
			{
				group = "destroyer";
			}
		}
		else if (type.Branch != EUnitBranch.Land || type.Stats[CSubUnitType.CombatWidth].Raw <= 0 || type.Key == "hq_brigade")
		{
			group = null;
		}
		else if (type.Stats[CUnitTotals.Softness].Raw < ArmourSoftness)
		{
			group = "armor_brigade";
		}
		else if (type.Flag(Paradrop) || type.Key == "bergsjaeger_brigade" || type.Key == "marine_brigade")
		{
			group = null;
		}
		else
		{
			group = "infantry_brigade";
		}
		return group is not null && state.SubUnitTypes.TryGetValue(group, out CSubUnitType? representative)
			? representative
			: type;
	}
}
