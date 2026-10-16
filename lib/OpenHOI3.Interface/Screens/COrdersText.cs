using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Convoys;
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

	/// <summary>What an escort's row is called: a text with the two ends of its convoy's run in it.</summary>
	public const string EscortRowKey = "ORDER_CONVOY_ESCORT_NTYPE";

	/// <summary>The localisation key of an order's name, as its row and a unit's panel have it.</summary>
	public static string NameKey(EUnitOrder order)
	{
		return order switch
		{
			EUnitOrder.Move => "MOVE_ORDER",
			EUnitOrder.Rebase => "ORDER_REBASE",
			EUnitOrder.RebaseAir => "ORDER_AIR_REBASE",
			EUnitOrder.Reserve => "ORDER_RESERVE",
			EUnitOrder.Patrol => "ORDER_PATROL",
			EUnitOrder.Intercept => "ORDER_INTERCEPT",
			EUnitOrder.Sortie => "ORDER_SORTIE",
			EUnitOrder.ConvoyEscort => "ORDER_CONVOY_ESCORT",
			EUnitOrder.ConvoyRaid => "ORDER_CONVOY_RAID",
			EUnitOrder.Transport => "ORDER_TRANSPORT",
			EUnitOrder.Invasion => "ORDER_INVADE",
			EUnitOrder.AirSuperiority => "AIRSUP",
			EUnitOrder.AirIntercept => "INTERCEPT",
			EUnitOrder.StrategicBombing => "STRAT_BOMB",
			EUnitOrder.LogisticalStrike => "LOG_BOMB",
			EUnitOrder.RunwayCratering => "AIRFIELD_BOMB",
			EUnitOrder.InstallationStrike => "INSTALLATION_BOMB",
			EUnitOrder.GroundAttack => "GROUNDATTACK_BOMB",
			EUnitOrder.Interdiction => "INTERDICT_BOMB",
			EUnitOrder.PortStrike => "PORTSTRIKE_BOMB",
			EUnitOrder.NavalStrike => "NAVALSTRIKE_BOMB",
			EUnitOrder.CarrierProtection => "CAG_DUTY",
			EUnitOrder.Paradrop => "PARADROP_MISSION",
			EUnitOrder.RebaseCarrier => "REBASE_TO_CAG_MISSION",
			EUnitOrder.AirSupply => "TRANS_SUP_MISSION",
			_ => "",
		};
	}

	/// <summary>What a row says: its order's name, and for an escort the run it is for.</summary>
	public static string RowText(System.Func<string, string> localise, COrderRow row)
	{
		return row.Convoy == 0
			? localise(row.NameKey)
			: Text.CTextTemplate.Fill(localise(row.NameKey), ("START", row.Start), ("END", row.End));
	}

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
	/// The rows for the first of several selected fleets, each greyed where any of the others may not take it - one
	/// whose own list has no such row, or has it greyed (<c>selection.md</c>, section 6). For an air unit, or one
	/// fleet, the first unit's rows as they are.
	/// </summary>
	public static List<COrderRow> Rows(CGameState state, CCountry country, IReadOnlyList<CUnit> units, CProvince province)
	{
		List<COrderRow> rows = Rows(state, country, units[0], province);
		if (units[0].Kind != EUnitKind.Navy)
		{
			return rows;
		}
		for (int other = 1; other < units.Count; other++)
		{
			List<COrderRow> theirs = Rows(state, country, units[other], province);
			for (int index = 0; index < rows.Count; index++)
			{
				COrderRow row = rows[index];
				if (row.MayGive && !theirs.Exists(their => their.Order == row.Order && their.Convoy == row.Convoy && their.MayGive))
				{
					rows[index] = row with { MayGive = false };
				}
			}
		}
		return rows;
	}

	/// <summary>
	/// The rows of the list, in the original's order. A fleet is shown every naval order
	/// there is, the ones that cannot be given with their boxes greyed. An air unit is
	/// shown only the orders it could ever fly: by what its wings can attack, what it
	/// carries, whether it is aboard a carrier here or a carrier lies here, and whether the
	/// province is land or sea. Of the air's, the raid on convoys and the reserve are
	/// listed and cannot be ticked: they are not built.
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
			Add(NameKey(EUnitOrder.Move), EUnitOrder.Move);
			Add(NameKey(EUnitOrder.Rebase), EUnitOrder.Rebase);
			Add(NameKey(EUnitOrder.Reserve), EUnitOrder.Reserve);
			Add(NameKey(EUnitOrder.Patrol), EUnitOrder.Patrol);
			Add(NameKey(EUnitOrder.Intercept), EUnitOrder.Intercept);
			Add(NameKey(EUnitOrder.Sortie), EUnitOrder.Sortie);
			Add(NameKey(EUnitOrder.ConvoyRaid), EUnitOrder.ConvoyRaid);
			Add(NameKey(EUnitOrder.Transport), EUnitOrder.Transport);
			Add(NameKey(EUnitOrder.Invasion), EUnitOrder.Invasion);
			// And an escort for each of the country's own convoys through the province.
			foreach (CConvoy convoy in country.Convoys)
			{
				if (convoy.Path.Contains(province.Id))
				{
					rows.Add(new COrderRow(EscortRowKey, EUnitOrder.ConvoyEscort,
						CUnitOrders.MayGive(state, country, unit, EUnitOrder.ConvoyEscort, province, convoy.Id),
						convoy.Id, state.Province(convoy.Start).Name, state.Province(convoy.End).Name));
				}
			}
			return rows;
		}
		if (unit.Kind != EUnitKind.Air)
		{
			return rows;
		}

		bool land = !province.IsSea;
		if (Has(unit, "air_attack"))
		{
			// Carrier protection first, for a unit based on a carrier here (transport.md, section 13.6).
			if (unit.CarriedBy is not null && unit.Base == province.Id)
			{
				Add(NameKey(EUnitOrder.CarrierProtection), EUnitOrder.CarrierProtection);
			}
			Add(NameKey(EUnitOrder.AirSuperiority), EUnitOrder.AirSuperiority);
			Add(NameKey(EUnitOrder.AirIntercept), EUnitOrder.AirIntercept);
		}
		if (land && unit.Passengers.Count > 0)
		{
			Add(NameKey(EUnitOrder.Paradrop), EUnitOrder.Paradrop);
		}
		if (HasCarrier(state, province))
		{
			Add(NameKey(EUnitOrder.RebaseCarrier), EUnitOrder.RebaseCarrier);
		}
		if (land && Has(unit, "transport_capability"))
		{
			Add(NameKey(EUnitOrder.AirSupply), EUnitOrder.AirSupply);
		}
		if (land && Has(unit, "strategic_attack"))
		{
			Add(NameKey(EUnitOrder.StrategicBombing), EUnitOrder.StrategicBombing);
			Add(NameKey(EUnitOrder.LogisticalStrike), EUnitOrder.LogisticalStrike);
			Add(NameKey(EUnitOrder.RunwayCratering), EUnitOrder.RunwayCratering);
			Add(NameKey(EUnitOrder.InstallationStrike), EUnitOrder.InstallationStrike);
		}
		if (land && (Has(unit, "soft_attack") || Has(unit, "hard_attack")))
		{
			Add(NameKey(EUnitOrder.GroundAttack), EUnitOrder.GroundAttack);
			Add(NameKey(EUnitOrder.Interdiction), EUnitOrder.Interdiction);
		}
		if (Has(unit, "sea_attack"))
		{
			if (land)
			{
				Add(NameKey(EUnitOrder.PortStrike), EUnitOrder.PortStrike);
			}
			else
			{
				Add(NameKey(EUnitOrder.NavalStrike), EUnitOrder.NavalStrike);
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

	/// <summary>Whether a ship of a fleet in the province, anybody's, is a carrier.</summary>
	private static bool HasCarrier(CGameState state, CProvince province)
	{
		foreach (CUnit fleet in state.UnitsIn(province.Id))
		{
			if (fleet.Kind != EUnitKind.Navy)
			{
				continue;
			}
			foreach (CSubUnit ship in fleet.SubUnits)
			{
				if (Sim.Combat.CShipCensus.IsCarrier(ship))
				{
					return true;
				}
			}
		}
		return false;
	}
}
