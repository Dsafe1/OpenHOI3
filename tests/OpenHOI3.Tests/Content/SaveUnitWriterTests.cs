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
/// A country's military part written as the original's <c>.hoi3</c>, as <c>docs/mechanics/savegame.md</c>, section 9,
/// writes it, on two provinces of land and one of sea:
///
///     1 Kiel ==port== 3 Bight      2 Berlin
/// </summary>
public class SaveUnitWriterTests
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
			["cag"] = new("cag", EUnitBranch.Air),
		};
		return new CGameState(new CDate(1936, 1, 7),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(1, "Kiel", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(2, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(3, "Bight", isSea: true),
			},
			new[] { germany })
		{
			SubUnitTypes = types,
			Traits = new[] { new CTrait("logistics_wizard"), new CTrait("old_guard") },
			HistoryUnitNext = 100,
		};
	}

	/// <summary>A tree in the original's layout: a theatre with its brigade, a division of the player's under it with
	/// its order and a plan; a fleet with a carrier's group, a patrol and a division aboard naming its superior.</summary>
	private const string Tree =
		"theatre=\r\n{\r\n" +
		"\tid=\r\n\t{\r\n\t\tid=10\r\n\t\ttype=41\r\n\t}\r\n\tname=\"OKH\"\r\n\tmovement_progress=0.500\r\n" +
		"\tpath=\r\n\t{\r\n\t\t2 \t}\r\n\tlocation=1\r\n\tis_prioritized=no\r\n\tcan_reinforce=yes\r\n\tcan_upgrade=yes\r\n" +
		"\tfuel=0.750\r\n\tsupplies=1.000\r\n" +
		"\tregiment=\r\n\t{\r\n\t\tid=\r\n\t\t{\r\n\t\t\tid=11\r\n\t\t\ttype=41\r\n\t\t}\r\n\t\tname=\"OKH HQ\"\r\n" +
		"\t\ttype=\"hq_brigade\"\r\n\t\torganisation=50.000\r\n\t\tstrength=10.000\r\n\t\thighest=10.000\r\n\t\texperience=15.000\r\n\t}\r\n" +
		"\tdivision=\r\n\t{\r\n\t\tid=\r\n\t\t{\r\n\t\t\tid=3\r\n\t\t\ttype=4713\r\n\t\t}\r\n\t\tname=\"2. Division\"\r\n" +
		"\t\tprevious=1\r\n\t\tmovement_progress=1.250\r\n\t\tpath=\r\n\t\t{\r\n\t\t\t2 \t\t}\r\n\t\tlocation=1\r\n\t\tdig_in=2.000\r\n" +
		"\t\tis_prioritized=yes\r\n\t\tcan_reinforce=no\r\n\t\tcan_upgrade=yes\r\n\t\tfuel=1.000\r\n\t\tsupplies=1.000\r\n" +
		"\t\tregiment=\r\n\t\t{\r\n\t\t\tid=\r\n\t\t\t{\r\n\t\t\t\tid=4\r\n\t\t\t\ttype=4713\r\n\t\t\t}\r\n" +
		"\t\t\tname=\"Infanterie-Regiment 1\"\r\n\t\t\ttype=\"infantry_brigade\"\r\n\t\t\torganisation=30.500\r\n" +
		"\t\t\tstrength=3.000\r\n\t\t\thighest=3.000\r\n\t\t\texperience=0.000\r\n\t\t\tis_reserve=yes\r\n\t\t\tbuilder=\"ITA\"\r\n\t\t}\r\n" +
		"\t\tstrategic_redeployment=\r\n\t\t{\r\n\t\t\tprovince=2\r\n\t\t\tstance=1\r\n\t\t\tstart_date=\"1936.1.7.0\"\r\n" +
		"\t\t\tdeath_date=\"1936.1.9.0\"\r\n\t\t\treturn_to_base=no\r\n\t\t\ttime=2\r\n\t\t\tpriority=0\r\n\t\t\tstop=0.300\r\n\t\t}\r\n" +
		"\t\tstart_date=\"1936.1.7.0\"\r\n\t\tdisengage=4\r\n\t\tpossible_retreat=2\r\n\t\tend_date=\"1936.1.8.12\"\r\n\t}\r\n" +
		"\tplan=\r\n\t{\r\n\t\tactive=yes\r\n\t\tstance=2\r\n\t\tair_stance=1\r\n\t\tnaval_stance=1\r\n\t\tour_power=1.500\r\n" +
		"\t\ttheir_power=0.250\r\n\t\tembark=0\r\n\t\tdebark=0\r\n\t\tops_area=\r\n\t\t{\r\n1 2 \t\t}\r\n" +
		"\t\tobjective=\r\n\t\t{\r\n\t\t\tprovince=2\r\n\t\t\tpriority=3\r\n\t\t}\r\n\t}\r\n" +
		"}\r\n" +
		"navy=\r\n{\r\n" +
		"\tid=\r\n\t{\r\n\t\tid=20\r\n\t\ttype=41\r\n\t}\r\n\tname=\"Flotte\"\r\n\tbase=1\r\n\tmovement_progress=0.000\r\n" +
		"\tlocation=3\r\n\tis_prioritized=no\r\n\tcan_reinforce=yes\r\n\tcan_upgrade=yes\r\n\tfuel=1.000\r\n\tsupplies=1.000\r\n" +
		"\tship=\r\n\t{\r\n\t\tid=\r\n\t\t{\r\n\t\t\tid=21\r\n\t\t\ttype=41\r\n\t\t}\r\n\t\tname=\"Graf Zeppelin\"\r\n" +
		"\t\ttype=\"carrier\"\r\n\t\torganisation=40.000\r\n\t\tstrength=100.000\r\n\t\thighest=100.000\r\n\t\texperience=10.000\r\n" +
		"\t\tair=\r\n\t\t{\r\n\t\t\tid=\r\n\t\t\t{\r\n\t\t\t\tid=22\r\n\t\t\t\ttype=41\r\n\t\t\t}\r\n\t\t\tname=\"CAG\"\r\n" +
		"\t\t\tmovement_progress=0.000\r\n\t\t\tlocation=3\r\n\t\t\tis_prioritized=no\r\n\t\t\tcan_reinforce=yes\r\n" +
		"\t\t\tcan_upgrade=yes\r\n\t\t\tfuel=1.000\r\n\t\t\tsupplies=1.000\r\n" +
		"\t\t\twing=\r\n\t\t\t{\r\n\t\t\t\tid=\r\n\t\t\t\t{\r\n\t\t\t\t\tid=23\r\n\t\t\t\t\ttype=41\r\n\t\t\t\t}\r\n" +
		"\t\t\t\tname=\"1st CAG\"\r\n\t\t\t\ttype=\"cag\"\r\n\t\t\t\torganisation=60.000\r\n\t\t\t\tstrength=100.000\r\n" +
		"\t\t\t\thighest=100.000\r\n\t\t\t\texperience=15.000\r\n\t\t\t}\r\n" +
		"\t\t\tcarrier_protection=\r\n\t\t\t{\r\n\t\t\t\tprovince=3\r\n\t\t\t\tstance=1\r\n\t\t\t\tstart_date=\"1.1.1.0\"\r\n" +
		"\t\t\t\tdeath_date=\"1.1.1.0\"\r\n\t\t\t\treturn_to_base=no\r\n\t\t\t\ttime=2\r\n\t\t\t\tpriority=0\r\n\t\t\t\tstop=-1.000\r\n" +
		"\t\t\t\treturn_to_base=no\r\n\t\t\t\tpriority=0\r\n\t\t\t\tcag=0.000\r\n\t\t\t}\r\n\t\t}\r\n\t}\r\n" +
		"\tpatrol=\r\n\t{\r\n\t\tprovince=3\r\n\t\tstance=1\r\n\t\tstart_date=\"1936.1.3.14\"\r\n\t\tdeath_date=\"1.1.1.0\"\r\n" +
		"\t\treturn_to_base=no\r\n\t\ttime=2\r\n\t\tpriority=0\r\n\t\tstop=0.300\r\n\t\treturn_to_base=no\r\n\t\tpriority=0\r\n" +
		"\t\tcapital=0\r\n\t\tsub_unit=0\r\n\t\tother=0\r\n" +
		"\t\tprovinces=\r\n\t\t{\r\n\t\t\tid=0\r\n\t\t\ttype=0\r\n\t\t}\r\n\r\n\t}\r\n" +
		"\tdivision=\r\n\t{\r\n\t\tid=\r\n\t\t{\r\n\t\t\tid=30\r\n\t\t\ttype=41\r\n\t\t}\r\n\t\tname=\"1. Division\"\r\n" +
		"\t\tmovement_progress=0.000\r\n\t\tlocation=3\r\n\t\tis_prioritized=no\r\n\t\tcan_reinforce=yes\r\n\t\tcan_upgrade=yes\r\n" +
		"\t\tfuel=1.000\r\n\t\tsupplies=1.000\r\n\t\tparent=\r\n\t\t{\r\n\t\t\tid=10\r\n\t\t\ttype=41\r\n\t\t}\r\n\t}\r\n" +
		"\tat_sea=0\r\n" +
		"}\r\n";

	[Fact]
	public void A_tree_read_and_written_again_is_the_originals_to_the_byte()
	{
		CGameState state = State(out CCountry germany);
		var ids = new CSaveIds();
		ids.SetHistoryNext(100);
		var reader = new CSaveUnitReader(state, ids, new CSaveReport());
		foreach (ClausewitzNode block in ClausewitzParser.Parse(Tree).Children)
		{
			if (reader.Read(block, CSaveUnitReader.UnitKinds[block.Key!], germany, parent: null, carriedBy: null) is { } unit)
			{
				germany.Units.Add(unit);
			}
		}
		reader.Finish();
		CUnit theatre = germany.Units[0];
		Assert.Equal(102, theatre.Children[0].Id);
		Assert.Contains(theatre.Children, child => child.Transport is not null);
		// Whose models a brigade is named by comes back (units.md, section 5).
		Assert.Equal(new CCountryTag("ITA"), theatre.Children[0].SubUnits[0].Builder);

		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		foreach (CUnit unit in germany.Units)
		{
			if (CSaveUnitWriter.IsWrittenInPlace(unit))
			{
				CSaveUnitWriter.Write(writer, state, unit);
			}
		}

		// The player's numbers go back to their own serials, the passenger is written aboard and not under its
		// superior, the carrier's group in its ship with no base, and a patrol's fleet is followed by an empty line.
		Assert.Equal(Tree, writer.Text);
	}

	/// <summary>A division others have been given to know of: one grant of a country known and one of a number of none.</summary>
	private const string Known =
		"division=\r\n{\r\n\tid=\r\n\t{\r\n\t\tid=40\r\n\t\ttype=41\r\n\t}\r\n\tname=\"3. Division\"\r\n" +
		"\tmovement_progress=0.000\r\n\tlocation=2\r\n\tis_prioritized=no\r\n\tcan_reinforce=yes\r\n\tcan_upgrade=yes\r\n" +
		"\tfuel=1.000\r\n\tsupplies=1.000\r\n\tcountry_intel=\r\n\t{\r\n1 31 8 9 20 4 \t}\r\n}\r\n";

	[Fact]
	public void What_others_were_given_to_know_of_a_unit_is_read_by_the_countrys_number_plus_one_and_written_back()
	{
		CGameState state = State(out CCountry germany);
		var ids = new CSaveIds();
		ids.SetHistoryNext(100);
		var report = new CSaveReport();
		var reader = new CSaveUnitReader(state, ids, report);
		ClausewitzNode block = ClausewitzParser.Parse(Known).Children[0];
		CUnit unit = reader.Read(block, CSaveUnitReader.UnitKinds[block.Key!], germany, parent: null, carriedBy: null)!;
		reader.Finish();

		// Number 1 is the first country; number 9 is none of this game's one country, reported and passed over.
		CIntelGrant grant = Assert.Single(unit.IntelGrants);
		Assert.Equal(Ger, grant.Country);
		Assert.Equal(31, grant.Days);
		Assert.Equal(8, grant.Level);
		Assert.Contains(report.Lines(), line => line.Contains("country_intel", System.StringComparison.Ordinal));

		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		CSaveUnitWriter.Write(writer, state, unit);
		Assert.Equal(Known.Replace("1 31 8 9 20 4 ", "1 31 8 "), writer.Text);
	}

	[Fact]
	public void A_second_country_intel_key_adds_its_grants_to_the_first_ones()
	{
		// The original's loader appends each key's triples to what the unit holds (savegame.md, section 5).
		CGameState state = State(out CCountry germany);
		var ids = new CSaveIds();
		ids.SetHistoryNext(100);
		var reader = new CSaveUnitReader(state, ids, new CSaveReport());
		string twice = Known.Replace("1 31 8 9 20 4 \t}", "1 31 8 \t}\r\n\tcountry_intel=\r\n\t{\r\n1 5 3 \t}");
		ClausewitzNode block = ClausewitzParser.Parse(twice).Children[0];
		CUnit unit = reader.Read(block, CSaveUnitReader.UnitKinds[block.Key!], germany, parent: null, carriedBy: null)!;
		reader.Finish();

		Assert.Equal(new[] { (31, 8), (5, 3) }, unit.IntelGrants.Select(g => (g.Days, g.Level)));
	}

	[Fact]
	public void An_intercept_keeps_the_fleet_its_order_names_apart_from_the_one_it_has_found()
	{
		CGameState state = State(out CCountry germany);
		var ids = new CSaveIds();
		ids.SetHistoryNext(100);
		var reader = new CSaveUnitReader(state, ids, new CSaveReport());
		const string Fleets =
			"navy=\r\n{\r\n\tid=\r\n\t{\r\n\t\tid=40\r\n\t\ttype=41\r\n\t}\r\n\tname=\"Enemy\"\r\n\tmovement_progress=0.000\r\n" +
			"\tlocation=3\r\n\tis_prioritized=no\r\n\tcan_reinforce=yes\r\n\tcan_upgrade=yes\r\n\tfuel=1.000\r\n\tsupplies=1.000\r\n" +
			"\tat_sea=0\r\n}\r\n" +
			"navy=\r\n{\r\n\tid=\r\n\t{\r\n\t\tid=41\r\n\t\ttype=41\r\n\t}\r\n\tname=\"Interceptor\"\r\n\tbase=1\r\n" +
			"\tmovement_progress=0.000\r\n\tlocation=1\r\n\tis_prioritized=no\r\n\tcan_reinforce=yes\r\n\tcan_upgrade=yes\r\n" +
			"\tfuel=1.000\r\n\tsupplies=1.000\r\n" +
			"\tintercept=\r\n\t{\r\n\t\tprovince=3\r\n\t\ttarget=\r\n\t\t{\r\n\t\t\tid=40\r\n\t\t\ttype=41\r\n\t\t}\r\n\t\tstance=1\r\n" +
			"\t\tstart_date=\"1937.7.5.2\"\r\n\t\tdeath_date=\"1937.7.5.2\"\r\n\t\treturn_to_base=no\r\n\t\ttime=2\r\n\t\tpriority=0\r\n" +
			"\t\tstop=0.300\r\n\t\treturn_to_base=no\r\n\t\tpriority=0\r\n\t\tcapital=0\r\n\t\tsub_unit=0\r\n\t\tother=0\r\n\t}\r\n" +
			"\tat_sea=0\r\n}\r\n";
		foreach (ClausewitzNode block in ClausewitzParser.Parse(Fleets).Children)
		{
			germany.Units.Add(reader.Read(block, EUnitKind.Navy, germany, parent: null, carriedBy: null)!);
		}
		reader.Finish();
		CUnit interceptor = germany.Units[1];

		// Still waiting: the order names the enemy, and the search has found nobody, so the fleet stays in port.
		Assert.Same(germany.Units[0], interceptor.Mission.OrderTarget);
		Assert.Null(interceptor.Mission.Target);

		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		foreach (CUnit unit in germany.Units)
		{
			CSaveUnitWriter.Write(writer, state, unit);
		}
		Assert.Equal(Fleets, writer.Text);

		// Once it has found the fleet, it is written a second time as the intercept's own, an empty line after it; told to
		// return, the order's return to base is set and the naval order's own, which nothing in play sets, is not.
		interceptor.Mission.Target = germany.Units[0];
		interceptor.Mission.ReturnToBase = true;
		writer = new CSaveTextWriter();
		writer.OpenRoot();
		CSaveUnitWriter.Write(writer, state, interceptor);
		Assert.Contains("\t\tother=0\r\n\t\tprovinces=\r\n\t\t{\r\n\t\t\tid=40\r\n\t\t\ttype=41\r\n\t\t}\r\n\r\n\t}\r\n", writer.Text);
		Assert.Contains("\t\treturn_to_base=yes\r\n\t\ttime=2\r\n\t\tpriority=0\r\n\t\tstop=0.300\r\n\t\treturn_to_base=no\r\n", writer.Text);
	}

	[Fact]
	public void A_raid_on_convoys_from_the_air_is_read_as_one_and_written_back_with_nothing_of_its_hours()
	{
		CGameState state = State(out CCountry germany);
		var ids = new CSaveIds();
		ids.SetHistoryNext(100);
		var reader = new CSaveUnitReader(state, ids, new CSaveReport());
		const string Air =
			"air=\r\n{\r\n\tid=\r\n\t{\r\n\t\tid=50\r\n\t\ttype=41\r\n\t}\r\n\tname=\"Fliegerkorps\"\r\n\tbase=1\r\n" +
			"\tmovement_progress=0.000\r\n\tlocation=1\r\n\tis_prioritized=no\r\n\tcan_reinforce=yes\r\n\tcan_upgrade=yes\r\n" +
			"\tfuel=1.000\r\n\tsupplies=1.000\r\n" +
			"\twing=\r\n\t{\r\n\t\tid=\r\n\t\t{\r\n\t\t\tid=51\r\n\t\t\ttype=41\r\n\t\t}\r\n\t\tname=\"1st CAG\"\r\n\t\ttype=\"cag\"\r\n" +
			"\t\torganisation=60.000\r\n\t\tstrength=100.000\r\n\t\thighest=100.000\r\n\t\texperience=15.000\r\n\t}\r\n" +
			"\tair_convoy_raid=\r\n\t{\r\n\t\tprovince=3\r\n\t\tstance=1\r\n\t\tstart_date=\"1.1.1.0\"\r\n" +
			"\t\tdeath_date=\"1.1.1.0\"\r\n\t\treturn_to_base=no\r\n\t\ttime=2\r\n\t\tpriority=0\r\n\t\tstop=-1.000\r\n" +
			"\t\treturn_to_base=no\r\n\t\tpriority=0\r\n\t\tprovinces=3\r\n\t\tcag=0.000\r\n\t}\r\n}\r\n";
		CUnit raider = reader.Read(ClausewitzParser.Parse(Air).Children[0], EUnitKind.Air, germany, parent: null, carriedBy: null)!;
		germany.Units.Add(raider);
		reader.Finish();

		Assert.Equal(EUnitOrder.AirConvoyRaid, raider.Order);
		Assert.Equal(new[] { 3 }, raider.Mission.Provinces);
		Assert.Equal(0, raider.Mission.RaidCountdown);

		// Its count of hours is not written: the original's air order writes none.
		raider.Mission.RaidCountdown = 7;
		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		CSaveUnitWriter.Write(writer, state, raider);
		Assert.Equal(Air, writer.Text);
	}

	[Fact]
	public void Leaders_a_line_and_the_templates_are_written_as_the_original_writes_them()
	{
		CGameState state = State(out CCountry germany);
		state.HistoryUnitNext = 5608;
		var leader = new CLeader(8) { Rank = 1, Skill = 3, StartingSkill = 2, Experience = new CFixed15(2000L * CFixed15.Scale) };
		leader.Traits.Add(state.Traits[0]);
		leader.Traits.Add(state.Traits[1]);
		leader.TraitProgress.Progress[new OpenHOI3.Sim.Leaders.CGainableTrait(OpenHOI3.Sim.Combat.ECombatKind.Land, state.Traits[1])] = 16400;
		germany.ActiveLeaders.Add(leader);
		var line = new CConstruction(Ger)
		{
			Id = 238, UnitId = 239, Cost = new CFixedPoint(7020), Duration = CFixedPoint.FromInt(383),
			Progress = CFixedPoint.FromInt(315), AccumulatedExperience = new CFixedPoint(390), AccumulatedProgress = new CFixedPoint(18),
		};
		var ship = new CBrigadeOrder(state.SubUnitTypes["carrier"]) { Id = 242, Name = "Graf Spee" };
		ship.Model.AddRange(new[] { 0, 0, 3 });
		line.Brigades.Add(ship);
		germany.Constructions.Add(line);
		var template = new CDivisionTemplate("GER_Infantry_1936");
		template.Brigades.AddRange(new[] { "infantry_brigade", "infantry_brigade" });
		germany.Templates.Add(template);

		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		CSaveLeaders.Write(writer, germany);
		CSaveLines.WriteQueue(writer, state, germany);
		CSaveLines.WriteTemplates(writer, germany);

		// A leader's number stands at column 0 and his traits too; his skill is written for not being his file's; a
		// line's model is indented; the templates are padded to twelve, each empty one named by its place.
		Assert.Equal(
			"active_leaders=\r\n{\r\n8=\r\n\t{\r\n\t\trank=1\r\n\t\tskill=3\r\n\t\tcurrent_experience=2000.00000\r\n" +
			"\t\tgainable_trait=\r\n\t\t{\r\n\t\t\told_guard=16.400\r\n\t\t\tduration=0\r\n\t\t\tcurrent=-1\r\n\t\t}\r\n" +
			"\t\ttrait=\r\n\t\t{\r\nlogistics_wizard old_guard \t\t}\r\n\t}\r\n}\r\n" +
			"military_construction=\r\n{\r\n\tid=\r\n\t{\r\n\t\tid=238\r\n\t\ttype=41\r\n\t}\r\n\tsize=1\r\n\tcost=7.020\r\n" +
			"\tduration=383.000\r\n\tprogress=315.000\r\n\tstatus=1.000\r\n\tcountry=\"GER\"\r\n" +
			"\tunit=\r\n\t{\r\n\t\tid=239\r\n\t\ttype=41\r\n\t}\r\n\tmanpower=0.000\r\n" +
			"\tcarrier=\r\n\t{\r\n\t\tid=\r\n\t\t{\r\n\t\t\tid=242\r\n\t\t\ttype=41\r\n\t\t}\r\n\t\tname=\"Graf Spee\"\r\n" +
			"\t\tmodel=\r\n\t\t{\r\n\t\t\t0 0 3 \t\t}\r\n\t}\r\n\taccumulated_experiance=0.390\r\n\taccumulated_progress=0.018\r\n}\r\n" +
			"saved_templates=\r\n{\r\n\tGER_Infantry_1936=\r\n\t{\r\ninfantry_brigade infantry_brigade \t}\r\n" +
			"\tPW_TEMPLATE_1=\r\n\t{\r\n\t}\r\n",
			writer.Text[..writer.Text.IndexOf("\tPW_TEMPLATE_2", System.StringComparison.Ordinal)]);
		Assert.EndsWith("\tPW_TEMPLATE_11=\r\n\t{\r\n\t}\r\n}\r\n", writer.Text);
	}
}
