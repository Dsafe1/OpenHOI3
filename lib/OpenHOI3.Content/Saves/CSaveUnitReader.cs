using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A save's unit tree, as the original's <c>CUnit::LoadKey</c> reads it: every unit block with its brigades, the units
/// under it, its order and its plan; an air group aboard a carrier inside its ship; a land unit aboard a fleet or an
/// aeroplane inside its carrier, put back under its land superior once every unit is read. A unit with no location is
/// thrown away, as the original deletes one. See <c>docs/mechanics/savegame.md</c>, section 5.
/// </summary>
public sealed class CSaveUnitReader
{
	/// <summary>The keys that declare a unit, and what each makes.</summary>
	public static readonly IReadOnlyDictionary<string, EUnitKind> UnitKinds = new Dictionary<string, EUnitKind>(StringComparer.Ordinal)
	{
		["theatre"] = EUnitKind.Theatre,
		["armygroup"] = EUnitKind.ArmyGroup,
		["army"] = EUnitKind.Army,
		["corps"] = EUnitKind.Corps,
		["division"] = EUnitKind.Division,
		["navy"] = EUnitKind.Navy,
		["air"] = EUnitKind.Air,
	};

	private readonly CGameState _state;
	private readonly CSaveIds _ids;
	private readonly CSaveReport _report;
	private readonly List<(CUnit Unit, int Target, int OrderTarget)> _targets = new();
	private readonly List<(CUnit Passenger, CUnit Carrier, int Parent, CCountry Owner)> _passengers = new();

	public CSaveUnitReader(CGameState state, CSaveIds ids, CSaveReport report)
	{
		_state = state;
		_ids = ids;
		_report = report;
	}

	/// <summary>Every unit read, by its number.</summary>
	public Dictionary<int, CUnit> ById { get; } = new();

