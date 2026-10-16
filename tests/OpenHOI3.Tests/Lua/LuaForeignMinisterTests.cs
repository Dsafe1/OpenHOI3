using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// What the foreign minister's decision script is handed and what its answer becomes, against
/// <c>docs/mechanics/foreignminister.md</c>: the minister, the decision and the scope; an answer to the nearest, ties to
/// even, and one that is no number a failure; the prepared wars from Lua; a country's flags. Germany owns Berlin (1),
/// Poland Posen (2).
/// </summary>
public sealed class LuaForeignMinisterTests : IDisposable
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private readonly string _root = Path.Combine(Path.GetTempPath(), "openhoi3-luaforeign-" + Guid.NewGuid().ToString("N"));
	private readonly List<string> _reports = new();

	public LuaForeignMinisterTests()
	{
		Directory.CreateDirectory(Path.Combine(_root, "script"));
	}

	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	private CAiScripts Load(string script)
	{
		File.WriteAllText(Path.Combine(_root, "script", "autoexec.lua"), script);
		return new CAiScripts(CDirectorySettings.At(_root), _reports.Add);
	}

	private static CGameState World()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = 2 };
		return new CGameState(new CDate(1939, 8, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(2, "Posen", isSea: false) { Owner = Pol, Controller = Pol },
			},
			new[] { germany, poland });
	}

	private static CDecision Decision(string name, string? allow = null)
	{
		var decision = new CDecision(name, EDecisionGroup.Diplomatic, "decisions/test.txt", 1);
		if (allow is not null)
		{
			decision.Allow.Children.AddRange(Sim.ScriptWorld.Condition(allow).Children);
		}
		return decision;
	}

	[Fact]
	public void The_answer_is_rounded_to_the_nearest_ties_to_even()
	{
		CGameState state = World();
		using CAiScripts scripts = Load(
			"local answers = { half = 0.5, oneandhalf = 1.5, most = 30.7 }\n" +
			"function ForeignMinister_EvaluateDecision(minister, decision, scope)\n" +
			"  return answers[tostring(decision:GetKey())]\n" +
			"end\n");
		CCountry germany = state.Country(Ger)!;
		CEventScope scope = CEventScope.Start(Ger);

		int?[] answers = new[] { "half", "oneandhalf", "most" }
			.Select(name => scripts.EvaluateDecision(state, germany, Decision(name), scope, out _)).ToArray();

		Assert.Equal(new int?[] { 0, 2, 31 }, answers);
	}

	[Fact]
	public void An_answer_that_is_no_number_or_an_error_is_a_failure_reported()
	{
		CGameState state = World();
		using CAiScripts scripts = Load(
			"function ForeignMinister_EvaluateDecision(minister, decision, scope)\n" +
			"  if tostring(decision:GetKey()) == 'text' then return 'yes' end\n" +
			"  error('broken')\n" +
			"end\n");
		CCountry germany = state.Country(Ger)!;

		Assert.Null(scripts.EvaluateDecision(state, germany, Decision("text"), CEventScope.Start(Ger), out string? text));
		Assert.Null(scripts.EvaluateDecision(state, germany, Decision("error"), CEventScope.Start(Ger), out string? error));
		Assert.Contains("not a number", text);
		Assert.Contains("broken", error);
		// Reported by the tick the failure ends, not here.
		Assert.Equal(0, scripts.Failures);
	}

	[Fact]
	public void The_script_is_handed_the_minister_the_decision_and_the_scope_its_conditions_are_asked_in()
	{
		CGameState state = World();
		using CAiScripts scripts = Load(
			"function ForeignMinister_EvaluateDecision(minister, decision, scope)\n" +
			"  local score = 0\n" +
			"  if tostring(minister:GetCountryTag():GetTag()) == 'GER' then score = score + 1 end\n" +
			"  if minister:GetOwnerAI():GetCountry():GetCountryTag() == minister:GetCountry():GetCountryTag() then score = score + 2 end\n" +
			"  if decision:IsPotential(scope) then score = score + 4 end\n" +
			"  if decision:IsAllowed(scope) then score = score + 8 end\n" +
			"  return score\n" +
			"end\n");
		CCountry germany = state.Country(Ger)!;

		Assert.Equal(15, scripts.EvaluateDecision(state, germany, Decision("open"), CEventScope.Start(Ger), out _));
		Assert.Equal(7, scripts.EvaluateDecision(state, germany, Decision("polish", allow: "tag = POL"), CEventScope.Start(Ger), out _));
	}

	[Fact]
	public void A_war_prepared_from_lua_keeps_its_decision_and_is_seen_by_the_strategy()
	{
		CGameState state = World();
		using CAiScripts scripts = Load(
			"function ForeignMinister_EvaluateDecision(minister, decision, scope)\n" +
			"  local strategy = minister:GetCountry():GetStrategy()\n" +
			"  local poland = CCountryDataBase.GetTag('POL')\n" +
			"  strategy:PrepareWarDecision(poland, 100, decision, false)\n" +
			"  if strategy:IsPreparingWar() and strategy:IsPreparingWarWith(poland) then return 0 end\n" +
			"  return 1\n" +
			"end\n");
		CCountry germany = state.Country(Ger)!;
		CDecision danzig = Decision("danzig_or_war");

		Assert.Equal(0, scripts.EvaluateDecision(state, germany, danzig, CEventScope.Start(Ger), out _));

		CWarTarget entry = Assert.Single(germany.WarTargets);
		Assert.Equal(Pol, entry.Target);
		Assert.Same(danzig, entry.Decision);
		Assert.Equal(BitConverter.SingleToInt32Bits(100f), entry.ValueBits);
	}

	[Fact]
	public void An_action_is_made_by_its_class_or_create_and_says_whether_its_kind_may_be_pressed()
	{
		CGameState state = World();
		using CAiScripts scripts = Load("");

		object? answer = scripts.Ask(state,
			"(function() local ger, pol = CCountryDataBase.GetTag('GER'), CCountryDataBase.GetTag('POL') " +
			"local nap = CNapAction(ger, pol) local own = CNapAction.Create(ger, ger) " +
			"local call = CCallAllyAction(ger, pol, ger) local leave = CAllianceAction(ger, pol) leave:SetValue(false) " +
			"return (nap:IsSelectable() and 1 or 0) + (own:IsSelectable() and 2 or 0) + (nap:GetValue() and 4 or 0) " +
			"+ (leave:GetValue() and 8 or 0) + (call:GetVersus() == ger and 16 or 0) + (leave:IsSelectable() and 32 or 0) end)()");

		// A NAP between the two may be offered; not with itself; leaving an alliance there is none of may not.
		Assert.Equal(1.0 + 4 + 16, answer);
	}

	[Fact]
	public void The_tick_proposes_through_the_minister_and_posts_through_the_ai()
	{
		CGameState state = World();
		using CAiScripts scripts = Load(
			"function ForeignMinister_Tick(minister)\n" +
			"  local ger, pol = minister:GetCountryTag(), CCountryDataBase.GetTag('POL')\n" +
			"  minister:Propose(CNapAction(ger, pol), 80)\n" +
			"  minister:GetOwnerAI():PostAction(CGuaranteeAction(ger, pol))\n" +
			"end\n");
		CCountry germany = state.Country(Ger)!;

		scripts.RunForeign(state, germany);

		Assert.Equal(OpenHOI3.Sim.Agreements.EOfferKind.NonAggressionPact, germany.ForeignMinister.Proposal!.Kind);
		Assert.Equal(80f, germany.ForeignMinister.ProposalScore);
		var posted = Assert.IsType<OpenHOI3.Sim.Commands.COfferCommand>(Assert.Single(state.Outbox));
		Assert.Equal(OpenHOI3.Sim.Agreements.EOfferKind.Guarantee, posted.Offer.Kind);
	}

	[Fact]
	public void A_decision_that_fails_ends_the_tick_reported_once()
	{
		CGameState state = World();
		state.Decisions = new[] { Decision("first"), Decision("second") };
		using CAiScripts scripts = Load(
			"after = false\n" +
			"function ForeignMinister_EvaluateDecision(minister, decision, scope) error('broken') end\n" +
			"function ForeignMinister_Tick(minister)\n" +
			"  minister:ExecuteDiploDecisions()\n" +
			"  after = true\n" +
			"end\n");
		state.AiScripts = scripts;

		scripts.RunForeign(state, state.Country(Ger)!);

		Assert.Equal(1, scripts.Failures);
		Assert.Equal(false, scripts.Ask(state, "after"));
		Assert.Contains(_reports, line => line.Contains("broken"));
	}

	[Fact]
	public void The_day_of_the_month_counts_from_nought_as_the_month_does()
	{
		CGameState state = World();
		using CAiScripts scripts = Load("");

		// 1 August 1939.
		Assert.Equal(700.0, scripts.Ask(state,
			"CCurrentGameState.GetCurrentDate():GetMonthOfYear() * 100 + CCurrentGameState.GetCurrentDate():GetDayOfMonth()"));
	}

	[Fact]
	public void The_ceiling_for_war_is_the_diplomacy_screens()
	{
		CGameState state = World();
		using CAiScripts scripts = Load("");

		object? answer = scripts.Ask(state,
			"CCountryDataBase.GetTag('GER'):GetCountry():GetMaxNeutralityForWarWith(CCountryDataBase.GetTag('POL')):Get()");

		CFixedPoint ceiling = OpenHOI3.Sim.Diplomacy.CNeutrality.WarCeiling(state, state.Country(Ger)!, state.Country(Pol)!);
		Assert.Equal((double)(float)(ceiling.Raw / 1000.0), answer);
	}

	[Fact]
	public void A_countrys_flags_say_whether_one_is_set_without_case()
	{
		CGameState state = World();
		using CAiScripts scripts = Load("");
		state.Country(Ger)!.Flags.Set("jap_seizes_coast");
		state.Country(Ger)!.Flags.Set("cleared");
		state.Country(Ger)!.Flags.Clear("cleared");

		object? answer = scripts.Ask(state,
			"(function() local f = CCountryDataBase.GetTag('GER'):GetCountry():GetFlags() " +
			"return (f:IsFlagSet('JAP_SEIZES_COAST') and 1 or 0) + (f:IsFlagSet('cleared') and 2 or 0) " +
			"+ (f:IsFlagSet('never') and 4 or 0) end)()");

		Assert.Equal(1.0, answer);
	}
}
