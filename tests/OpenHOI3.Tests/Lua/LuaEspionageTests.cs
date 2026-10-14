using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// What the intelligence script is handed, against <c>docs/mechanics/espionage.md</c>, section 15: a country's spies in
/// another and their methods, the static <c>MissionAllowed</c>, who is spying on us, faction members as allies, the
/// minister and the two commands with their exact arguments. Italy and Germany, both of the axis, each owning a province.
/// </summary>
public sealed class LuaEspionageTests : IDisposable
{
	private static readonly CCountryTag Ita = new("ITA");
	private static readonly CCountryTag Ger = new("GER");

	private readonly string _root = Path.Combine(Path.GetTempPath(), "openhoi3-luaespionage-" + Guid.NewGuid().ToString("N"));
	private readonly List<string> _reports = new();

	public LuaEspionageTests()
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

	private static CGameState World()
	{
		CCountry italy = new() { Tag = Ita, Name = "Italy", DefinitionFile = "", Capital = 1, Faction = "axis" };
		CCountry germany = new() { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 2, Faction = "axis" };
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false), new CProvince(1, "Roma", isSea: false) { Owner = Ita, Controller = Ita },
				new CProvince(2, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
			},
			new[] { italy, germany });
	}

	private const string Italy = "CCountryDataBase.GetTag('ITA'):GetCountry()";
	private const string Germany = "CCountryDataBase.GetTag('GER')";

	[Fact]
	public void A_presence_answers_its_level_priority_weights_and_primary_mission()
	{
		CGameState state = World();
		CSpyPresence presence = CGameState.Presence(state.Country(Ita)!, state.Country(Ger)!);
		presence.Level = 3500;
		presence.Priority = 2;
		using CAiScripts scripts = Load();

		Assert.Equal(3.5, scripts.Ask(state, $"{Italy}:GetSpyPresence({Germany}):GetLevel():Get()"));
		Assert.Equal(2.0, scripts.Ask(state, $"{Italy}:GetSpyPresence({Germany}):GetPriority()"));
		Assert.Equal(9.0, scripts.Ask(state, $"{Italy}:GetSpyPresence({Germany}):GetPrimaryMission()"));

		presence.SetMission((int)ESpyMission.Technology, 2);
		presence.SetMission((int)ESpyMission.Military, 2);
		Assert.Equal(1.0, scripts.Ask(state, $"{Italy}:GetSpyPresence({Germany}):GetPrimaryMission()"));
		Assert.Equal(2.0, scripts.Ask(state, $"{Italy}:GetSpyPresence({Germany}):GetMissionPriority(SpyMission.SPYMISSION_TECH)"));
		Assert.Equal(3.0, scripts.Ask(state, "CSpyPresence.MAX_SPY_PRIORITY"));
	}

	[Fact]
	public void Mission_allowed_keeps_raising_unity_at_home_and_the_hostile_six_abroad()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();

		Assert.Equal(true, scripts.Ask(state, $"CSpyPresence.MissionAllowed(SpyMission.SPYMISSION_RAISE_NATIONAL_UNITY, {Germany}, {Germany})"));
		Assert.Equal(false, scripts.Ask(state, $"CSpyPresence.MissionAllowed(SpyMission.SPYMISSION_TECH, {Germany}, {Germany})"));
		Assert.Equal(true, scripts.Ask(state, $"CSpyPresence.MissionAllowed(SpyMission.SPYMISSION_BOOST_RULING_PARTY, {Germany}, {Germany})"));
	}

	[Fact]
	public void A_faction_member_is_allied_and_a_country_with_spies_here_is_spying_on_us()
	{
		CGameState state = World();
		CGameState.Presence(state.Country(Ger)!, state.Country(Ita)!).Level = 500;
		using CAiScripts scripts = Load();

		Assert.Equal(true, scripts.Ask(state, $"{Italy}:CalculateIsAllied({Germany})"));
		Assert.Equal("GER", scripts.Ask(state, $"(function() for t in {Italy}:GetSpyingOnUs() do return tostring(t) end end)()"));
		Assert.Equal(1.0, scripts.Ask(state, $"{Italy}:GetCapitalLocation():GetProvinceID()"));
	}

	[Fact]
	public void The_tick_is_handed_a_minister_whose_ai_posts_the_two_commands()
	{
		CGameState state = World();
		using CAiScripts scripts = Load(@"
function IntelligenceMinister_Tick(minister)
	local tag = minister:GetCountryTag()
	local ai = minister:GetOwnerAI()
	ai:Post(CChangeSpyPriority(tag, CCountryDataBase.GetTag('GER'), 3))
	ai:Post(CChangeSpyMission(tag, CCountryDataBase.GetTag('GER'), SpyMission.SPYMISSION_TECH, 2))
end");

		scripts.RunIntelligence(state, state.Country(Ita)!);

		Assert.Equal(2, state.Outbox.Count);
		Assert.True(state.Outbox[0] is CChangeSpyPriorityCommand { Priority: 3 });
		Assert.True(state.Outbox[1] is CChangeSpyMissionCommand { Mission: 2, Priority: 2 });
	}

	[Fact]
	public void A_command_made_with_the_wrong_number_of_arguments_is_luabind_s_error()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();

		Assert.Equal(false, scripts.Ask(state, $"pcall(function() return CChangeSpyMission({Germany}, {Germany}, 2) end)"));
		Assert.Equal(true, scripts.Ask(state, $"pcall(function() return CChangeSpyPriority({Germany}, {Germany}, 2) end)"));
	}
}