	/// <summary>
	/// A unit block and all under it. Null where it has no location and was thrown away - a unit waiting to be placed
	/// is read with <paramref name="placed"/> false, and kept.
	/// </summary>
	public CUnit? Read(ClausewitzNode block, EUnitKind kind, CCountry owner, CUnit? parent, CSubUnit? carriedBy,
		bool placed = true)
	{
		var unit = new CUnit(kind, owner.Tag) { Parent = parent, CarriedBy = carriedBy };
		bool located = false;
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is not { } key)
			{
				continue;
			}
			if (entry.IsBlock && UnitKinds.TryGetValue(key, out EUnitKind childKind))
			{
				// Land under a fleet or an aeroplane is what it carries; under a land unit, its own.
				bool carried = kind is EUnitKind.Navy or EUnitKind.Air && childKind is not (EUnitKind.Navy or EUnitKind.Air);
				CUnit? child = Read(entry, childKind, owner, carried ? null : unit, carriedBy: null);
				if (child is null)
				{
					continue;
				}
				if (carried)
				{
					_passengers.Add((child, unit, _ids.Of(entry.Get("parent")), owner));
				}
				else
				{
					unit.Children.Add(child);
				}
				continue;
			}
			if (entry.IsBlock && key is "regiment" or "ship" or "wing")
			{
				ReadSubUnit(entry, key, unit, owner);
				continue;
			}
			if (entry.IsBlock && CSaveOrders.Kinds.TryGetValue(key, out Sim.Units.EUnitOrder? order))
			{
				if (order is { } known)
				{
					(int target, int orderTarget) = CSaveOrders.Read(_state, unit, known, entry, _ids, _report);
					if (target > 0 || orderTarget > 0)
					{
						_targets.Add((unit, target, orderTarget));
					}
				}
				else
				{
					_report.Unread("unit", $"order {key}");
				}
				continue;
			}
			switch (key)
			{
				case "id":
					unit.Id = _ids.Of(entry);
					break;
				case "name":
					unit.Name = entry.Value?.Trim('"') ?? "";
					break;
				case "base":
					unit.Base = (int)CSaveValues.Whole(entry.Value);
					break;
				// A leader's number is his own, of type 37 and not of the count: the serial is his id.
				case "leader":
					unit.LeaderId = entry.GetInt("id");
					break;
				case "previous":
					unit.Previous = (int)CSaveValues.Whole(entry.Value);
					break;
				case "movement_progress":
					unit.MovementProgress = CSaveValues.Fixed(entry.Value);
					break;
				case "path":
					foreach (ClausewitzNode step in entry.Children)
					{
						unit.Path.Add((int)CSaveValues.Whole(step.Value));
					}
					break;
				case "location":
					unit.Location = (int)CSaveValues.Whole(entry.Value);
					located = unit.Location > 0 && unit.Location < _state.Provinces.Count;
					break;
				case "retreat":
					unit.IsRetreating = entry.Value == "yes";
					break;
				case "dig_in":
					unit.DigIn = CSaveValues.Fixed(entry.Value);
					break;
				case "attack_delay":
					unit.AttackDelay = CSaveValues.Fixed(entry.Value);
					break;
				case "combat_hours":
					unit.HoursAttacking = (int)CSaveValues.Whole(entry.Value);
					break;
				case "is_prioritized":
					unit.IsPrioritised = entry.Value == "yes";
					break;
				case "can_reinforce":
					unit.MayReinforce = entry.Value == "yes";
					break;
				case "can_upgrade":
					unit.MayUpgrade = entry.Value == "yes";
					break;
				case "fuel":
					unit.FuelReceived = CSaveValues.Fixed(entry.Value);
					break;
				case "supplies":
					unit.SuppliesReceived = CSaveValues.Fixed(entry.Value);
					break;
				// The order's two hours, the unit's own (landorders.md, section 5).
				case "start_date":
					unit.NotBeforeHour = CSaveOrders.Hour(entry.Value);
					break;
				case "end_date":
					unit.ArriveNotBeforeHour = CSaveOrders.Hour(entry.Value);
					break;
				case "disengage":
					unit.Disengage = (int)CSaveValues.Whole(entry.Value);
					break;
				case "possible_retreat":
					unit.DisengageTo = (int)CSaveValues.Whole(entry.Value);
					break;
				case "track":
					unit.IsShadowed = entry.Value == "yes";
					break;
				case "was_paradropped":
					unit.WasParadropped = entry.Value == "yes";
					break;
				case "aggression_enabled":
					unit.HasOwnAggression = entry.Value == "yes";
					break;
				case "aggression_setting":
					unit.Aggression = CSaveValues.Fixed(entry.Value);
					break;
				case "plan" when entry.IsBlock:
					ReadPlan(unit.Plan, entry);
					break;
				case "is_reserve":
					unit.IsReserve = entry.Value == "yes";
					break;
				// The AI's sealift target and an airborne army's staging province.
				case "target":
					unit.SealiftTarget = (int)CSaveValues.Whole(entry.Value);
					break;
				case "staging_province":
					unit.Staging = (int)CSaveValues.Whole(entry.Value);
					break;
				// The hours after a battle at sea before a fleet starts another.
				case "at_sea":
					unit.EngageWait = (int)CSaveValues.Whole(entry.Value);
					break;
				case "parent":
					break;
				default:
					_report.Unread("unit", key);
					break;
			}
		}
		// An air group aboard a carrier is based where the carrier's fleet is; a save writes it no base.
		foreach (CSubUnit ship in unit.SubUnits)
		{
			foreach (CUnit aboard in ship.Carried)
			{
				aboard.Base = unit.Location;
			}
		}
		if (placed && !located)
		{
			_report.Unread("unit", "a unit with no location, thrown away");
			return null;
		}
		if (unit.Id == 0)
		{
			unit.Id = ++_state.LastUnitId;
		}
		ById[unit.Id] = unit;
		return unit;
	}

	private void ReadSubUnit(ClausewitzNode block, string keyword, CUnit unit, CCountry owner)
	{
		bool regiment = keyword == "regiment";
		if (Build(block, keyword, unit, owner, reserve: regiment && unit.IsReserve) is not { } subUnit)
		{
			return;
		}
		unit.SubUnits.Add(subUnit);
		if (subUnit.Name.Length == 0 && regiment)
		{
			subUnit.Name = $"{unit.SubUnits.Count.ToString(CultureInfo.InvariantCulture)}. {unit.Name}";
		}
	}

	/// <summary>
	/// A ship of the save's <c>sunk_ships</c>: written as a ship in a fleet is, and belonging to no unit. Its owner is
	/// the tag it is filed under, which the caller holds. See <c>docs/mechanics/savegame.md</c>, section 7.
	/// </summary>
	public CSubUnit? ReadSunkShip(ClausewitzNode block, CCountry? owner)
	{
		return Build(block, "sunk_ships", unit: null, owner, reserve: false);
	}

	/// <summary>A brigade, ship or wing read whole, its figures made again from its type and levels, and not put anywhere.</summary>
	private CSubUnit? Build(ClausewitzNode block, string keyword, CUnit? unit, CCountry? owner, bool reserve)
	{
		string typeKey = block.Get("type")?.Value?.Trim('"') ?? "";
		if (!_state.SubUnitTypes.TryGetValue(typeKey, out CSubUnitType? type))
		{
			_report.Unread(keyword, $"a brigade of no type known, {typeKey}, left out");
			return null;
		}
		var subUnit = new CSubUnit(type) { Unit = unit, IsReserve = reserve };
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is not { } key)
			{
				continue;
			}
			switch (key)
			{
				case "type":
					break;
				case "id":
					subUnit.Id = _ids.Of(entry);
					break;
				case "name":
					subUnit.Name = entry.Value?.Trim('"') ?? "";
					break;
				case "organisation":
					subUnit.Organisation = CSaveValues.Fixed(entry.Value);
					break;
				case "strength":
					subUnit.Strength = CSaveValues.Fixed(entry.Value);
					break;
				case "highest":
					subUnit.Highest = CSaveValues.Fixed(entry.Value);
					break;
				case "experience":
					subUnit.Experience = CSaveValues.Fixed(entry.Value);
					break;
				case "is_reserve":
					subUnit.IsReserve = entry.Value == "yes";
					break;
				case "pride":
					subUnit.IsPride = entry.Value == "yes";
					break;
				case "sunk_by":
					// A sunk ship's sinker, a ship's or a wing's own id.
					subUnit.SunkById = _ids.Of(entry);
					break;
				case "current_distance":
					subUnit.Distance = CSaveValues.Fixed(entry.Value);
					break;
				case "air" when entry.IsBlock && owner is null:
					_report.Unread(keyword, "an air group of a ship whose country does not exist");
					break;
				case "air" when entry.IsBlock:
					// A carrier's air group: written here and nowhere else.
					if (Read(entry, EUnitKind.Air, owner!, parent: null, carriedBy: subUnit) is { } group)
					{
						subUnit.Carried.Add(group);
					}
					break;
				default:
					if (entry.IsBlock && subUnit.TechnologyLevels.ContainsKey(key) && entry.Children.Count >= 2)
					{
						// A level and how far the next has got.
						subUnit.TechnologyLevels[key] = (int)CSaveValues.Whole(entry.Children[0].Value);
						subUnit.TechnologyProgress[key] = CSaveValues.Fixed(entry.Children[1].Value);
					}
					else
					{
						_report.Unread(keyword, entry.IsBlock && _state.Technologies.ContainsKey(key) ? "a technology not its type's" : key);
					}
					break;
			}
		}
		// A save never writes a historical model, so none is applied again: the figures are its type's and its levels'.
		CUnits.RebuildStats(subUnit);
		if (subUnit.Id == 0)
		{
			subUnit.Id = ++_state.LastUnitId;
		}
		return subUnit;
	}

	/// <summary>A theatre's plan, as <c>CUnitPlan</c> writes it.</summary>
	private void ReadPlan(CUnitPlan plan, ClausewitzNode block)
	{
		plan.Area.Clear();
		plan.Min.Clear();
		plan.Objectives.Clear();
		plan.Path.Clear();
		plan.Path2.Clear();
		foreach (ClausewitzNode entry in block.Children)
		{
			switch (entry.Key)
			{
				case null:
					break;
				case "active":
					plan.Active = entry.Value == "yes";
					break;
				case "allow_hq_reorg":
					plan.AllowHqReorg = entry.Value == "yes";
					break;
				case "stance":
					plan.Stance = (int)CSaveValues.Whole(entry.Value);
					break;
				case "air_stance":
					plan.AirStance = (int)CSaveValues.Whole(entry.Value);
					break;
				case "naval_stance":
					plan.NavalStance = (int)CSaveValues.Whole(entry.Value);
					break;
				case "our_power":
					plan.OurPower = CSaveValues.Fixed(entry.Value).Raw;
					break;
				case "their_power":
					plan.TheirPower = CSaveValues.Fixed(entry.Value).Raw;
					break;
				case "embark":
					plan.Embark = (int)CSaveValues.Whole(entry.Value);
					break;
				case "debark":
					plan.Debark = (int)CSaveValues.Whole(entry.Value);
					break;
				case "path":
					Numbers(plan.Path, entry);
					break;
				case "path2":
					Numbers(plan.Path2, entry);
					break;
				case "ops_area":
					Numbers(plan.Area, entry);
					break;
				case "min":
					Numbers(plan.Min, entry);
					break;
				case "objective" when entry.IsBlock:
					plan.Objectives.Add(new CObjective((int)CSaveValues.Whole(entry, "province"),
						(int)CSaveValues.Whole(entry, "priority")));
					break;
				default:
					_report.Unread("plan", entry.Key);
					break;
			}
		}
	}

	private static void Numbers(List<int> into, ClausewitzNode block)
	{
		foreach (ClausewitzNode value in block.Children)
		{
			into.Add((int)CSaveValues.Whole(value.Value));
		}
	}

	/// <summary>
	/// Once every country's units are read: each order's target found, and each land unit aboard put back under its land
	/// superior - or at the top of its country's units where it has none - and aboard its carrier.
	/// </summary>
	public void Finish()
	{
		foreach ((CUnit unit, int target, int orderTarget) in _targets)
		{
			if (ById.TryGetValue(target, out CUnit? aimed))
			{
				unit.Mission.Target = aimed;
				if (unit.Order == EUnitOrder.Patrol)
				{
					aimed.IsShadowed = true;
				}
			}
			if (ById.TryGetValue(orderTarget, out CUnit? named))
			{
				unit.Mission.OrderTarget = named;
			}
		}
		foreach ((CUnit passenger, CUnit carrier, int parentId, CCountry owner) in _passengers)
		{
			if (ById.TryGetValue(parentId, out CUnit? parent) && parent.Kind is not (EUnitKind.Navy or EUnitKind.Air))
			{
				passenger.Parent = parent;
				parent.Children.Add(passenger);
			}
			else
			{
				owner.Units.Add(passenger);
			}
			if (carrier.Kind == EUnitKind.Navy)
			{
				// Aboard a fleet it is dug in nowhere and stages nowhere, as the original's fleet zeroes both.
				CTransport.Board(carrier, passenger);
				passenger.Staging = 0;
			}
			else
			{
				CAirTransport.Board(carrier, passenger);
			}
		}
	}
}
