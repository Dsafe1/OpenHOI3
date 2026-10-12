using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// The install's Lua as the game runs it, against <c>docs/mechanics/lua.md</c>, on an install written
/// here - the scripts are this test's own, in the original's shape - and on <see cref="ScriptWorld"/>,
/// where Germany and Italy are in the Axis and the others in no faction.
/// </summary>
public sealed class AiScriptsTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "openhoi3-lua-" + Guid.NewGuid().ToString("N"));
	private readonly List<string> _reports = new();

	public AiScriptsTests()
	{
		Directory.CreateDirectory(_root);
	}

	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	private void Write(string relative, string text)
	{
		string path = Path.Combine(_root, relative);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, text);
	}

	private CAiScripts Load() => new(CDirectorySettings.At(_root), _reports.Add);

	private void WriteRelationScript(string body)
	{
		Write("script/autoexec.lua", "require('helpers')\nrequire('ai_country')\n");
		Write("common/helpers.lua", "BASE = 25\n");
		Write("script/ai_country.lua", "function AI_GenerateNonHistoricalRelation(country, target)\n" + body + "\nend\n");
	}

	[Fact]
	public void A_new_games_relation_is_asked_of_the_script_with_the_country_and_a_tag()
	{
		WriteRelationScript(@"
			local relation = country:GetRelation(target)
			local other = target:GetCountry()
			local desired = BASE
			if relation:HasWar() then desired = desired - 200 end
			if country:GetFaction() == other:GetFaction() then desired = desired + 50 else desired = desired - 25 end
			return desired");
		CGameState state = ScriptWorld.State(atWar: true);
		using CAiScripts scripts = Load();

		Assert.Equal(75, scripts.NonHistoricalRelation(state, state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Ita)!));
		Assert.Equal(-200, scripts.NonHistoricalRelation(state, state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Pol)!));
		// Two countries in no faction share the null faction, so they are "the same".
		Assert.Equal(75, scripts.NonHistoricalRelation(state, state.Country(ScriptWorld.Pol)!, state.Country(ScriptWorld.Slo)!));
		Assert.Empty(_reports);
	}

	[Fact]
	public void Governments_are_equal_where_they_are_one_and_the_alliance_test_reads_the_game()
	{
		WriteRelationScript(@"
			local other = target:GetCountry()
			local same = 0
			if country:GetGovernment() == other:GetGovernment() then same = same + 1 end
			if country:GetRelation(target):HasAlliance() then same = same + 10 end
			if country:IsAtWar() then same = same + 100 end
			return same");
		CGameState state = ScriptWorld.State();
		state.Country(ScriptWorld.Ger)!.GovernmentType = "national_socialism";
		state.Country(ScriptWorld.Ita)!.GovernmentType = "fascist_republic";
		state.Country(ScriptWorld.Pol)!.GovernmentType = "national_socialism";
		state.Alliances.Add(new CAlliance(ScriptWorld.Ger, ScriptWorld.Ita));
		using CAiScripts scripts = Load();

		Assert.Equal(1, scripts.NonHistoricalRelation(state, state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Pol)!));
		Assert.Equal(10, scripts.NonHistoricalRelation(state, state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Ita)!));
	}

	[Fact]
	public void The_answer_is_rounded_to_the_nearest_with_ties_to_even()
	{
		WriteRelationScript("return BASE + 59.5");
		CGameState state = ScriptWorld.State();
		using CAiScripts scripts = Load();

		Assert.Equal(84, scripts.NonHistoricalRelation(state, state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Ita)!));
		Assert.Equal(86, CAiScripts.ToInteger(85.5));
		Assert.Equal(-2, CAiScripts.ToInteger(-2.5));
	}

	[Fact]
	public void An_error_a_missing_function_or_no_number_gives_25_and_is_reported()
	{
		WriteRelationScript("error('broken')");
		CGameState state = ScriptWorld.State();
		using (CAiScripts scripts = Load())
		{
			Assert.Equal(25, scripts.NonHistoricalRelation(state, state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Ita)!));
		}
		WriteRelationScript("return 'seventy'");
		using (CAiScripts scripts = Load())
		{
			Assert.Equal(25, scripts.NonHistoricalRelation(state, state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Ita)!));
		}
		Write("script/ai_country.lua", "-- nothing");
		using (CAiScripts scripts = Load())
		{
			Assert.Equal(25, scripts.NonHistoricalRelation(state, state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Ita)!));
		}
		Assert.Equal(3, _reports.Count);
		Assert.All(_reports, report => Assert.StartsWith("LUA Error: ", report));
	}

	[Fact]
	public void A_file_in_tfh_is_read_before_the_base_games_and_a_module_is_found_in_its_folders()
	{
		WriteRelationScript("return 1");
		Write("tfh/script/ai_country.lua", "function AI_GenerateNonHistoricalRelation(country, target) return BASE + 2 end");
		CGameState state = ScriptWorld.State();
		using CAiScripts scripts = Load();

		Assert.Equal(27, scripts.NonHistoricalRelation(state, state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Ita)!));
	}

	[Fact]
	public void An_autoexec_that_fails_is_reported_and_what_ran_before_stays()
	{
		Write("script/autoexec.lua", "FIRST = 1\nrequire('nowhere')\nSECOND = 2\n");
		using CAiScripts scripts = Load();

		Assert.Equal(1.0, scripts.Lua.GetGlobal("FIRST"));
		Assert.Null(scripts.Lua.GetGlobal("SECOND"));
		Assert.Single(_reports);
		Assert.Contains("nowhere", _reports[0]);
	}
}
