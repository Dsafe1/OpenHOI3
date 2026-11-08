using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// The pride of the fleet: which ship is a country's, and the dissent of naming another. See
/// <c>docs/mechanics/brigadecard.md</c>, section 4.
/// </summary>
public static class CPride
{
	public const string RedesignationDissent = "PRIDE_BASE_REDESIGNATION_DISSENT";

	/// <summary>Whether a ship's type may be one.</summary>
	public const string CanBePride = "can_be_pride";

	/// <summary>The first ship marked a pride, walking the country's units as made and each fleet's ships.</summary>
	public static CSubUnit? Find(CCountry country)
	{
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (unit.Kind != EUnitKind.Navy)
			{
				continue;
			}
			foreach (CSubUnit ship in unit.SubUnits)
			{
				if (ship.IsPride)
				{
					return ship;
				}
			}
		}
		return null;
	}

	/// <summary>
	/// What naming <paramref name="newer"/> in place of <paramref name="older"/> costs: the define, cut to a share by
	/// how many more technology levels the new ship holds, and halved where its hull is the greater. Truncated each
	/// time; nothing where there is no older ship.
	/// </summary>
	public static CFixedPoint Dissent(CDefines defines, CSubUnit newer, CSubUnit? older)
	{
		if (older is null)
		{
			return CFixedPoint.Zero;
		}
		long dissent = defines[CManpower.MilitarySection, RedesignationDissent].Raw;
		long more = CUnitSupply.LevelsHeld(newer) - CUnitSupply.LevelsHeld(older);
		if (more > 0)
		{
			dissent = dissent / (more + 1);
		}
		if (newer.Stats[CNavalBattle.Hull].Raw > older.Stats[CNavalBattle.Hull].Raw)
		{
			dissent = dissent * CFixedPoint.Scale / (2 * CFixedPoint.Scale);
		}
		return new CFixedPoint(System.Math.Max(0, dissent));
	}
}
