using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Occupation;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Occupation policies and nationalism, against <c>docs/mechanics/occupation.md</c>, on <see cref="ScriptWorld"/>
/// with three policies: a collaboration government first, a military government, and a civil war Germany may set on
/// Poland.
/// </summary>
public class OccupationTests
{
	private static CGameState World(bool atWar = true, params CModifier[] statics)
	{
		CGameState state = ScriptWorld.State(atWar, statics: statics);
		List<COccupationPolicy> policies = COccupationPolicies.WithNull();
		COccupationPolicy Policy(string name, string ic)
		{
			var modifier = new CModifier(name);
			modifier.Set("local_ic", CFixedPoint.Parse(ic));
			var policy = new COccupationPolicy(modifier, policies.Count);
			policies.Add(policy);
			return policy;
		}
		Policy("collaboration_government", "-0.95");
		Policy("military_government", "-0.8");
		Policy("civil_war", "0").Allow = ScriptWorld.Condition("tag = GER FROM = { tag = POL }");
		state.OccupationPolicies = policies;
		state.Defines.Set(CNationalism.Section, CNationalism.Years, CFixedPoint.FromInt(20));
		return state;
	}

	private static int PolicyOf(CGameState state, CCountryTag holder, CCountryTag other)
	{
		return CGameState.Status(state.Country(holder)!, state.Country(other)!).OccupationPolicy;
	}

