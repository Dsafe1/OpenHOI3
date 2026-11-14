using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
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
		// Its hours to the next try are not saved: the original's air order writes none, and a load starts them at nought.
		["air_convoy_raid"] = EUnitOrder.AirConvoyRaid,
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
		["nuke_mission"] = EUnitOrder.Nuke,
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
	/// <returns>
	/// The number of the unit the order is after - a patrol's or an intercept's fleet, the unit to join - and of the
	/// one an intercept's order names, apart from the fleet its search found; nought for none.
	/// </returns>
	public static (int Target, int OrderTarget) Read(CGameState state, CUnit unit, EUnitOrder order, ClausewitzNode block,
		CSaveIds ids, CSaveReport report)
	{
		unit.Order = order;
		unit.OrderProvince = 0;
		var mission = new CMission();
		unit.Mission = mission;
		int target = 0;
		int orderTarget = 0;
		bool navalOwnReturn = false;
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
				// An intercept's own target is the order's; the fleet it is after is its `provinces`.
				case "target" when entry.IsBlock && order == EUnitOrder.Intercept:
					orderTarget = ids.Of(entry);
					break;
				case "target" when entry.IsBlock:
					target = ids.Of(entry) is > 0 and int unitId ? unitId : target;
					break;
				case "stop":
					mission.Stop = CSaveValues.Fixed(entry.Value);
					break;
				// A naval order's three counts are nought for good in the original (savegame.md, section 9).
				case "capital":
				case "sub_unit":
				case "other":
					if (CSaveValues.Whole(entry.Value) != 0)
					{
						report.Unread("order", $"{entry.Key} not nought");
					}
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
				// A naval order's two both go to its own, the last one read staying, and its being told to return is
				// lost - as the original loads them (reversing/findings/FINDINGS-savegame.md, section C.5).
				case "return_to_base" when IsNaval(order):
					navalOwnReturn = entry.Value == "yes";
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
				// An invasion's sea, the one the landing is made from (reversing/findings/FINDINGS-transport.md, B.1).
				case "transport":
					mission.LandingSea = (int)CSaveValues.Whole(entry.Value);
					break;
				case "combat":
					mission.Joined = entry.Value == "yes";
					break;
				case "supplies":
					mission.Supplies = CSaveValues.Fixed(entry.Value);
					break;
				case "nukes":
					mission.Nukes = (int)CSaveValues.Whole(entry.Value);
					mission.BombsFrom = state.Country(unit.Owner);
					break;
				case "fuel":
					mission.Fuel = CSaveValues.Fixed(entry.Value);
					break;
				// An air order's figure for an air type, nought in the original but where a save gives one; the rest is
				// nothing held here.
				default:
					if (state.SubUnitTypes.TryGetValue(entry.Key, out CSubUnitType? type) && type.Branch == EUnitBranch.Air)
					{
						if (CSaveValues.Fixed(entry.Value).Raw != 0)
						{
							report.Unread("order", "an air type's figure not nought");
						}
						break;
					}
					report.Unread("order", entry.Key);
					break;
			}
		}
		// The naval order's own, which nothing in play sets and nothing here holds.
		if (navalOwnReturn)
		{
			report.Unread("order", "a naval order's own return to base");
		}
		// A sortie's own province is the first of its way.
		if (order == EUnitOrder.Sortie && mission.Waypoints.Count == 0 && unit.OrderProvince > 0)
		{
			mission.Waypoints.Add(unit.OrderProvince);
		}
		return (target, orderTarget);
	}

	/// <summary>The orders of the original's naval family, which keep a return to base and a priority of their own.</summary>
	private static bool IsNaval(EUnitOrder order)
	{
		return order is EUnitOrder.Rebase or EUnitOrder.Reserve or EUnitOrder.Patrol or EUnitOrder.Intercept
			or EUnitOrder.Sortie or EUnitOrder.ConvoyEscort or EUnitOrder.ConvoyRaid or EUnitOrder.Transport
			or EUnitOrder.Invasion or EUnitOrder.JoinFleet;
	}

	/// <summary>The date an order writes where it has none.</summary>
	private const string NullDate = "1.1.1.0";

	/// <summary>
	/// A unit's order under its own key, as the original's <c>COrder</c> family writes it: the order's own keys, a naval
	/// or an air order's second <c>return_to_base</c> and <c>priority</c> - a naval order's three counts, nought for good
	/// in the original - an air order's area and a figure for each air type, nought but where a save gives one, then each
	/// kind's tail (<c>savegame.md</c>, section 9).
	/// </summary>
	public static void Write(CSaveTextWriter writer, CGameState state, CUnit unit)
	{
		EUnitOrder order = unit.Order;
		CMission mission = unit.Mission;
		string key = Kinds.First(pair => pair.Value == order).Key;
		bool air = CAirMissions.IsAirOrder(order) || order == EUnitOrder.JoinAir;
		bool naval = IsNaval(order);
		// The unit the order names: an intercept's own, apart from the fleet it is after, and a join's.
		CUnit? aimed = order == EUnitOrder.Intercept ? mission.OrderTarget
			: order is EUnitOrder.JoinFleet or EUnitOrder.JoinAir ? mission.Target : null;

		writer.Begin(key);
		if (unit.OrderProvince > 0)
		{
			writer.Int("province", unit.OrderProvince);
		}
		if (aimed is not null)
		{
			writer.IdPair("target", CSaveUnitWriter.Pair(state, aimed.Id));
		}
		writer.Int("stance", mission.Stance);
		writer.Quoted("start_date", mission.StartHour > 0 ? CSaveValues.HoursText(mission.StartHour) : NullDate);
		writer.Quoted("death_date", mission.EndHour != CMission.NoEnd ? CSaveValues.HoursText(mission.EndHour) : NullDate);
		writer.Bool("return_to_base", mission.ReturnToBase);
		writer.Int("time", mission.Time);
		writer.Int("priority", mission.Priority);
		writer.Fixed("stop", mission.Stop);
		// An air order writes its two again; a naval order its own, of which the return to base is nothing in play sets.
		if (air || naval)
		{
			writer.Bool("return_to_base", air && mission.ReturnToBase);
			writer.Int("priority", mission.Priority);
		}
		if (naval)
		{
			writer.Int("capital", 0);
			writer.Int("sub_unit", 0);
			writer.Int("other", 0);
		}
		if (air)
		{
			foreach (int province in mission.Provinces)
			{
				writer.Int("provinces", province);
			}
			foreach (CSubUnitType type in state.SubUnitTypes.Values.Where(type => type.Branch == EUnitBranch.Air)
				.OrderBy(type => type.DatabaseIndex))
			{
				writer.Fixed(type.Key, CFixedPoint.Zero);
			}
		}
		switch (order)
		{
			case EUnitOrder.SupportAttack when mission.Joined:
				writer.Bool("combat", true);
				break;
			// The fleet a patrol shadows, the null pair for none, and an empty line after it - as after an intercept's
			// fleet and a join's second target.
			case EUnitOrder.Patrol:
				writer.IdPair("provinces", CSaveUnitWriter.Pair(state, mission.Target?.Id ?? 0));
				writer.BlankLine();
				break;
			// An intercept's fleet only once its search has found one, and one still in the game.
			case EUnitOrder.Intercept when mission.LiveTarget is { } chased:
				writer.IdPair("provinces", CSaveUnitWriter.Pair(state, chased.Id));
				writer.BlankLine();
				break;
			case EUnitOrder.JoinFleet:
				writer.IdPair("join_fleet", CSaveUnitWriter.Pair(state, aimed?.Id ?? 0));
				break;
			case EUnitOrder.JoinAir:
				writer.IdPair("target", CSaveUnitWriter.Pair(state, aimed?.Id ?? 0));
				writer.BlankLine();
				break;
			case EUnitOrder.ConvoyRaid:
				writer.Int("attack", mission.RaidCountdown);
				break;
			case EUnitOrder.Nuke:
				writer.Int("nukes", mission.Nukes);
				break;
			case EUnitOrder.Sortie:
				foreach (int waypoint in mission.Waypoints)
				{
					writer.Int("start", waypoint);
				}
				foreach (int province in mission.Provinces)
				{
					writer.Int("provinces", province);
				}
				writer.Int("current", mission.Current);
				break;
			case EUnitOrder.ConvoyEscort:
				writer.Int("province", mission.Destination);
				writer.Int("convoy", mission.Convoy);
				break;
			case EUnitOrder.Invasion when mission.LandingSea > 0:
				writer.Int("transport", mission.LandingSea);
				break;
			case EUnitOrder.AirSupply:
				writer.Fixed("supplies", mission.Supplies);
				writer.Fixed("fuel", mission.Fuel);
				break;
		}
		writer.End();
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
