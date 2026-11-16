using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content;
using OpenHOI3.Lua;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// The parts of the original's Lua API BlackICE's scripts call and the base game's do not, against
/// <c>docs/mechanics/lua.md</c>, sections 4.4 to 4.7 and 5.1: a country's variables and the command that sets one, a
/// <c>CString</c> made of a string, a country's units and their counts, its shares and their need, the second kind of
/// number, its free spies, allies, provinces, cabinet and overlord, a faction's members, any agreement, a unit type's
/// transport flag, a building's working level, every modifier kind by its number, the minister type database and a
/// minister's country. Germany owns Berlin (1) and Kiel (2) and leads the axis with Italy, which owns Roma (3).
/// </summary>
public sealed class LuaModApiTests : IDisposable
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Ita = new("ITA");

	private readonly string _root = Path.Combine(Path.GetTempPath(), "openhoi3-luamodapi-" + Guid.NewGuid().ToString("N"));
	private readonly List<string> _reports = new();

	public LuaModApiTests()
	{
		Directory.CreateDirectory(Path.Combine(_root, "script"));
	}

	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	private CAiScripts Load(string autoexec = "")
	{
		File.WriteAllText(Path.Combine(_root, "script", "autoexec.lua"), autoexec);
		return new CAiScripts(CDirectorySettings.At(_root), _reports.Add);
	}

	private static readonly CBuilding Industry = new(0, "industry", 10, "ic", CFixedPoint.One);

	private static readonly CSubUnitType Infantry = new("infantry_brigade", EUnitBranch.Land);
	private static readonly CSubUnitType Destroyer = new("destroyer", EUnitBranch.Naval);
	private static readonly CSubUnitType Transport = TransportType();
	private static readonly CSubUnitType Fighter = new("interceptor", EUnitBranch.Air);

	private static CSubUnitType TransportType()
	{
		var type = new CSubUnitType("transport_ship", EUnitBranch.Naval);
		type.Flags["transport"] = true;
		return type;
	}

	private static CGameState World()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1, Faction = "axis", FactionSeat = 1 };
		var italy = new CCountry { Tag = Ita, Name = "Italy", DefinitionFile = "", Capital = 3, Faction = "axis", FactionSeat = 2 };
		var types = new List<CMinisterType>
		{
			new(new CModifier("great_compromiser")),
			new(new CModifier("iron_fisted_brute")),
		};
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(2, "Kiel", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(3, "Roma", isSea: false) { Owner = Ita, Controller = Ita },
			},
			new[] { italy, germany }, buildings: new[] { Industry })
		{
			SubUnitTypes = new Dictionary<string, CSubUnitType>(StringComparer.OrdinalIgnoreCase)
			{
				[Infantry.Key] = Infantry, [Destroyer.Key] = Destroyer, [Transport.Key] = Transport, [Fighter.Key] = Fighter,
			},
			GovernmentPositions = new[] { new CGovernmentPosition("head_of_state", 1, false), new CGovernmentPosition("foreign_minister", 2, true) },
			MinisterTypes = types,
		};
	}

	private const string Germany = "CCountryDataBase.GetTag('GER'):GetCountry()";
	private const string Tag = "CCountryDataBase.GetTag('GER')";

	private static CUnit Unit(EUnitKind kind, params CSubUnitType[] brigades)
	{
		var unit = new CUnit(kind, Ger);
		foreach (CSubUnitType type in brigades)
		{
			unit.SubUnits.Add(new CSubUnit(type) { Strength = CFixedPoint.FromInt(2) });
		}
		return unit;
	}

	[Fact]
	public void A_variable_reads_its_value_and_a_missing_one_nought_without_being_made()
	{
		CGameState state = World();
		state.Country(Ger)!.Variables["BaseIC"] = CFixedPoint.FromInt(35);
		using CAiScripts scripts = Load();

		Assert.Equal(35.0, scripts.Ask(state, $"{Germany}:GetVariables():GetVariable(CString('baseic')):Get()"));
		Assert.Equal(0.0, scripts.Ask(state, $"{Germany}:GetVariables():GetVariable(CString('never_set')):Get()"));
		Assert.False(state.Country(Ger)!.Variables.ContainsKey("never_set"));
		// The name must be a CString, as the original's takes one.
		Assert.Throws<CLuaException>(() => scripts.Ask(state, $"{Germany}:GetVariables():GetVariable('BaseIC')"));
	}

	[Fact]
	public void CSetVariableCommand_is_posted_and_sets_the_variable_making_it_where_missing()
	{
		CGameState state = World();
		state.Country(Ger)!.Variables["Count"] = CFixedPoint.FromInt(1);
		using CAiScripts scripts = Load();

		scripts.Ask(state, $"CCurrentGameState.Post(CSetVariableCommand({Tag}, CString('count'), CFixedPoint(7)))");
		scripts.Ask(state, $"CCurrentGameState.Post(CSetVariableCommand({Tag}, CString('fresh'), CFixedPoint(0.25)))");

		Assert.Equal(2, state.Outbox.Count);
		var first = Assert.IsType<CSetVariableCommand>(state.Outbox[0]);
		Assert.Equal(("count", 7000L), (first.Name, first.Value.Raw));
		Assert.True(first.MayExecute(state));
		foreach (CCommand command in state.Outbox)
		{
			command.Execute(state);
		}
		Assert.Equal(7000, state.Country(Ger)!.Variables["Count"].Raw);
		Assert.Equal(250, state.Country(Ger)!.Variables["fresh"].Raw);
		// Nothing in the scripts' state changed before the command ran: the reads are of the game.
		Assert.False(new CSetVariableCommand(new CCountryTag("XXX"), "a", CFixedPoint.One).MayExecute(state));
	}

	[Fact]
	public void CSetVariableCommand_goes_into_a_recording_and_comes_back_the_same()
	{
		CGameState state = World();
		using var buffer = new MemoryStream();
		using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
		{
			OpenHOI3.Sim.Replay.CCommandCodec.Write(writer, new CSetVariableCommand(Ger, "want_date", new CFixedPoint(12345)));
		}
		using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(buffer.ToArray());

		var back = Assert.IsType<CSetVariableCommand>(OpenHOI3.Sim.Replay.CCommandCodec.Read(document.RootElement, state));
		Assert.Equal((Ger, "want_date", 12345L), (back.Country, back.Name, back.Value.Raw));
	}

	[Fact]
	public void CSetVariableCommand_takes_exactly_a_tag_a_CString_and_a_CFixedPoint()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();

		Assert.Throws<CLuaException>(() => scripts.Ask(state, $"CSetVariableCommand({Tag}, 'name', CFixedPoint(1))"));
		Assert.Throws<CLuaException>(() => scripts.Ask(state, $"CSetVariableCommand({Tag}, CString('name'), 1)"));
		Assert.Throws<CLuaException>(() => scripts.Ask(state, $"CSetVariableCommand({Tag}, CString('name'))"));
	}

	[Fact]
	public void CString_is_made_of_a_string_and_of_nothing_else()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();

		Assert.Equal("axis", scripts.Ask(state, "tostring(CString('axis'))"));
		Assert.Equal(true, scripts.Ask(state, "CString('axis') == CString('axis')"));
		Assert.Throws<CLuaException>(() => scripts.Ask(state, "CString(5)"));
		Assert.Throws<CLuaException>(() => scripts.Ask(state, "CString()"));
	}

	[Fact]
	public void A_countrys_units_are_counted_at_every_level_by_kind_and_a_division_is_a_land_unit_of_two_brigades_or_more()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CUnit army = Unit(EUnitKind.Army, Infantry);
		CUnit corps = Unit(EUnitKind.Corps, Infantry);
		CUnit division = Unit(EUnitKind.Division, Infantry, Infantry, Infantry);
		CUnit lone = Unit(EUnitKind.Division, Infantry);
		corps.Children.Add(division);
		corps.Children.Add(lone);
		army.Children.Add(corps);
		germany.Units.Add(army);
		germany.Units.Add(Unit(EUnitKind.Navy, Destroyer, Transport, Transport));
		germany.Units.Add(Unit(EUnitKind.Air, Fighter, Fighter));
		using CAiScripts scripts = Load();
		object? Ask(string method) => scripts.Ask(state, $"{Germany}:GetUnits():{method}");

		Assert.Equal(1.0, Ask("GetTotalAmountOfDivisions()"));
		Assert.Equal(4.0, Ask("GetTotalAmountOfArmies()"));
		Assert.Equal(6.0, Ask("GetTotalNumOfRegiments()"));
		Assert.Equal(3.0, Ask("GetTotalNumOfShips()"));
		Assert.Equal(1.0, Ask("GetTotalNumOfWarShips()"));
		Assert.Equal(2.0, Ask("GetTotalNumOfTransports()"));
		Assert.Equal(2.0, Ask("GetTotalNumOfPlanes()"));
		Assert.Equal(6.0, Ask("GetCount(CSubUnitDataBase.GetSubUnit('infantry_brigade'))"));
		Assert.Equal(22.0, Ask("GetTotalStrength():Get()"));
	}

	[Fact]
	public void A_share_answers_its_percentage_and_its_need_in_the_second_kind_of_number()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		germany.ProductionShares[4] = new CFixed15(6554);
		germany.LeadershipShares[3] = new CFixed15(32768);
		germany.ReinforcementCost = new CFixedPoint(1500);
		germany.ResearchQueue.Add("radar");
		germany.ResearchQueue.Add("smallarms_technology");
		using CAiScripts scripts = Load();
		string Production(int index) => $"{Germany}:GetProductionDistributionAt({index})";
		string Leadership(int index) => $"{Germany}:GetLeadershipDistributionAt({index})";

		Assert.Equal((double)(float)(6554 / 32768.0),
			scripts.Ask(state, Production(4) + ":GetPercentage():Get()"));
		Assert.Equal(1.5, scripts.Ask(state, Production(4) + ":GetNeeded():Get()"));
		Assert.Equal(1.0, scripts.Ask(state, Leadership(3) + ":GetPercentage():Get()"));
		Assert.Equal(2.0, scripts.Ask(state, Leadership(3) + ":GetNeeded():Get()"));
		// Lend-lease, officers and espionage need nothing.
		Assert.Equal(0.0, scripts.Ask(state, Production(0) + ":GetNeeded():Get()"));
		Assert.Equal(0.0, scripts.Ask(state, Leadership(0) + ":GetNeeded():Get()"));
		Assert.Equal(4.0, scripts.Ask(state, "CDistributionSetting._PRODUCTION_REINFORCEMENT_"));
		Assert.Throws<CLuaException>(() => scripts.Ask(state, Production(6)));
	}

	[Fact]
	public void The_second_kind_of_number_rounds_to_a_float_and_cuts_toward_minus_infinity_under_both_names()
	{
		Assert.Equal((float)(6554 / 32768.0), OpenHOI3.Lua.Api.CFixedPoint64Class.Get(6554));
		Assert.Equal(3, OpenHOI3.Lua.Api.CFixedPoint64Class.Whole(3 * 32768 + 5));
		Assert.Equal(-1, OpenHOI3.Lua.Api.CFixedPoint64Class.Whole(-1));

		CGameState state = World();
		state.Country(Ger)!.ProductionShares[1] = new CFixed15(-5);
		using CAiScripts scripts = Load();
		string share = $"{Germany}:GetProductionDistributionAt(1):GetPercentage()";
		Assert.Equal(-1.0, scripts.Ask(state, share + ":GetTruncated()"));
		Assert.Equal(-1.0, scripts.Ask(state, share + ":GetRounded()"));
	}

	[Fact]
	public void Free_spies_are_whole_allies_counted_provinces_listed_and_a_subject_has_an_overlord()
	{
		CGameState state = World();
		state.Country(Ger)!.FreeSpies = new CFixedPoint(2999);
		state.Country(Ita)!.Overlord = Ger;
		using CAiScripts scripts = Load();

		Assert.Equal(2.0, scripts.Ask(state, $"{Germany}:GetNumberOfFreeSpies()"));
		Assert.Equal(1.0, scripts.Ask(state, $"{Germany}:GetNumOfAllies()"));
		Assert.Equal("1,2", scripts.Ask(state, $"(function() local t = {{}} for id in {Germany}:GetOwnedProvinces() do t[#t + 1] = id end return table.concat(t, ',') end)()"));
		Assert.Equal(false, scripts.Ask(state, $"{Germany}:IsSubject()"));
		Assert.Equal(true, scripts.Ask(state, "CCountryDataBase.GetTag('ITA'):GetCountry():IsSubject()"));
	}

	[Fact]
	public void The_cabinet_runs_from_the_null_post_through_every_post_an_empty_one_the_null_minister()
	{
		CGameState state = World();
		var leader = new CMinister(7, "Hitler");
		state.Country(Ger)!.Government["head_of_state"] = leader;
		using CAiScripts scripts = Load();

		Assert.Equal("0,7,0", scripts.Ask(state,
			$"(function() local t = {{}} for m in {Germany}:GetMinisters() do t[#t + 1] = m:GetKey() == CString('Hitler') and 7 or 0 end return table.concat(t, ',') end)()"));
	}

	[Fact]
	public void A_factions_members_come_in_its_order_and_any_agreement_takes_in_war()
	{
		CGameState state = World();
		state.Alliances.Add(new CAlliance(Ger, Ita));
		using CAiScripts scripts = Load();

		Assert.Equal("GER,ITA", scripts.Ask(state,
			"(function() local t = {} for tag in CCountryDataBase.GetTag('ITA'):GetCountry():GetFaction():GetMembers() do t[#t + 1] = tostring(tag) end return table.concat(t, ',') end)()"));
		Assert.Equal(true, scripts.Ask(state, $"{Germany}:GetRelation(CCountryDataBase.GetTag('ITA')):HasAnyAgreement()"));
		state.Alliances.Clear();
		Assert.Equal(false, scripts.Ask(state, $"{Germany}:GetRelation(CCountryDataBase.GetTag('ITA')):HasAnyAgreement()"));
	}

	[Fact]
	public void A_transport_is_its_types_flag_and_a_building_answers_its_working_level()
	{
		CGameState state = World();
		CProvince berlin = state.Province(1);
		berlin.SetBuildingLevel(Industry, CFixedPoint.FromInt(4));
		berlin.SetBuildingCurrentLevel(Industry, new CFixedPoint(2500));
		using CAiScripts scripts = Load();

		Assert.Equal(true, scripts.Ask(state, "CSubUnitDataBase.GetSubUnit('transport_ship'):IsTransport()"));
		Assert.Equal(false, scripts.Ask(state, "CSubUnitDataBase.GetSubUnit('destroyer'):IsTransport()"));
		string building = "CCurrentGameState.GetProvince(1):GetBuilding(CBuildingDataBase.GetBuilding('industry'))";
		Assert.Equal(2.5, scripts.Ask(state, building + ":GetCurrent():Get()"));
		Assert.Equal(4.0, scripts.Ask(state, building + ":GetMax():Get()"));
	}

	[Fact]
	public void Every_modifier_kind_is_read_by_its_number_and_the_constants_skip_57()
	{
		CGameState state = World();
		state.Country(Ger)!.Modifier.Set("industrial_efficiency", new CFixedPoint(150));
		state.Country(Ger)!.Modifier.Set("threat_resistance", new CFixedPoint(-200));
		using CAiScripts scripts = Load();
		string modifier = $"{Germany}:GetGlobalModifier()";

		Assert.Equal(74.0, scripts.Ask(state, "CModifier._MODIFIER_INDUSTRIAL_EFFICIENCY_"));
		Assert.Equal(79.0, scripts.Ask(state, "CModifier._MODIFIER_PARTISAN_EFFICENCY_"));
		Assert.Equal(82.0, scripts.Ask(state, "CModifier._MODIFIER_OFFICER_RECRUITMENT_"));
		Assert.Null(scripts.Ask(state, "CModifier._MODIFIER_THREAT_RESISTANCE_"));
		Assert.Equal((double)(float)0.15, scripts.Ask(state, modifier + ":GetValue(CModifier._MODIFIER_INDUSTRIAL_EFFICIENCY_):Get()"));
		Assert.Equal((double)(float)-0.2, scripts.Ask(state, modifier + ":GetValue(57):Get()"));
		Assert.Equal(0.0, scripts.Ask(state, modifier + ":GetValue(200):Get()"));
		Assert.Contains(_reports, report => report.Contains("numbered 200"));
	}

	[Fact]
	public void The_minister_type_database_begins_with_the_null_type()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();

		Assert.Equal("noMinisterType,great_compromiser,iron_fisted_brute", scripts.Ask(state,
			"(function() local t = {} for m in CMinisterTypeDataBase.GetMinisterTypeList() do t[#t + 1] = tostring(m:GetKey()) end return table.concat(t, ',') end)()"));
		Assert.Equal(3.0, scripts.Ask(state, "CMinisterTypeDataBase.GetNumberOfMinisterTypes()"));
		Assert.Equal("iron_fisted_brute", scripts.Ask(state, "tostring(CMinisterTypeDataBase.GetMinisterType('iron_fisted_brute'):GetKey())"));
		Assert.Null(scripts.Ask(state, "CMinisterTypeDataBase.GetMinisterType('Iron_Fisted_Brute')"));
	}

	[Fact]
	public void Every_minister_answers_its_country()
	{
		CGameState state = World();
		using CAiScripts scripts = Load(
			"function PoliticsMinister_Tick(m) Politics = tostring(m:GetCountry():GetCountryTag()) end\n"
			+ "function IntelligenceMinister_Tick(m) Intelligence = tostring(m:GetCountry():GetCountryTag()) end");

		scripts.RunPolitics(state, state.Country(Ger)!);
		scripts.RunIntelligence(state, state.Country(Ita)!);

		Assert.Equal("GER", scripts.Ask(state, "Politics"));
		Assert.Equal("ITA", scripts.Ask(state, "Intelligence"));
		Assert.Empty(_reports);
	}
}