	[Fact]
	public void Every_record_starts_at_no_occupation()
	{
		CGameState state = World();
		Assert.Equal(0, PolicyOf(state, ScriptWorld.Ger, ScriptWorld.Pol));
		Assert.Equal(COccupationPolicies.NoOccupation, COccupationPolicies.Of(state, state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Pol)!).Name);
	}

	[Fact]
	public void A_capture_sets_the_files_first_policy_once_and_a_set_one_is_kept()
	{
		CGameState state = World();
		state.OccupationPolicies.Last().Allow = ScriptWorld.Condition("always = no");
		CProvince warsaw = state.Province(4);

		CProvinceHands.SetController(state, warsaw, ScriptWorld.Ger);
		Assert.Equal(1, PolicyOf(state, ScriptWorld.Ger, ScriptWorld.Pol));

		CGameState.Status(state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Pol)!).OccupationPolicy = 2;
		CProvinceHands.SetController(state, warsaw, ScriptWorld.Pol);
		CProvinceHands.SetController(state, warsaw, ScriptWorld.Ger);
		Assert.Equal(2, PolicyOf(state, ScriptWorld.Ger, ScriptWorld.Pol));
	}

	[Fact]
	public void A_capture_sets_civil_war_where_its_allow_holds_with_the_old_holder_as_from()
	{
		CGameState state = World();
		CProvinceHands.SetController(state, state.Province(4), ScriptWorld.Ger);
		Assert.Equal(3, PolicyOf(state, ScriptWorld.Ger, ScriptWorld.Pol));

		// Poland taking Posen back sets Poland's towards Germany, whose civil war is not allowed.
		CProvinceHands.SetController(state, state.Province(3), ScriptWorld.Pol);
		Assert.Equal(1, PolicyOf(state, ScriptWorld.Pol, ScriptWorld.Ger));
	}

	[Fact]
	public void A_capture_keys_by_the_old_holder_and_the_province_reads_its_holder_towards_its_owner()
	{
		CGameState state = World();
		CProvince warsaw = state.Province(4);
		warsaw.Controller = ScriptWorld.Ger;

		// Italy takes Polish land from Germany: Italy's policy towards Germany is set, not towards Poland.
		CProvinceHands.SetController(state, warsaw, ScriptWorld.Ita);
		Assert.Equal(1, PolicyOf(state, ScriptWorld.Ita, ScriptWorld.Ger));
		Assert.Equal(0, PolicyOf(state, ScriptWorld.Ita, ScriptWorld.Pol));

		CProvinceModifiers.Rebuild(state);
		Assert.Equal(CFixedPoint.Zero, warsaw.Modifier["local_ic"]);
		Assert.Contains(warsaw.ModifierSources, source => source.Modifier.Name == COccupationPolicies.NoOccupation);
	}

	[Fact]
	public void A_province_gets_its_holders_policy_towards_its_owner_with_no_war_asked()
	{
		CGameState state = World(atWar: false);
		CProvince warsaw = state.Province(4);
		CGameState.Status(state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Pol)!).OccupationPolicy = 2;

		CProvinceModifiers.Rebuild(state);
		Assert.Equal(CFixedPoint.Zero, warsaw.Modifier["local_ic"]);

		warsaw.Controller = ScriptWorld.Ger;
		CProvinceModifiers.Rebuild(state);
		Assert.Equal(CFixedPoint.Parse("-0.8"), warsaw.Modifier["local_ic"]);

		// Nothing where the rebels hold it.
		warsaw.Controller = ScriptWorld.Reb;
		CProvinceModifiers.Rebuild(state);
		Assert.Equal(CFixedPoint.Zero, warsaw.Modifier["local_ic"]);
	}

	[Fact]
	public void The_command_checks_nothing_and_an_unknown_name_sets_no_occupation()
	{
		CGameState state = World();
		CProvince warsaw = state.Province(4);
		warsaw.Controller = ScriptWorld.Ger;
		var command = new CChangeOccupationPolicyCommand(ScriptWorld.Ger, ScriptWorld.Pol, "civil_war");
		state.OccupationPolicies.Last().Allow = ScriptWorld.Condition("always = no");
		Assert.True(command.MayExecute(state));

		command.Execute(state);
		Assert.Equal(3, PolicyOf(state, ScriptWorld.Ger, ScriptWorld.Pol));

		new CChangeOccupationPolicyCommand(ScriptWorld.Ger, ScriptWorld.Pol, "military_government").Execute(state);
		Assert.Equal(CFixedPoint.Parse("-0.8"), warsaw.Modifier["local_ic"]);

		new CChangeOccupationPolicyCommand(ScriptWorld.Ger, ScriptWorld.Pol, "no_such_policy").Execute(state);
		Assert.Equal(0, PolicyOf(state, ScriptWorld.Ger, ScriptWorld.Pol));
	}

	[Fact]
	public void The_history_sets_one_with_no_allow_asked_and_annexation_forgets_it()
	{
		CGameState state = World();
		state.OccupationPolicies.Last().Allow = ScriptWorld.Condition("always = no");
		state.Country(ScriptWorld.Ger)!.HistoryOccupationPolicies.Add(("civil_war", ScriptWorld.Pol));
		state.Country(ScriptWorld.Ger)!.HistoryOccupationPolicies.Add(("military_government", new CCountryTag("XXX")));
		COccupationPolicies.SetFromHistory(state);
		Assert.Equal(3, PolicyOf(state, ScriptWorld.Ger, ScriptWorld.Pol));

		CAnnexation.Annex(state, state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Pol)!);
		Assert.Equal(0, PolicyOf(state, ScriptWorld.Ger, ScriptWorld.Pol));
	}

	[Fact]
	public void A_change_of_owner_sets_nationalism_even_to_the_same_owner_and_the_month_takes_83_off()
	{
		CGameState state = World();
		CProvince warsaw = state.Province(4);
		CProvince wilds = state.Province(6);

		CProvinceHands.ChangeOwner(state, warsaw, ScriptWorld.Ger);
		Assert.Equal(CFixedPoint.FromInt(20), warsaw.Nationalism);
		// Nobody owned the wilds: no nationalism.
		CProvinceHands.ChangeOwner(state, wilds, ScriptWorld.Ger);
		Assert.Equal(CFixedPoint.Zero, wilds.Nationalism);

		CNationalism.DecayMonthly(state);
		Assert.Equal(CFixedPoint.Parse("19.917"), warsaw.Nationalism);
		CProvinceHands.ChangeOwner(state, warsaw, ScriptWorld.Ger);
		Assert.Equal(CFixedPoint.FromInt(20), warsaw.Nationalism);

		warsaw.Nationalism = CFixedPoint.Parse("0.05");
		CNationalism.DecayMonthly(state);
		Assert.Equal(CFixedPoint.Zero, warsaw.Nationalism);
	}

	[Fact]
	public void Nationalism_adds_its_static_by_the_years_where_the_owner_holds_it_without_a_core()
	{
		var nationalism = new CModifier(CProvinceModifiers.Nationalism);
		nationalism.Set("local_revolt_risk", CFixedPoint.Parse("0.3"));
		CGameState state = World(true, nationalism);
		CProvince border = state.Province(2);
		CProvince posen = state.Province(3);
		border.Nationalism = CFixedPoint.FromInt(20);
		posen.Nationalism = CFixedPoint.FromInt(20);

		CProvinceModifiers.Rebuild(state);
		Assert.Equal(CFixedPoint.FromInt(6), border.Modifier["local_revolt_risk"]);
		Assert.Equal(CFixedPoint.Zero, posen.Modifier["local_revolt_risk"]);    // Germany's core

		border.Controller = ScriptWorld.Pol;
		CProvinceModifiers.Rebuild(state);
		Assert.Equal(CFixedPoint.Zero, border.Modifier["local_revolt_risk"]);
	}

	[Fact]
	public void The_condition_wants_more_whole_years_than_its_figure()
	{
		CGameState state = World();
		state.Province(4).Nationalism = CFixedPoint.Parse("19.917");
		bool Holds(string text) => CConditions.All(state, ScriptWorld.Condition(text), CEventScope.Start(ScriptWorld.Pol, 4), null);

		Assert.True(Holds("nationalism = 19"));
		Assert.True(Holds("nationalism = 19.9"));
		Assert.False(Holds("nationalism = 20"));
		Assert.False(CConditions.All(state, ScriptWorld.Condition("nationalism = 1"), CEventScope.Start(ScriptWorld.Pol), null));
	}

	[Fact]
	public void Days_out_of_supply_want_strictly_more_days_of_the_scopes_province()
	{
		CGameState state = World();
		state.Province(4).OutOfSupplyDays = 3;
		bool Holds(string text) => CConditions.All(state, ScriptWorld.Condition(text), CEventScope.Start(ScriptWorld.Pol, 4), null);

		Assert.True(Holds("out_of_supply_days = 2"));
		Assert.False(Holds("out_of_supply_days = 3"));
		// A country's scope has no province, and answers false (supply.md, section 18).
		Assert.False(CConditions.All(state, ScriptWorld.Condition("out_of_supply_days = 0"), CEventScope.Start(ScriptWorld.Pol), null));
	}

	[Fact]
	public void The_coast_land_or_sea_and_nothing_held_alike_beside_it()
	{
		var statics = new List<CModifier>();
		foreach (string name in new[]
		{
			CProvinceModifiers.Coastal, CProvinceModifiers.NonCoastal, CProvinceModifiers.CoastalSea, CProvinceModifiers.LandProvince,
			CProvinceModifiers.SeaZone, CProvinceModifiers.NoAdjacentControlled,
		})
		{
			var modifier = new CModifier(name);
			modifier.Set(name + "_mark", CFixedPoint.One);
			statics.Add(modifier);
		}
		CGameState state = World(true, statics.ToArray());
		CProvinceModifiers.Rebuild(state);
		bool Has(int province, string name) => state.Province(province).Modifier[name + "_mark"] == CFixedPoint.One;

		Assert.True(Has(1, CProvinceModifiers.Coastal));
		Assert.True(Has(2, CProvinceModifiers.NonCoastal));
		Assert.True(Has(5, CProvinceModifiers.CoastalSea));
		Assert.True(Has(1, CProvinceModifiers.LandProvince));
		Assert.True(Has(5, CProvinceModifiers.SeaZone));
		Assert.False(Has(5, CProvinceModifiers.LandProvince));
		// Rome stands alone; Warsaw has nothing of Poland's beside it; Berlin has the border.
		Assert.True(Has(7, CProvinceModifiers.NoAdjacentControlled));
		Assert.True(Has(4, CProvinceModifiers.NoAdjacentControlled));
		Assert.False(Has(1, CProvinceModifiers.NoAdjacentControlled));
		Assert.False(Has(6, CProvinceModifiers.NoAdjacentControlled));     // nobody owns the wilds
	}
}
