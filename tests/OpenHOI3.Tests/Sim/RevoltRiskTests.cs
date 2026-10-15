using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Occupation;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Suppression, revolt risk's day, the figure read and the conditions and effect on it, against
/// <c>docs/mechanics/revolt.md</c>, on <see cref="ScriptWorld"/> at war, Germany holding Warsaw under a policy that lets
/// revolt risk climb to 1.5.
/// </summary>
public class RevoltRiskTests
{
	private static readonly CSubUnitType Militia = Type("militia_brigade", "0.33");

	private static CSubUnitType Type(string key, string suppression)
	{
		var type = new CSubUnitType(key, EUnitBranch.Land);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(CSuppression.Kind, CFixedPoint.Parse(suppression));
		return type;
	}

	private static CGameState World(bool atWar = true, params CModifier[] statics)
	{
		CGameState state = ScriptWorld.State(atWar, statics: statics);
		List<COccupationPolicy> policies = COccupationPolicies.WithNull();
		var collaboration = new CModifier("collaboration_government");
		collaboration.Set(CRevoltRisk.PartisanSupport, CFixedPoint.Parse("1.5"));
		policies.Add(new COccupationPolicy(collaboration, 1));
		state.OccupationPolicies = policies;
		CGameState.Status(state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Pol)!).OccupationPolicy = 1;
		state.Province(4).Controller = ScriptWorld.Ger;
		CProvinceModifiers.Rebuild(state);
		return state;
	}

	private static CUnit Division(CGameState state, CCountryTag owner, int province, string strength = "30")
	{
		CCountry country = state.Country(owner)!;
		var unit = new CUnit(EUnitKind.Division, owner) { Id = ++state.LastUnitId, Location = province };
		for (int i = 0; i < 3; i++)
		{
			var subUnit = new CSubUnit(Militia) { Id = ++state.LastUnitId, Unit = unit, Strength = CFixedPoint.Parse(strength) };
			subUnit.Stats.CopyFrom(Militia.Stats);
			unit.SubUnits.Add(subUnit);
		}
		country.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	[Fact]
	public void A_province_counts_its_friends_units_by_their_strength_a_tenth_of_the_sum()
	{
		CGameState state = World();
		Division(state, ScriptWorld.Ger, 4);
		Division(state, ScriptWorld.Ger, 4, strength: "15");
		Division(state, ScriptWorld.Pol, 4);

		// 990 and 495 from Germany's, nothing from Poland's, and a tenth of it.
		Assert.Equal(148, CSuppression.In(state, state.Province(4)));
	}

	[Fact]
	public void The_figure_reaches_two_edges_over_land_of_the_same_holder_and_counts_the_province_twice()
	{
		CGameState state = World();
		Division(state, ScriptWorld.Ger, 4);
		Assert.Equal(198, CSuppression.Near(state, state.Province(4)));

		// Berlin is two edges from Posen and three from Warsaw.
		Division(state, ScriptWorld.Ger, 2);
		Division(state, ScriptWorld.Ger, 1);
		Assert.Equal(297, CSuppression.Near(state, state.Province(4)));
		Assert.Equal(new[] { 3, 2, 4, 6 }, CNearProvinces.Of(state, state.Province(4)));
	}

	[Fact]
	public void At_war_the_day_climbs_a_tenth_less_the_suppression_to_the_support_and_at_peace_it_stands()
	{
		CGameState state = World();
		CProvince warsaw = state.Province(4);
		warsaw.Suppression = new CFixedPoint(198);
		CRevoltRisk.StepDaily(state, warsaw);
		Assert.Equal(new CFixedPoint(81), warsaw.RevoltRisk);

		warsaw.Suppression = CFixedPoint.Zero;
		warsaw.RevoltRisk = CFixedPoint.Parse("1.45");
		CRevoltRisk.StepDaily(state, warsaw);
		Assert.Equal(CFixedPoint.Parse("1.5"), warsaw.RevoltRisk);

		// Above the support it comes down a tenth and as much as the suppression takes, at most 0.2.
		warsaw.RevoltRisk = CFixedPoint.FromInt(2);
		warsaw.Suppression = CFixedPoint.FromInt(5);
		CRevoltRisk.StepDaily(state, warsaw);
		Assert.Equal(CFixedPoint.Parse("1.5"), warsaw.RevoltRisk);
		warsaw.RevoltRisk = CFixedPoint.Parse("1.6");
		CRevoltRisk.StepDaily(state, warsaw);
		Assert.Equal(CFixedPoint.Parse("1.3"), warsaw.RevoltRisk);

		CGameState peace = World(atWar: false);
		peace.Province(4).RevoltRisk = CFixedPoint.Parse("0.7");
		CRevoltRisk.StepDaily(peace, peace.Province(4));
		Assert.Equal(CFixedPoint.Parse("0.7"), peace.Province(4).RevoltRisk);
	}

	[Fact]
	public void The_figure_read_is_the_risk_held_against_an_enemy_and_the_modifiers_less_the_suppression()
	{
		CGameState state = World();
		CProvince warsaw = state.Province(4);
		warsaw.RevoltRisk = CFixedPoint.FromInt(1);
		warsaw.Suppression = CFixedPoint.Parse("1.5");
		state.Country(ScriptWorld.Ger)!.Modifier.Set(CRevoltRisk.GlobalRevoltRisk, CFixedPoint.Parse("0.2"));
		warsaw.Modifier.Set(CRevoltRisk.LocalRevoltRisk, CFixedPoint.Parse("0.1"));

		Assert.Equal(-200, CRevoltRisk.Effective(state, warsaw));
		Assert.Equal(0, CRevoltRisk.Shown(state, warsaw));
		warsaw.Modifier.Set(CRevoltRisk.MinimumRevoltRisk, CFixedPoint.Parse("0.3"));
		Assert.Equal(300, CRevoltRisk.Shown(state, warsaw));

		// The rebels holding it read the owner's global figure.
		warsaw.Controller = ScriptWorld.Reb;
		state.Country(ScriptWorld.Pol)!.Modifier.Set(CRevoltRisk.GlobalRevoltRisk, CFixedPoint.Parse("1"));
		Assert.Equal(600, CRevoltRisk.Effective(state, warsaw));
	}

	[Fact]
	public void The_province_is_given_its_revolt_risk_static_by_the_figure_shown()
	{
		var revoltRisk = new CModifier(CProvinceModifiers.RevoltRisk);
		revoltRisk.Set("local_ic", CFixedPoint.Parse("-0.02"));
		CGameState state = World(true, revoltRisk);
		CProvince warsaw = state.Province(4);
		warsaw.RevoltRisk = CFixedPoint.Parse("1.5");
		CProvinceModifiers.RebuildOne(state, warsaw);
		Assert.Equal(CFixedPoint.Parse("-0.03"), warsaw.Modifier["local_ic"]);

		warsaw.Suppression = CFixedPoint.FromInt(2);
		CProvinceModifiers.RebuildOne(state, warsaw);
		Assert.Equal(CFixedPoint.Zero, warsaw.Modifier["local_ic"]);
	}

	[Fact]
	public void The_conditions_are_strict_on_a_province_and_count_the_rebels_land_on_a_country()
	{
		CGameState state = World();
		CProvince warsaw = state.Province(4);
		warsaw.RevoltRisk = CFixedPoint.Parse("1.5");
		warsaw.Modifier.Set(CRevoltRisk.MinimumRevoltRisk, CFixedPoint.FromInt(3));
		bool Holds(string text, CEventScope scope) => CConditions.All(state, ScriptWorld.Condition(text), scope, null);
		CEventScope here = CEventScope.Start(ScriptWorld.Pol, 4);

		Assert.True(Holds("revolt_risk = 2", here));
		Assert.False(Holds("revolt_risk = 3", here));
		Assert.True(Holds("pure_revolt_risk = 1", here));
		Assert.False(Holds("pure_revolt_risk = 2", here));
		Assert.False(Holds("revolt_risk = 0", CEventScope.Start(ScriptWorld.Pol)));

		state.Province(2).Controller = ScriptWorld.Reb;
		CEventScope germany = CEventScope.Start(ScriptWorld.Ger);
		Assert.True(Holds("num_of_revolts = 1", germany));
		Assert.False(Holds("num_of_revolts = 2", germany));
		Assert.True(Holds("revolt_percentage = 0.33", germany));
		Assert.False(Holds("revolt_percentage = 0.34", germany));
		Assert.False(Holds("revolt_percentage = 0", CEventScope.Start(ScriptWorld.Slo)));
	}

	[Fact]
	public void The_effect_adds_and_floors_and_a_new_game_gives_each_province_its_support()
	{
		CGameState state = World();
		CProvince warsaw = state.Province(4);
		CEffects.Run(state, ScriptWorld.Effects("revolt_risk = 2"), CEventScope.Start(ScriptWorld.Pol, 4));
		Assert.Equal(CFixedPoint.FromInt(2), warsaw.RevoltRisk);
		CEffects.Run(state, ScriptWorld.Effects("revolt_risk = -5"), CEventScope.Start(ScriptWorld.Pol, 4));
		Assert.Equal(CFixedPoint.Zero, warsaw.RevoltRisk);

		state.Province(3).RevoltRisk = CFixedPoint.Parse("0.4");
		CRevoltRisk.SetAtNewGame(state);
		Assert.Equal(CFixedPoint.Parse("1.5"), warsaw.RevoltRisk);
		Assert.Equal(CFixedPoint.Parse("0.4"), state.Province(3).RevoltRisk);
	}
}
