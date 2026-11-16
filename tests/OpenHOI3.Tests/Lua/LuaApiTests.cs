using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content;
using OpenHOI3.Lua;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// The classes the AI's functions are handed, against <c>docs/mechanics/lua.md</c>, sections 4 to 6,
/// on <see cref="ScriptWorld"/> - Germany and Italy the Axis, Poland beside Germany, Slovakia owning
/// nothing - with the fascist group holding the Axis.
/// </summary>
public sealed class LuaApiTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "openhoi3-luaapi-" + Guid.NewGuid().ToString("N"));
	private readonly List<string> _reports = new();

	public LuaApiTests()
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

	private static CGameState World(bool atWar = false)
	{
		var fascism = new CIdeologyGroup("fascism", 0, 0) { Faction = "axis" };
		fascism.Ideologies.Add("national_socialist");
		var democracy = new CIdeologyGroup("democracy", 0, 0) { Faction = "allies" };
		democracy.Ideologies.Add("social_conservative");
		CGameState state = ScriptWorld.State(atWar, new[] { fascism, democracy });
		state.Country(ScriptWorld.Ger)!.RulingIdeology = "national_socialist";
		state.Country(ScriptWorld.Pol)!.RulingIdeology = "social_conservative";
		return state;
	}

	private const string Ger = "CCountryDataBase.GetTag('GER')";
	private const string Pol = "CCountryDataBase.GetTag('POL')";
	private const string Ita = "CCountryDataBase.GetTag('ITA')";
	private const string Slo = "CCountryDataBase.GetTag('SLO')";

	[Fact]
	public void The_autoexec_may_read_SpyMission_as_it_loads()
	{
		using CAiScripts scripts = Load("TECH = SpyMission.SPYMISSION_TECH\nMONEY = CGoodsPool._MONEY_");
		Assert.Equal(2.0, scripts.Lua.GetGlobal("TECH"));
		Assert.Equal(2.0, scripts.Lua.GetGlobal("MONEY"));
		Assert.Empty(_reports);
	}

	[Fact]
	public void A_fixed_point_reads_as_a_single_precision_float_and_truncates_toward_nought()
	{
		CGameState state = World();
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		germany.EffectiveNeutrality = CFixedPoint.Parse("0.1");
		germany.Neutrality = CFixedPoint.Parse("-2.5");
		using CAiScripts scripts = Load();
		Assert.Equal((double)0.1f, scripts.Ask(state, Ger + ":GetCountry():GetEffectiveNeutrality():Get()"));
		Assert.Equal(-2.0, scripts.Ask(state, Ger + ":GetCountry():GetNeutrality():GetTruncated()"));
	}

	[Fact]
	public void Fixed_point_arithmetic_with_a_number_on_the_left_is_a_number_and_runs_backwards_for_minus_and_divide()
	{
		CGameState state = World();
		state.Country(ScriptWorld.Ger)!.Neutrality = CFixedPoint.FromInt(2);
		state.Country(ScriptWorld.Pol)!.Neutrality = CFixedPoint.Zero;
		using CAiScripts scripts = Load();
		string two = Ger + ":GetCountry():GetNeutrality()";
		string nought = Pol + ":GetCountry():GetNeutrality()";
		Assert.Equal(2.5, scripts.Ask(state, $"({two} + 0.5):Get()"));
		Assert.Equal(1.0, scripts.Ask(state, $"({two} * 0.5):Get()"));
		Assert.Equal(4.0, scripts.Ask(state, $"({two} * {two}):Get()"));
		Assert.Equal(2.5, scripts.Ask(state, $"0.5 + {two}"));
		// n - fp is fp - n, and n / fp is fp / n.
		Assert.Equal(1.5, scripts.Ask(state, $"0.5 - {two}"));
		Assert.Equal((double)0.666f, scripts.Ask(state, $"3 / {two}"));
		// Nought below a division is -1 thousandth, not an error.
		Assert.Equal((double)-0.001f, scripts.Ask(state, $"({two} / {nought}):Get()"));
		Assert.Equal(true, scripts.Ask(state, $"{nought} < {two}"));
		Assert.Equal(false, scripts.Ask(state, $"{two} == 2"));
		Assert.Throws<CLuaException>(() => scripts.Ask(state, $"{two} < 3"));
	}

	[Fact]
	public void A_number_becomes_thousandths_through_single_precision_and_a_half_thousandth()
	{
		Assert.Equal(500, OpenHOI3.Lua.Api.CLuaFloats.ToThousandths(0.5));
		Assert.Equal(1000, OpenHOI3.Lua.Api.CLuaFloats.ToThousandths(0.9995));
		Assert.Equal(-1000, OpenHOI3.Lua.Api.CLuaFloats.ToThousandths(-1));
		Assert.Equal(5000, OpenHOI3.Lua.Api.CLuaFloats.AlignmentDistance(0, 0, 3000, 4000));
	}

	[Fact]
	public void A_tag_is_its_letters_two_of_one_country_are_equal_and_the_null_tag_is_not_valid()
	{
		CGameState state = World();
		state.Country(ScriptWorld.Slo)!.Overlord = ScriptWorld.Ger;
		using CAiScripts scripts = Load();
		Assert.Equal("GER", scripts.Ask(state, $"tostring({Ger})"));
		Assert.Equal(true, scripts.Ask(state, $"{Ger} == {Ger}:GetCountry():GetCountryTag()"));
		Assert.Equal(false, scripts.Ask(state, $"{Ger} == {Pol}"));
		Assert.Equal("---", scripts.Ask(state, $"tostring({Ger}:GetCountry():GetOverlord())"));
		Assert.Equal(false, scripts.Ask(state, $"{Ger}:GetCountry():GetOverlord():IsValid()"));
		Assert.Equal(false, scripts.Ask(state, "CCountryDataBase.GetTag('XYZ'):IsValid()"));
		Assert.Equal(true, scripts.Ask(state, $"{Slo}:GetCountry():GetOverlord() == {Ger}"));
		Assert.Equal(true, scripts.Ask(state, $"{Slo}:GetCountry():IsPuppet()"));
	}

	[Fact]
	public void Two_countries_cannot_be_compared_even_where_they_are_one()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		var thrown = Assert.Throws<CLuaException>(() => scripts.Ask(state, $"{Ger}:GetCountry() == {Ger}:GetCountry()"));
		Assert.Contains("No such operator defined", thrown.Message);
	}

	[Fact]
	public void Factions_are_equal_where_they_are_one_and_the_null_faction_is_shared_and_not_valid()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		Assert.Equal(true, scripts.Ask(state, $"{Ger}:GetCountry():GetFaction() == {Ita}:GetCountry():GetFaction()"));
		Assert.Equal(true, scripts.Ask(state, $"{Pol}:GetCountry():GetFaction() == {Slo}:GetCountry():GetFaction()"));
		Assert.Equal(false, scripts.Ask(state, $"{Pol}:GetCountry():GetFaction():IsValid()"));
		Assert.Equal(false, scripts.Ask(state, $"{Pol}:GetCountry():HasFaction()"));
		Assert.Equal("axis", scripts.Ask(state, $"tostring({Ita}:GetCountry():GetFaction():GetTag())"));
		Assert.Equal("GER", scripts.Ask(state, $"tostring({Ita}:GetCountry():GetFaction():GetFactionLeader())"));
		Assert.Equal(true, scripts.Ask(state, $"CCurrentGameState.GetFaction('axis') == {Ger}:GetCountry():GetFaction()"));
		// A name is matched case and all; none is the null faction.
		Assert.Equal(false, scripts.Ask(state, "CCurrentGameState.GetFaction('Axis'):IsValid()"));
		Assert.Equal(2.0, scripts.Ask(state, "(function() local n = 0 for f in CCurrentGameState.GetFactions() do n = n + 1 end return n end)()"));
		Assert.Equal("---", scripts.Ask(state, "tostring(CCurrentGameState.GetFaction('allies'):GetFactionLeader())"));
	}

	[Fact]
	public void The_null_tags_country_is_the_placeholder_at_war_with_nobody()
	{
		// Japan's script asks a misspelt faction's leader for its country and that for a relation; the original's list of
		// countries begins with the placeholder, so the answer is no war and not an error (FINDINGS-liveread.md, section 108).
		CGameState state = World();
		using CAiScripts scripts = Load();
		Assert.Equal(false, scripts.Ask(state,
			$"CCurrentGameState.GetFaction('cominterm'):GetFactionLeader():GetCountry():GetRelation({Ger}):HasWar()"));
		Assert.Equal("---", scripts.Ask(state, "tostring(CCurrentGameState.GetFaction('cominterm'):GetFactionLeader():GetCountry():GetCountryTag())"));
		Assert.Equal(false, scripts.Ask(state, $"{Ger}:GetCountry():GetRelation(CCurrentGameState.GetFaction('cominterm'):GetFactionLeader()):HasWar()"));
	}

	[Fact]
	public void An_ideology_has_its_group_and_two_groups_are_equal_where_they_are_one()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		Assert.Equal("fascism", scripts.Ask(state, $"tostring({Ger}:GetCountry():GetRulingIdeology():GetGroup():GetKey())"));
		Assert.Equal(false, scripts.Ask(state,
			$"{Ger}:GetCountry():GetRulingIdeology():GetGroup() == {Pol}:GetCountry():GetRulingIdeology():GetGroup()"));
		Assert.Equal(true, scripts.Ask(state,
			$"{Ger}:GetCountry():GetRulingIdeology():GetGroup() == {Ger}:GetCountry():GetRulingIdeology():GetGroup()"));
	}

	[Fact]
	public void The_iterators_walk_the_enemies_the_neighbours_and_a_status_for_every_country()
	{
		CGameState state = World(atWar: true);
		using CAiScripts scripts = Load();
		const string walk = "(function(f) local s = '' for t in f do s = s .. tostring(t) end return s end)";
		Assert.Equal("POL", scripts.Ask(state, $"{walk}({Ger}:GetCountry():GetCurrentAtWarWith())"));
		Assert.Equal("POL", scripts.Ask(state, $"{walk}({Ger}:GetCountry():GetNeighbours())"));
		Assert.Equal("GERPOLITASLOREB", scripts.Ask(state,
			$"(function() local s = '' for d in {Ger}:GetCountry():GetDiplomacy() do s = s .. tostring(d:GetTarget()) end return s end)()"));
	}

	[Fact]
	public void Friends_enemies_and_safe_land_answer_as_the_original_does()
	{
		CGameState state = World(atWar: true);
		state.Country(ScriptWorld.Pol)!.BaseIc = 101;
		using CAiScripts scripts = Load();
		Assert.Equal(true, scripts.Ask(state, $"{Ger}:GetCountry():IsFriend({Ita}, false)"));
		Assert.Equal(false, scripts.Ask(state, $"{Ger}:GetCountry():IsFriend({Pol}, false)"));
		Assert.Equal(true, scripts.Ask(state, $"{Ger}:GetCountry():IsEnemy({Pol})"));
		Assert.Equal(true, scripts.Ask(state, $"{Ger}:GetCountry():IsEnemy(CCountryDataBase.GetTag('REB'))"));
		Assert.Equal(false, scripts.Ask(state, $"{Ger}:GetCountry():IsEnemy({Ger})"));
		Assert.Equal(true, scripts.Ask(state, $"{Pol}:GetCountry():IsMajor()"));
		Assert.Equal(true, scripts.Ask(state, $"{Pol}:GetCountry():IsNonExileNeighbour({Ger})"));
		// The Axis's leader is at war, so its land is never safe to join.
		Assert.Equal(false, scripts.Ask(state, $"{Pol}:GetCountry():IsLandSafeToJoin(CCurrentGameState.GetFaction('axis'))"));
		state.Wars.Clear();
		Assert.Equal(true, scripts.Ask(state, $"{Pol}:GetCountry():IsLandSafeToJoin(CCurrentGameState.GetFaction('axis'))"));
		Assert.Equal(false, scripts.Ask(state, $"{Pol}:GetCountry():IsLandSafeToJoin({Pol}:GetCountry():GetFaction())"));
	}

	[Fact]
	public void A_guarantee_is_read_from_the_holders_side_and_counts_as_a_friendly_agreement()
	{
		CGameState state = World();
		state.Guarantees.Add(new CGuarantee(ScriptWorld.Ger, ScriptWorld.Pol));
		using CAiScripts scripts = Load();
		Assert.Equal(true, scripts.Ask(state, $"{Pol}:GetCountry():GetRelation({Ger}):IsGuaranteed()"));
		Assert.Equal(false, scripts.Ask(state, $"{Ger}:GetCountry():GetRelation({Pol}):IsGuaranteed()"));
		Assert.Equal(true, scripts.Ask(state, $"{Ger}:GetCountry():GetRelation({Pol}):HasFriendlyAgreement()"));
		Assert.Equal(false, scripts.Ask(state, $"{Ita}:GetCountry():GetRelation({Pol}):HasFriendlyAgreement()"));
	}

	[Fact]
	public void The_strategy_answers_its_lists_weights_and_its_personality_and_nought_for_a_country_not_listed()
	{
		CGameState state = World();
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		germany.Strategy.Befriend.Add(ScriptWorld.Pol, 120, 7);
		germany.Strategy.Antagonize.Add(ScriptWorld.Ita, 300, 7);
		germany.Strategy.Threat.Add(ScriptWorld.Pol, 75, 0);
		germany.Strategy.Protect.Add(ScriptWorld.Slo, 35, 6);
		germany.Strategy.Personality = OpenHOI3.Sim.Ai.EAiPersonality.Military;
		using CAiScripts scripts = Load();

		Assert.Equal(120.0, scripts.Ask(state, $"{Ger}:GetCountry():GetStrategy():GetFriendliness({Pol})"));
		Assert.Equal(0.0, scripts.Ask(state, $"{Ger}:GetCountry():GetStrategy():GetFriendliness({Ita})"));
		Assert.Equal(300.0, scripts.Ask(state, $"{Ger}:GetCountry():GetStrategy():GetAntagonism({Ita})"));
		Assert.Equal(75.0, scripts.Ask(state, $"{Ger}:GetCountry():GetStrategy():GetThreat({Pol})"));
		Assert.Equal(35.0, scripts.Ask(state, $"{Ger}:GetCountry():GetStrategy():GetProtectionism({Slo})"));
		Assert.Equal(true, scripts.Ask(state, $"{Ger}:GetCountry():GetStrategy():IsMilitarist()"));
		Assert.Equal(false, scripts.Ask(state, $"{Pol}:GetCountry():GetStrategy():IsMilitarist()"));
		Assert.Empty(_reports);
	}

	[Fact]
	public void A_country_may_make_a_puppet_where_it_has_one_to_make_and_nothing_is_reported()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		Assert.Equal(false, scripts.Ask(state, $"{Pol}:GetCountry():CanCreatePuppet()"));
		// Slovakia owns nothing and its capital, with its core, is Germany's: the list goes by the original's Lua name,
		// GetPossiblePuppets, which ITA.lua's HandlePuppets calls - reached at once at a later start, Ethiopia Italy's.
		Assert.Equal(true, scripts.Ask(state, $"{Ger}:GetCountry():CanCreatePuppet()"));
		Assert.Equal("SLO", scripts.Ask(state,
			$"(function() local s = '' for t in {Ger}:GetCountry():GetPossiblePuppets() do s = s .. tostring(t) end return s end)()"));
		Assert.Equal(false, scripts.Ask(state, $"{Ger}:GetCountry():IsGovernmentInExile()"));
		Assert.Empty(_reports);
		// Germany has no spies in Poland here, so the factor is Poland's counter-intelligence turned round - none - and
		// nothing about it is unbuilt.
		Assert.Equal(0.0, scripts.Ask(state, $"CAIIntel({Ger}, {Pol}):GetFactor()"));
		Assert.Empty(_reports);
	}

	[Fact]
	public void The_surrender_level_is_the_occupied_share_of_owned_cores_over_national_unity()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		// No day has counted Germany's victory points: none owned, none lost.
		Assert.Equal(0.0, scripts.Ask(state, $"{Ger}:GetCountry():GetSurrenderLevel():Get()"));
		// Slovakia holds nothing, so it is on the brink whatever the victory points.
		Assert.Equal(1.0, scripts.Ask(state, $"{Slo}:GetCountry():GetSurrenderLevel():Get()"));
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		germany.NationalUnity = CFixedPoint.FromInt(50);
		germany.OwnedCoreVictoryPoints = 10000;
		germany.HeldCoreVictoryPoints = 8000;
		// A fifth occupied against a half's unity: 200 / 500.
		Assert.Equal(0.4, (double)scripts.Ask(state, $"{Ger}:GetCountry():GetSurrenderLevel():Get()")!, 5);
	}

	[Fact]
	public void The_AIs_intel_counts_its_own_strength_and_rolls_once_a_unit_for_theirs()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		CCountry poland = state.Country(ScriptWorld.Pol)!;
		poland.Modifier.Set("counter_intelligence", new CFixedPoint(-250));
		Assert.Equal(0.25, (double)scripts.Ask(state, $"CAIIntel({Ger}, {Pol}):GetFactor()")!, 5);
		long before = state.AiRandom.Draws;
		scripts.Ask(state, $"CAIIntel({Ger}, {Pol}):CalculateTheirPercievedMilitaryStrengh()");
		// One draw for each of Poland's units, and one for the guess at what is hidden.
		Assert.Equal(poland.UnitsInOrder().Count + 1, state.AiRandom.Draws - before);
	}

	[Fact]
	public void The_resource_values_are_worked_in_single_precision()
	{
		CGameState state = World();
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		germany.BaseIc = 10;
		using CAiScripts scripts = Load();
		// Energy: (0 + 0) * 10 + 10, twice over.
		Assert.Equal(20.0, scripts.Ask(state,
			$"(function() local v = CResourceValues() v:GetResourceValues({Ger}:GetCountry(), CGoodsPool._ENERGY_) return v.vDailyExpense end)()"));
		Assert.Equal(10.0, scripts.Ask(state, $"{Ger}:GetCountry():AICalculateExpense(CGoodsPool._METAL_)"));
		Assert.Equal(0.0, scripts.Ask(state, $"{Ger}:GetCountry():AICalculateExpense(CGoodsPool._FUEL_)"));
	}

	[Fact]
	public void An_offer_is_put_to_the_recipients_AI_with_the_three_tags_and_a_failure_is_a_refusal()
	{
		CGameState state = World();
		state.Country(ScriptWorld.Pol)!.BaseIc = 40;
		state.Country(ScriptWorld.Pol)!.AlignmentX = 3000;
		state.Country(ScriptWorld.Pol)!.AlignmentY = 4000;
		using CAiScripts scripts = Load(@"
function DiploScore_Probe(ai, actor, recipient, observer)
	local score = ai:GetCountry():GetMaxIC()
	if actor == observer then score = score + 1000 end
	score = score + ai:GetCountryAlignmentDistance(actor:GetCountry(), recipient:GetCountry()):Get()
	return score + 0.5
end
function DiploScore_Broken(ai, actor, recipient, observer)
	return ai:GetNothing()
end");
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		CCountry poland = state.Country(ScriptWorld.Pol)!;
		// 40 + 5 + 0.5 is 45.5, which rounds to the even 46.
		Assert.Equal(46, scripts.DiploScore(state, "DiploScore_Probe", germany, poland, poland));
		Assert.Equal(1046, scripts.DiploScore(state, "DiploScore_Probe", germany, poland, germany));
		Assert.Empty(_reports);
		Assert.Equal(0, scripts.DiploScore(state, "DiploScore_Broken", germany, poland, poland));
		Assert.Equal(0, scripts.DiploScore(state, "DiploScore_Missing", germany, poland, poland));
		Assert.Equal(2, _reports.Count);
		Assert.All(_reports, report => Assert.StartsWith("LUA Error: ", report));
	}
}
