using System;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CUnitList</c>, a country's units at every level - its army groups, armies and corps as well as its divisions, its
/// fleets and its air units - and the original's counts over them, each asking a unit only its kind and its own brigades,
/// ships or wings. See <c>docs/mechanics/lua.md</c>, section 4.6, and <c>reversing/findings/FINDINGS-luaapi.md</c>, section 11.
/// </summary>
public static class CUnitListClass
{
	public const string Name = "CUnitList";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("GetTotalAmountOfDivisions", call => call.Return(Count(Of(call), IsLand, unit => unit.SubUnits.Count > 1 ? 1 : 0)))
			.Method("GetTotalAmountOfArmies", call => call.Return(Count(Of(call), IsLand, _ => 1)))
			.Method("GetTotalNumOfRegiments", call => call.Return(Count(Of(call), IsLand, unit => unit.SubUnits.Count)))
			.Method("GetTotalNumOfShips", call => call.Return(Count(Of(call), IsNaval, unit => unit.SubUnits.Count)))
			.Method("GetTotalNumOfPlanes", call => call.Return(Count(Of(call), IsAir, unit => unit.SubUnits.Count)))
			.Method("GetTotalNumOfWarShips", call => call.Return(Count(Of(call), IsNaval, unit => Ships(unit, transports: false))))
			.Method("GetTotalNumOfTransports", call => call.Return(Count(Of(call), IsNaval, unit => Ships(unit, transports: true))))
			.Method("GetCount", call =>
			{
				CSubUnitType type = call.Object<CSubUnitType>(2, CLicenceActionClass.SubUnitName);
				return call.Return(Count(Of(call), _ => true, unit => unit.SubUnits.FindAll(each => each.Type == type).Count));
			})
			.Method("GetTotalStrength", call =>
			{
				long strength = 0;
				foreach (CUnit unit in Of(call).Country.AllUnits())
				{
					foreach (CSubUnit subUnit in unit.SubUnits)
					{
						strength += subUnit.Strength.Raw;
					}
				}
				return call.Return(api.Fixed(new CFixedPoint((int)strength)));
			});
	}

	public static bool IsLand(CUnit unit) => unit.Kind <= EUnitKind.Division;

	public static bool IsNaval(CUnit unit) => unit.Kind == EUnitKind.Navy;

	public static bool IsAir(CUnit unit) => unit.Kind == EUnitKind.Air;

	/// <summary>A sum over the list's units of the kind asked: what each gives.</summary>
	public static int Count(CLuaUnitList list, Predicate<CUnit> kind, Func<CUnit, int> gives)
	{
		int count = 0;
		foreach (CUnit unit in list.Country.AllUnits())
		{
			if (kind(unit))
			{
				count += gives(unit);
			}
		}
		return count;
	}

	/// <summary>A fleet's ships that are naval, and transports or not as asked.</summary>
	private static int Ships(CUnit fleet, bool transports)
	{
		int count = 0;
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			if (ship.Type.Branch == EUnitBranch.Naval && Sim.Combat.CShipCensus.IsTransport(ship) == transports)
			{
				count++;
			}
		}
		return count;
	}

	private static CLuaUnitList Of(CLuaCall call) => call.Object<CLuaUnitList>(1, Name);
}
