using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// A save's units, leaders in service and lines of the queue as <c>docs/mechanics/savegame.md</c>, section 5, reads them,
/// on two provinces of land and one of sea:
///
///     1 Kiel ==port== 3 Bight      2 Berlin
/// </summary>
public class SaveUnitsTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static CGameState State(out CCountry germany)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var types = new Dictionary<string, CSubUnitType>(System.StringComparer.OrdinalIgnoreCase)
		{
			["infantry_brigade"] = new("infantry_brigade", EUnitBranch.Land),
			["hq_brigade"] = new("hq_brigade", EUnitBranch.Land),
			["carrier"] = new("carrier", EUnitBranch.Naval),
			["transport_ship"] = new("transport_ship", EUnitBranch.Naval),
			["cag"] = new("cag", EUnitBranch.Air),
		};
		var state = new CGameState(new CDate(1936, 1, 7),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(1, "Kiel", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(2, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(3, "Bight", isSea: true),
			},
			new[] { germany },
			buildings: new[] { new CBuilding(0, "industry", 10, "industry", CFixedPoint.One) })
		{
			SubUnitTypes = types,
			Traits = new[] { new CTrait("panzer_leader"), new CTrait("old_guard") },
		};
		return state;
	}

	private static ClausewitzNode Parse(string text) => ClausewitzParser.Parse(text);

	[Fact]
	public void The_history_keeps_its_numbers_and_the_players_come_after_the_counter_in_the_order_minted()
	{
		var ids = new CSaveIds();
		ids.SetHistoryNext(5608);
		ids.SetHighest(2574, CSaveIds.Played);
		Assert.Equal(87, ids.Of(87, CSaveIds.History));
		// The player's first serial is the counter itself, right after the history's last.
		Assert.Equal(5608, ids.Of(1, CSaveIds.Played));
		Assert.Equal(5608 + 2, ids.Of(3, CSaveIds.Played));
		Assert.True(ids.Of(10, CSaveIds.Played) > ids.Of(3, CSaveIds.Played));
		Assert.Equal(0, ids.Of(0, 0));
		// And each number is given back as the pair it came from.
		Assert.Equal((87, CSaveIds.History), CSaveIds.Pair(87, ids.HistoryNext));
		Assert.Equal((3, CSaveIds.Played), CSaveIds.Pair(ids.Of(3, CSaveIds.Played), ids.HistoryNext));
		Assert.Equal((2574, CSaveIds.Played), CSaveIds.Pair(ids.Reserved, ids.HistoryNext));
		Assert.Equal((0, 0), CSaveIds.Pair(0, ids.HistoryNext));
		// A third type goes after both, and the same pair is the same number.
		int other = ids.Of(1, 4714);
		Assert.True(other > ids.Reserved);
		Assert.Equal(other, ids.Of(1, 4714));
	}

	[Fact]
	public void A_tree_is_read_whole_with_a_carriers_group_and_a_passenger_put_back_under_its_superior()
	{
		CGameState state = State(out CCountry germany);
		var report = new CSaveReport();
		var reader = new CSaveUnitReader(state, new CSaveIds(), report);
		ClausewitzNode theatre = Parse("""
			id = { id = 10 type = 41 }
			name = "OKH"
			movement_progress = 0.500
			path = { 2 }
			location = 1
			fuel = 0.750
			supplies = 1.000
			can_reinforce = no
			regiment = { id = { id = 1 type = 41 } name = "OKH HQ" type = "hq_brigade" organisation = 53.594 strength = 10.000 highest = 10.000 experience = 15.000 }
			division = {
				id = { id = 11 type = 41 } name = "lost" movement_progress = 0.000
				regiment = { id = { id = 2 type = 41 } name = "lost 1" type = "infantry_brigade" organisation = 1.000 strength = 1.000 highest = 1.000 experience = 0.000 }
			}
			navy = {
				id = { id = 12 type = 41 } name = "Fleet" base = 1 location = 3 movement_progress = 0.000 at_sea = 3
				ship = {
					id = { id = 3 type = 41 } name = "Graf Zeppelin" type = "carrier" organisation = 30.000 strength = 100.000 highest = 100.000 experience = 0.000
					air = { id = { id = 13 type = 41 } name = "CAG" location = 3 movement_progress = 0.000
						wing = { id = { id = 4 type = 41 } name = "CAG 1" type = "cag" organisation = 30.000 strength = 100.000 highest = 100.000 experience = 0.000 }
						carrier_protection = { province = 3 stance = 1 start_date = "1.1.1.0" death_date = "1.1.1.0" return_to_base = no time = 2 priority = 0 stop = -1.000 }
					}
				}
				division = { id = { id = 14 type = 41 } name = "1. Division" location = 3 movement_progress = 0.000 dig_in = 2.000
					parent = { id = 10 type = 41 }
					regiment = { id = { id = 5 type = 41 } name = "1. Division" type = "infantry_brigade" organisation = 20.000 strength = 3.000 highest = 3.000 experience = 0.000 }
				}
			}
			""");
		CUnit unit = reader.Read(theatre, EUnitKind.Theatre, germany, parent: null, carriedBy: null)!;
		germany.Units.Add(unit);
		reader.Finish();

		Assert.Equal(10, unit.Id);
		Assert.Equal("OKH", unit.Name);
		Assert.Equal(new[] { 2 }, unit.Path);
		Assert.Equal(new CFixedPoint(500), unit.MovementProgress);
		Assert.Equal(new CFixedPoint(750), unit.FuelReceived);
		Assert.False(unit.MayReinforce);
		Assert.Equal(new CFixedPoint(53594), unit.SubUnits.Single().Organisation);

		// A unit with no location is thrown away; the fleet stays under the theatre.
		CUnit fleet = Assert.Single(unit.Children, child => child.Kind == EUnitKind.Navy);
		Assert.DoesNotContain(unit.Children, child => child.Id == 11);
		Assert.Equal(3, fleet.EngageWait);
		Assert.Contains(report.UnreadKeys.Keys, key => key.Key.Contains("no location"));

		// The carrier's group is aboard its ship, based where the fleet is, under carrier protection with no end.
		CUnit group = Assert.Single(fleet.SubUnits.Single().Carried);
		Assert.Equal(3, group.Base);
		Assert.Equal(EUnitOrder.CarrierProtection, group.Order);
		Assert.Equal(OpenHOI3.Sim.Missions.CMission.NoEnd, group.Mission.EndHour);
		// And hung under the fleet, as the original's CShip reading does it (transport.md, section 13.1).
		Assert.Same(fleet, group.Parent);
		Assert.Equal(new[] { group }, fleet.Children);

		// The passenger is back under the theatre, aboard its fleet, and dug in nowhere.
		CUnit passenger = Assert.Single(fleet.Passengers);
		Assert.Equal(14, passenger.Id);
		Assert.Same(unit, passenger.Parent);
		Assert.Contains(passenger, unit.Children);
		Assert.Same(fleet, passenger.Transport);
		Assert.Equal(CFixedPoint.Zero, passenger.DigIn);
		Assert.Equal(4, germany.AllUnits().Count());
	}

	[Fact]
	public void An_order_is_read_onto_the_unit_and_its_target_found_once_every_unit_is_read()
	{
		CGameState state = State(out CCountry germany);
		var reader = new CSaveUnitReader(state, new CSaveIds(), new CSaveReport());
		ClausewitzNode fleets = Parse("""
			navy = { id = { id = 20 type = 41 } name = "Raider" location = 3 movement_progress = 0.000
				patrol = { province = 3 stance = 1 start_date = "1936.1.3.14" death_date = "1936.1.10.14" return_to_base = no time = 2 priority = 1 stop = 0.300 return_to_base = no priority = 1 capital = 0 sub_unit = 0 other = 0 provinces = { id = 21 type = 41 } }
			}
			navy = { id = { id = 21 type = 41 } name = "Shadowed" location = 3 movement_progress = 0.000 }
			""");
		foreach (ClausewitzNode block in fleets.Children)
		{
			germany.Units.Add(reader.Read(block, EUnitKind.Navy, germany, parent: null, carriedBy: null)!);
		}
		reader.Finish();

		CUnit raider = germany.Units[0];
		Assert.Equal(EUnitOrder.Patrol, raider.Order);
		Assert.Equal(3, raider.OrderProvince);
		Assert.Equal(1, raider.Mission.Priority);
		Assert.True(CSaveValues.TryHours("1936.1.10.14", out long end));
		Assert.Equal(end, raider.Mission.EndHour);
		Assert.Same(germany.Units[1], raider.Mission.Target);
		Assert.True(germany.Units[1].IsShadowed);
	}

	[Fact]
	public void Leaders_in_service_take_their_state_and_keep_a_trait_once()
	{
		CGameState state = State(out CCountry germany);
		var guderian = new CLeader(324) { Name = "Guderian", Country = Ger, Skill = 4, MaxSkill = 6 };
		guderian.Traits.Add(state.Traits[0]);
		state.Leaders[324] = guderian;
		var report = new CSaveReport();
		CSaveLeaders.Read(state, germany, Parse("""
			324 = { rank = 1 current_experience = 13000.00000 gainable_trait = { duration = 0 current = -1 } trait = { panzer_leader old_guard } }
			999 = { rank = 2 current_experience = 0.00000 }
			"""), report);

		Assert.Same(guderian, Assert.Single(germany.ActiveLeaders));
		Assert.Equal(1, guderian.Rank);
		Assert.Equal(CFixed15.FromInt(13000), guderian.Experience);
		Assert.Equal(new[] { "panzer_leader", "old_guard" }, guderian.Traits.Select(trait => trait.Key));
		// Raised to the skill his experience is past the floor of, as after any reading.
		Assert.Equal(5, guderian.Skill);
		Assert.Contains(report.UnreadKeys.Keys, key => key.Key.Contains("no file's"));
	}

	[Fact]
	public void Lines_keep_their_figures_and_a_convoy_line_builds_escorts_by_its_second_status()
	{
		CGameState state = State(out CCountry germany);
		var ids = new CSaveIds();
		var report = new CSaveReport();
		CSaveLines.ReadMilitary(state, germany, Parse("""
			id = { id = 238 type = 41 } size = 1 cost = 7.020 duration = 383.000 progress = 315.000 status = 1.000 country = "GER"
			unit = { id = 239 type = 41 } manpower = 0.000
			carrier = { id = { id = 242 type = 41 } name = "Graf Spee" model = { 0 3 1 } }
			accumulated_experiance = 0.390 accumulated_progress = 0.018
			"""), ids, report);
		CSaveLines.ReadConvoy(state, germany, Parse("""
			id = { id = 1031 type = 4713 } size = 1 cost = 2.000 duration = 100.000 progress = 8.097 status = 0.500 country = "GER" status = yes
			"""), ids, report);
		CSaveLines.ReadBuilding(state, germany, Parse("""
			id = { id = 1032 type = 4713 } size = 1 cost = 1.000 duration = 50.000 progress = 1.000 status = 1.000 country = "GER" location = 2 building = 1
			"""), ids, report);

		Assert.Equal(3, germany.Constructions.Count);
		CConstruction ship = germany.Constructions[0];
		Assert.Equal((238, 239), (ship.Id, ship.UnitId));
		Assert.Equal(new CFixedPoint(315000), ship.Progress);
		Assert.Equal(new CFixedPoint(390), ship.AccumulatedExperience);
		CBrigadeOrder order = Assert.Single(ship.Brigades);
		Assert.Equal((242, "Graf Spee"), (order.Id, order.Name));
		Assert.Equal(new[] { 0, 3, 1 }, order.Model);

		CConstruction convoy = germany.Constructions[1];
		Assert.True(convoy.IsConvoy && convoy.IsEscorts);
		Assert.Equal(new CFixedPoint(500), convoy.Status);

		CConstruction building = germany.Constructions[2];
		Assert.Same(state.Buildings[0], building.Building);
		Assert.Equal(2, building.Location);
		Assert.Empty(report.UnreadKeys);
	}
}
