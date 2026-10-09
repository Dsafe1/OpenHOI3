using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the window of orders says: which orders it lists for a unit and a province,
/// which of them can be given, which is ticked when it comes up, and its two figures.
/// See <c>docs/mechanics/fleetsandair.md</c>, section 4.
/// </summary>
public static class COrdersText
{
	public const string NavalWindow = "naval_unit_order";
	public const string AirWindow = "air_unit_order";

	/// <summary>The window a row of the list is made from.</summary>
	public const string EntryWindow = "order_entry";

	public const int Aggressive = 0;
	public const int Passive = 2;
	public const int HighestPriority = 4;

	/// <summary>The window for a unit's orders, or an empty string for a unit that has none.</summary>
	public static string WindowFor(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Navy => NavalWindow,
			EUnitKind.Air => AirWindow,
			_ => "",
		};
	}

	/// <summary>
	/// The rows of the list, in the original's order. A fleet is shown every naval order
	/// there is, the ones that cannot be given with their boxes greyed. An air unit is
	/// shown only the orders it could ever fly: by what its wings can attack, and by
	/// whether the province is land or sea. Of them all only a move and a change of
	/// base are built; the rest are listed and cannot be ticked.
	/// </summary>
	public static List<COrderRow> Rows(CGameState state, CCountry country, CUnit unit, CProvince province)
	{
		var rows = new List<COrderRow>();
		void Add(string key, EUnitOrder order = EUnitOrder.None)
		{
			rows.Add(new COrderRow(key, order,
				order != EUnitOrder.None && CUnitOrders.MayGive(state, country, unit, order, province)));
		}

		if (unit.Kind == EUnitKind.Navy)
		{
			Add("MOVE_ORDER", EUnitOrder.Move);
			Add("ORDER_REBASE", EUnitOrder.Rebase);
			Add("ORDER_RESERVE");
			Add("ORDER_PATROL");
			Add("ORDER_INTERCEPT");
			Add("ORDER_SORTIE");
			Add("ORDER_CONVOY_RAID");
			Add("ORDER_TRANSPORT");
			Add("ORDER_INVADE");
			return rows;
		}
		if (unit.Kind != EUnitKind.Air)
		{
			return rows;
		}

		bool land = !province.IsSea;
		if (Has(unit, "air_attack"))
		{
			Add("AIRSUP");
			Add("INTERCEPT");
		}
		if (land && Has(unit, "transport_capability"))
		{
			Add("TRANS_SUP_MISSION");
		}
		if (land && Has(unit, "strategic_attack"))
		{
			Add("STRAT_BOMB");
			Add("LOG_BOMB");
			Add("AIRFIELD_BOMB");
			Add("INSTALLATION_BOMB");
		}
		if (land && (Has(unit, "soft_attack") || Has(unit, "hard_attack")))
		{
			Add("GROUNDATTACK_BOMB");
			Add("INTERDICT_BOMB");
		}
		if (Has(unit, "sea_attack"))
		{
			if (land)
			{
				Add("PORTSTRIKE_BOMB");
			}
			else
			{
				Add("NAVALSTRIKE_BOMB");
				Add("ORDER_AIR_CONVOY_RAID");
			}
		}
		if (land)
		{
			Add("ORDER_AIR_RESERVE");
			Add("ORDER_AIR_REBASE", EUnitOrder.RebaseAir);
		}
		return rows;
	}

	/// <summary>
	/// The row ticked when the window comes up, or -1 for none: the order the original
	/// suggests, where it is one of the rows and can be given.
	/// </summary>
	public static int Suggested(IReadOnlyList<COrderRow> rows, CGameState state, CCountry country, CUnit unit,
		CProvince province)
	{
		EUnitOrder order = CUnitOrders.Suggested(state, country, unit, province);
		if (order == EUnitOrder.None)
		{
			return -1;
		}
		for (int index = 0; index < rows.Count; index++)
		{
			if (rows[index].Order == order && rows[index].MayGive)
			{
				return index;
			}
		}
		return -1;
	}

	/// <summary>The localisation key of a stance: 0 the aggressive one, 2 the passive.</summary>
	public static string StanceKey(int stance)
	{
		return stance switch
		{
			1 => "STAN_DEFENSIVE",
			2 => "STAN_PASSIVE",
			_ => "STAN_AGRESSIVE",
		};
	}

	/// <summary>A stance a step down or up from another, going round at either end.</summary>
	public static int StepStance(int stance, int step)
	{
		return Wrap(stance + step, Passive);
	}

	/// <summary>A priority a step down or up from another, 0 to 4, going round at either end.</summary>
	public static int StepPriority(int priority, int step)
	{
		return Wrap(priority + step, HighestPriority);
	}

	private static int Wrap(int value, int highest)
	{
		return value < 0 ? highest : value > highest ? 0 : value;
	}

	/// <summary>Whether any ship or wing of a unit has something of a statistic.</summary>
	private static bool Has(CUnit unit, string statistic)
	{
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (subUnit.Stats[statistic] > CFixedPoint.Zero)
			{
				return true;
			}
		}
		return false;
	}
}
