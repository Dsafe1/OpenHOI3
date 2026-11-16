using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// What the politics script is handed, against <c>docs/mechanics/politics.md</c>, section 12: the law database with its
/// null group and null law, a country's laws and ministers, the null minister of an empty post, a law command's two
/// flags while a game is being made, the politics minister and the commands it posts, and the AI's draw. Italy, a
/// fascist country, with a press group of two laws and two posts.
/// </summary>
public sealed class LuaPoliticsTests : IDisposable
{
	private static readonly CCountryTag Ita = new("ITA");

	private readonly string _root = Path.Combine(Path.GetTempPath(), "openhoi3-luapolitics-" + Guid.NewGuid().ToString("N"));
	private readonly List<string> _reports = new();

	public LuaPoliticsTests()
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
		var fascism = new CIdeologyGroup("fascism", 0, 0);
		fascism.IdeologyOrder.Add("fascistic");
		fascism.Ideologies.UnionWith(fascism.IdeologyOrder);
		var press = new CLawGroup("press_laws", 1);
		press.Laws.Add(new CLaw(press.Name, new CModifier("free_press")) { Index = 1 });
		press.Laws.Add(new CLaw(press.Name, new CModifier("censored_press")) { Index = 2 });

		CCountry italy = new() { Tag = Ita, Name = "Italy", DefinitionFile = "", Capital = 1 };
		italy.RulingIdeology = "fascistic";
		italy.Laws[press.Name] = press.Laws[0];
		var ciano = new CMinister(7, "Ciano") { Ideology = "fascistic" };
		ciano.SetType("foreign_minister", new CMinisterType(new CModifier("great_compromiser")));
		italy.Ministers[7] = ciano;
		italy.PossibleMinisters.Add(ciano);
		italy.Government["foreign_minister"] = ciano;
		return new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Roma", isSea: false) { Owner = Ita, Controller = Ita } },
			new[] { italy })
		{
			IdeologyGroups = new[] { fascism },
			GovernmentPositions = new[] { new CGovernmentPosition("head_of_state", 1, false), new CGovernmentPosition("foreign_minister", 2, true) },
			LawGroups = new[] { press },
		};
	}

	private const string Italy = "CCountryDataBase.GetTag('ITA'):GetCountry()";

	[Fact]
	public void The_law_database_lists_the_null_group_first_and_numbers_the_laws_from_the_null_law()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		Assert.Equal(3.0, scripts.Ask(state, "CLawDataBase.GetNumberOfLaws()"));
		Assert.Equal("noLawgroup", scripts.Ask(state, "(function() for g in CLawDataBase.GetGroups() do return tostring(g:GetKey()) end end)()"));
		Assert.Equal(0.0, scripts.Ask(state, "(function() for g in CLawDataBase.GetGroups() do return " + Italy + ":GetLaw(g):GetIndex() end end)()"));
		Assert.Equal("press_laws", scripts.Ask(state, "tostring(CLawDataBase.GetLaw(2):GetGroup():GetKey())"));
		Assert.Equal(true, scripts.Ask(state, "CLawDataBase.GetLaw(2):ValidFor(CCountryDataBase.GetTag('ITA'))"));
	}

	[Fact]
	public void An_empty_post_holds_the_null_minister_and_ministers_are_equal_by_number()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		const string HeadOfState = "CGovernmentPositionDataBase.GetGovernmentPositionByIndex(1)";
		const string Foreign = "CGovernmentPositionDataBase.GetGovernmentPositionByIndex(2)";
		Assert.Equal("", scripts.Ask(state, $"tostring({Italy}:GetMinister({HeadOfState}):GetKey())"));
		Assert.Equal(true, scripts.Ask(state,
			$"(function() for m in {Italy}:GetPossibleMinisters() do return m == {Italy}:GetMinister({Foreign}) end end)()"));
		Assert.Equal("great_compromiser", scripts.Ask(state,
			$"tostring({Italy}:GetMinister({Foreign}):GetPersonality({Foreign}):GetKey())"));
		// A post he has no posting for: the null type, noMinisterType.
		Assert.Equal("noMinisterType", scripts.Ask(state, $"tostring({Italy}:GetMinister({Foreign}):GetPersonality({HeadOfState}):GetKey())"));
		Assert.Equal(false, scripts.Ask(state, $"{Italy}:GetMinister({Foreign}):CanTakePosition({HeadOfState})"));
	}

	[Fact]
	public void A_law_command_made_as_a_game_is_made_pays_nothing_and_is_silent_until_told()
	{
		CGameState state = World();
		using CAiScripts scripts = Load(@"
function Laws(country)
	for group in CLawDataBase.GetGroups() do
		if tostring(group:GetKey()) == 'press_laws' then
			local command = CChangeLawCommand(country:GetCountryTag(), CLawDataBase.GetLaw(2), group)
			if TELL then command:SetEnablePostMessage(true) end
			CCurrentGameState.Post(command)
		end
	end
end");
		state.IsBeingMade = true;
		scripts.RunLaws(state, state.Country(Ita)!);
		scripts.Ask(state, "(function() TELL = true end)()");
		scripts.RunLaws(state, state.Country(Ita)!);
		state.IsBeingMade = false;
		scripts.RunLaws(state, state.Country(Ita)!);
		var posted = state.Outbox.ConvertAll(command => (CChangeLawCommand)command);
		Assert.Equal(new[] { (false, false), (false, true), (true, true) }, posted.ConvertAll(c => (c.PayCost, c.PostMessage)));
		Assert.All(posted, c => Assert.Equal(("censored_press", "press_laws"), (c.Law, c.Group)));
		Assert.Empty(_reports);
	}

	[Fact]
	public void The_tick_is_handed_a_minister_whose_ai_posts_and_a_liberation_is_posted_like_the_rest()
	{
		CGameState state = World();
		using CAiScripts scripts = Load(@"
function PoliticsMinister_Tick(minister)
	local tag = minister:GetCountryTag()
	local ai = minister:GetOwnerAI()
	ai:Post(CToggleMobilizationCommand(tag, true))
	local foreign = CGovernmentPositionDataBase.GetGovernmentPositionByIndex(2)
	for m in tag:GetCountry():GetPossibleMinisters() do
		ai:Post(CChangeMinisterCommand(tag, m, foreign))
	end
	ai:Post(CLiberateCountryCommand(tag, tag))
end");
		scripts.RunPolitics(state, state.Country(Ita)!);
		Assert.Equal(3, state.Outbox.Count);
		Assert.True(state.Outbox[0] is CToggleMobilisationCommand { Mobilise: true });
		Assert.True(state.Outbox[1] is CChangeMinisterCommand { Minister: 7, Position: "foreign_minister" });
		Assert.IsType<CLiberateCountryCommand>(state.Outbox[2]);
	}

	[Fact]
	public void The_ais_draw_is_the_games_generators_next()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		var saved = state.Random.Snapshot();
		int expected = state.Random.Next();
		state.Random.Restore(saved);
		Assert.Equal((double)expected, scripts.Ask(state, "CCurrentGameState.GetAIRand()"));
	}
}
