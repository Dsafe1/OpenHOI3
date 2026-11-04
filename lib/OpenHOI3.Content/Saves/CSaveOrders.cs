using System;
using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A unit's order as a save writes it - a block under the order's own key - read onto the unit as the original's
/// <c>COrder</c> family reads it: the province, the target, the stance, the two dates, return-to-base, the time, the
/// priority, and each kind's own tail. Nothing is started again: the order goes on from where the save has it. See
/// <c>docs/mechanics/savegame.md</c>, section 5.
/// </summary>
public static class CSaveOrders
{
	/// <summary>Every order a save may name, by its key; null where nothing here is that order.</summary>
	public static readonly IReadOnlyDictionary<string, EUnitOrder?> Kinds = new Dictionary<string, EUnitOrder?>(StringComparer.Ordinal)
	{
		["support_attack"] = EUnitOrder.SupportAttack,
		["strategic_redeployment"] = EUnitOrder.StrategicRedeployment,
		["move_order"] = EUnitOrder.Move,
		["rebase"] = EUnitOrder.Rebase,
		["reserves"] = EUnitOrder.Reserve,
		["patrol"] = EUnitOrder.Patrol,
		["intercept"] = EUnitOrder.Intercept,
		["sortie"] = EUnitOrder.Sortie,
		["convoy_escort"] = EUnitOrder.ConvoyEscort,
		["convoy_raid"] = EUnitOrder.ConvoyRaid,
		["air_convoy_raid"] = null,
		["transport"] = EUnitOrder.Transport,
		["invasion"] = EUnitOrder.Invasion,
		["strategic_bomb"] = EUnitOrder.StrategicBombing,
		["logistical_strike"] = EUnitOrder.LogisticalStrike,
		["runway_cratering"] = EUnitOrder.RunwayCratering,
		["installation_strike"] = EUnitOrder.InstallationStrike,
		["ground_attack"] = EUnitOrder.GroundAttack,
		["interdiction"] = EUnitOrder.Interdiction,
		["air_intercept"] = EUnitOrder.AirIntercept,
		["carrier_protection"] = EUnitOrder.CarrierProtection,
		["paradrop_mission"] = EUnitOrder.Paradrop,
		["port_strike"] = EUnitOrder.PortStrike,
		["naval_strike"] = EUnitOrder.NavalStrike,
		["rebase_to_carrier"] = EUnitOrder.RebaseCarrier,
		["nuke_mission"] = null,
		["transport_supplies_mission"] = EUnitOrder.AirSupply,
		["rebase_air"] = EUnitOrder.RebaseAir,
		["air_reserve"] = null,
		["air_superiority"] = EUnitOrder.AirSuperiority,
		["join_fleet"] = EUnitOrder.JoinFleet,
		["join_air"] = EUnitOrder.JoinAir,
	};

	/// <summary>
	/// Reads the order onto the unit. A target is a unit's number, given back to be found once every unit is read.
	/// </summary>
	/// <returns>The number of the unit the order is aimed at, or nought.</returns>
	public static int Read(CUnit unit, EUnitOrder order, ClausewitzNode block, CSaveIds ids, CSaveReport report)
	{
		unit.Order = order;
		unit.OrderProvince = 0;
		var mission = new CMission();
		unit.Mission = mission;
		int target = 0;
		bool first = true;
		foreach (ClausewitzNode entry in block.Children)
		{
			switch (entry.Key)
			{
				case null:
					break;
				case "province" when first:
					unit.OrderProvince = (int)CSaveValues.Whole(entry.Value);
					first = false;
					break;
				case "province":
					// A convoy escort's second province: where it was sent to.
					mission.Destination = (int)CSaveValues.Whole(entry.Value);
					break;
				case "target" when entry.IsBlock:
					target = ids.Of(entry) is > 0 and int unitId ? unitId : target;
					break;
				case "stance":
					mission.Stance = (int)CSaveValues.Whole(entry.Value);
					break;
				case "start_date":
					mission.StartHour = Hour(entry.Value) ?? 0;
					break;
				case "death_date":
					mission.EndHour = Hour(entry.Value) ?? CMission.NoEnd;
					break;
				case "return_to_base":
					mission.ReturnToBase = entry.Value == "yes";
					break;
				case "time":
					mission.Time = (int)CSaveValues.Whole(entry.Value);
					break;
				case "priority":
					mission.Priority = (int)CSaveValues.Whole(entry.Value);
					break;
				// A patrol's and an intercept's fleet, and the fleet to join: an id. An air order's area: a province a line.
				case "provinces" when entry.IsBlock:
				case "join_fleet" when entry.IsBlock:
					target = ids.Of(entry) is > 0 and int other ? other : target;
					break;
				case "provinces":
					mission.Provinces.Add((int)CSaveValues.Whole(entry.Value));
					break;
				case "start":
					mission.Waypoints.Add((int)CSaveValues.Whole(entry.Value));
					break;
				case "current":
					mission.Current = (int)CSaveValues.Whole(entry.Value);
					break;
				// A raider's hours to its next try at the convoys (inferred from the field it is written from).
				case "attack":
					mission.RaidCountdown = (int)CSaveValues.Whole(entry.Value);
					break;
				case "convoy":
					mission.Convoy = (int)CSaveValues.Whole(entry.Value);
					break;
				case "combat":
					mission.Joined = entry.Value == "yes";
					break;
				case "supplies":
					mission.Supplies = CSaveValues.Fixed(entry.Value);
					break;
				case "fuel":
					mission.Fuel = CSaveValues.Fixed(entry.Value);
					break;
				// The halt figure, a naval order's three counts, an air order's figure for each air type and the rest:
				// nothing here holds them.
				default:
					report.Unread("order", entry.Key);
					break;
			}
		}
		// A sortie's own province is the first of its way.
		if (order == EUnitOrder.Sortie && mission.Waypoints.Count == 0 && unit.OrderProvince > 0)
		{
			mission.Waypoints.Add(unit.OrderProvince);
		}
		return target;
	}

	/// <summary>A date with its hour; none for the null date, <c>1.1.1</c>.</summary>
	public static long? Hour(string? text)
	{
		if (!CSaveValues.TryHours(text, out long hours) || text!.Trim('"').StartsWith("1.1.1", StringComparison.Ordinal))
		{
			return null;
		}
		return hours;
	}
}
