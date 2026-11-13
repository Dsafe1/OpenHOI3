using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The order the original keeps a province's units on the map in, which decides which counter of a stack is on top: the
/// player's own first, then in a land province land units before fleets and at sea fleets before the rest, then the
/// higher rank first - a theatre before an army group and down to a division, then a fleet, then an air unit - and an
/// air unit before a land unit carried by air. The first of the list is on top of its stack, until a unit is selected:
/// then its place starts the list (<c>docs/mechanics/mapunits.md</c>, section 3).
/// </summary>
public static class CProvinceUnitOrder
{
	/// <summary>
	/// A province's units in the original's order. Of two it does not part, the one that came into the province first
	/// stands first; this game keeps no such list, so the one made first does (<c>mapunits.md</c>, section 3, *Ours*).
	/// </summary>
	public static List<CUnit> Sort(IEnumerable<CUnit> units, CCountryTag? player, bool landProvince)
	{
		return units.OrderBy(unit => unit, Comparer<CUnit>.Create((a, b) =>
				Before(a, b, player, landProvince) ? -1 : Before(b, a, player, landProvince) ? 1 : 0))
			.ThenBy(unit => unit.Id)
			.ToList();
	}

	/// <summary>
	/// Whether the original puts <paramref name="left"/> before <paramref name="right"/>, its comparison step for step for
	/// the units that have a counter - an air unit at its base, a fleet in port and a passenger in port being on no stack.
	/// </summary>
	public static bool Before(CUnit left, CUnit right, CCountryTag? player, bool landProvince)
	{
		if (player is { } played)
		{
			bool leftOwn = left.Owner == played;
			bool rightOwn = right.Owner == played;
			if (leftOwn != rightOwn)
			{
				return leftOwn;
			}
		}
		bool leftAir = left.Kind == EUnitKind.Air;
		bool rightAir = right.Kind == EUnitKind.Air;
		if (leftAir && IsLand(right))
		{
			// An air unit before a land unit carried by air; otherwise by rank.
			return right.Plane is not null || Rank(left) < Rank(right);
		}
		if (IsLand(left) && rightAir)
		{
			return left.Plane is null && Rank(left) < Rank(right);
		}
		bool leftNaval = left.Kind == EUnitKind.Navy;
		bool rightNaval = right.Kind == EUnitKind.Navy;
		if (leftNaval != rightNaval)
		{
			return landProvince ? rightNaval : leftNaval;
		}
		return Rank(left) < Rank(right);
	}

	/// <summary>The original's level of a unit in its order of battle: 0 a theatre to 4 a division, 5 a fleet, 6 an air unit.</summary>
	public static int Rank(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Theatre => 0,
			EUnitKind.ArmyGroup => 1,
			EUnitKind.Army => 2,
			EUnitKind.Corps => 3,
			EUnitKind.Division => 4,
			EUnitKind.Navy => 5,
			EUnitKind.Air => 6,
			_ => 7,
		};
	}

	private static bool IsLand(CUnit unit)
	{
		return unit.Kind is not (EUnitKind.Navy or EUnitKind.Air);
	}
}
