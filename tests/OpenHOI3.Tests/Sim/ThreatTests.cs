using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Threat, against <c>docs/mechanics/diplomacy.md</c>, section 5, on <see cref="ScriptWorld"/>'s strip of
/// ground with the triangle's three corners: every capital at one point, every province of the smallest
/// size, everyone standing at nought on the triangle. So democracy, the nearest corner, is 0.133 of the
/// way the threat distance squared from where Germany stands, and fascism 0.5.
/// </summary>
public class ThreatTests
{
	private static readonly CCountryTag Ger = ScriptWorld.Ger;
	private static readonly CCountryTag Pol = ScriptWorld.Pol;
	private static readonly CCountryTag Ita = ScriptWorld.Ita;
	private static readonly CCountryTag Slo = ScriptWorld.Slo;

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	private static CFixedPoint Felt(CGameState state, CCountryTag holder, CCountryTag feared) =>
		CGameState.Status(C(state, holder), C(state, feared)).Threat;

	private static CFixedPoint N(string text) => CFixedPoint.Parse(text);

	[Fact]
	public void Spread_to_all_weighs_by_the_triangle_and_gives_the_dead_the_most()
	{
		CGameState state = AlignmentTests.State();

		CThreat.SpreadToAll(state, 1000, CThreat.Adds, C(state, Ger));

		Assert.Equal(N("0.133"), Felt(state, Pol, Ger));   // a neighbour: all of it, by democracy's 133
		Assert.Equal(N("0.5"), Felt(state, Ita, Ger));     // fascism's 500, though of Germany's faction
		Assert.Equal(N("0.133"), Felt(state, Slo, Ger));   // owns nothing: 1001 of it
		Assert.Equal(CFixedPoint.Zero, Felt(state, Ger, Ger));
	}

	[Fact]
	public void A_feared_country_in_no_faction_spreads_half_and_nobody_feels_under_a_twentieth()
	{
		CGameState state = AlignmentTests.State();
		C(state, Ger).Modifier.Add(CThreat.ResistanceModifier, CFixedPoint.One);

		CThreat.SpreadToAll(state, 1000, CThreat.Adds, C(state, Pol));

		// Half of 1000 is 500 and its twentieth 25; Germany resists all of it and still feels 25.
		Assert.Equal(N("0.025"), Felt(state, Ger, Pol));
	}

	[Fact]
	public void Threat_taken_away_is_floored_at_nought()
	{
		CGameState state = AlignmentTests.State();

		CThreat.SpreadToAll(state, 1000, CThreat.Takes, C(state, Ger));

		Assert.Equal(CFixedPoint.Zero, Felt(state, Pol, Ger));
	}

	[Fact]
	public void Spread_over_the_map_falls_130_a_step_and_charges_a_country_once_at_its_first_province()
	{
		CGameState state = AlignmentTests.State();

		CThreat.SpreadOverMap(state, C(state, Ger), 1000, CThreat.Adds, state.Province(1));

		// Berlin, then the border, Posen, and Poland's province three steps out: 1000 - 390 = 610,
		// by democracy's 133. Italy's province is reached by no way.
		Assert.Equal(N("0.081"), Felt(state, Pol, Ger));
		Assert.Equal(CFixedPoint.Zero, Felt(state, Ita, Ger));
	}

	[Fact]
	public void The_most_threatening_country_is_the_first_one_may_fear_even_at_nought_and_then_the_most()
	{
		CGameState state = AlignmentTests.State();
		CGameState.Status(C(state, Pol), C(state, Ger)).Threat = CFixedPoint.FromInt(5);
		CGameState.Status(C(state, Pol), C(state, Ita)).Threat = CFixedPoint.FromInt(7);

		CThreat.UpdateMostThreatening(state);

		Assert.Equal(Ita, C(state, Pol).MostThreatening);
		Assert.Equal(Pol, C(state, Ger).MostThreatening);   // Italy is of its faction, Slovakia owns nothing
		Assert.Null(C(state, ScriptWorld.Reb).MostThreatening);

		state.Alliances.Add(new CAlliance(Pol, Ita));
		CThreat.UpdateMostThreatening(state);
		Assert.Equal(Ger, C(state, Pol).MostThreatening);
	}

	[Fact]
	public void The_threat_effect_gives_one_country_unweighed_or_spreads_to_all()
	{
		CGameState state = AlignmentTests.State();

		CEffects.Run(state, ScriptWorld.Effects("threat = { who = POL value = 3 }"), CEventScope.Start(Ger, 0));
		Assert.Equal(CFixedPoint.FromInt(3), Felt(state, Pol, Ger));

		CEffects.Run(state, ScriptWorld.Effects("threat = { who = all value = 1 }"), CEventScope.Start(Ger, 0));
		Assert.Equal(N("3.133"), Felt(state, Pol, Ger));
		Assert.Equal(N("0.5"), Felt(state, Ita, Ger));
	}
}
