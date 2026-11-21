using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What the AI does with its undergrounds, against <c>docs/mechanics/undergrounds.md</c>, section 10, on
/// <see cref="RebelWorld"/>: Poland's underground in Warsaw, which Germany holds, on the one front of Germany's area.
/// </summary>
public class UndergroundAiTests
{
	private static CProvince Place(CGameState state, string strength, int action)
	{
		CProvince warsaw = state.Province(4);
		warsaw.SetBuildingLevel(RebelWorld.Underground, CFixedPoint.One);
		warsaw.SetBuildingCurrentLevel(RebelWorld.Underground, CFixedPoint.Parse(strength));
		warsaw.UndergroundOwner = RebelWorld.Pol;
		warsaw.UndergroundAction = action;
		return warsaw;
	}

	private static CUnit Garrison(CGameState state, CCountryTag owner, int province)
	{
		state.SubUnitTypes["infantry_brigade"].Stats.Values.Set("soft_attack", CFixedPoint.FromInt(5));
		var unit = new CUnit(EUnitKind.Division, owner) { Id = ++state.LastUnitId, Location = province };
		var brigade = new CSubUnit(state.SubUnitTypes["infantry_brigade"]) { Id = ++state.LastUnitId, Unit = unit };
		CRebelArmies.SetFull(state, state.Country(owner)!, brigade);
		unit.SubUnits.Add(brigade);
		state.Country(owner)!.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	[Theory]
	// No hold on the fronts: spread while under 4% of the area, which is eight of two hundred.
	[InlineData(7, 200, 0, 0, CUndergrounds.Spread)]
	[InlineData(8, 200, 0, 0, CUndergrounds.BuildUp)]
	[InlineData(8, 200, 7, 0, CUndergrounds.BuildUp)]
	[InlineData(8, 200, 8, 0, CUndergrounds.RaisePartisans)]
	// An area counts two hundred provinces at most.
	[InlineData(8, 500, 0, 0, CUndergrounds.BuildUp)]
	// A full hold: spread under 2%, and raise partisans with none ready.
	[InlineData(3, 200, 0, 1000, CUndergrounds.Spread)]
	[InlineData(4, 200, 0, 1000, CUndergrounds.RaisePartisans)]
	// Half: 3% of the area, and the ready ones wanted half of them, truncated.
	[InlineData(6, 200, 0, 500, CUndergrounds.BuildUp)]
	[InlineData(6, 200, 3, 500, CUndergrounds.RaisePartisans)]
	[InlineData(1, 4, 0, 0, CUndergrounds.BuildUp)]
	[InlineData(1, 4, 1, 0, CUndergrounds.RaisePartisans)]
	public void The_action_is_the_share_of_the_area_then_the_ready_ones_by_the_hold(int count, int area, int ready, int score, int action)
	{
		Assert.Equal(action, CUndergroundAi.Action(count, area, ready, score));
	}

	[Fact]
	public void A_country_nobody_plays_sets_an_action_only_where_it_changes()
	{
		CGameState state = RebelWorld.State();
		CProvince warsaw = Place(state, "0.6", CUndergrounds.BuildUp);
		CUndergroundAi.RunHourly(state);
		var command = Assert.IsType<CChangeUndergroundMissionCommand>(Assert.Single(state.Outbox));
		Assert.Equal((4, CUndergrounds.RaisePartisans), (command.Province, command.Action));

		state.Outbox.Clear();
		warsaw.UndergroundAction = CUndergrounds.RaisePartisans;
		CUndergroundAi.RunHourly(state);
		Assert.Empty(state.Outbox);

		// Not yet ready: built up.
		warsaw.SetBuildingCurrentLevel(RebelWorld.Underground, CFixedPoint.Parse("0.3"));
		CUndergroundAi.RunHourly(state);
		Assert.Equal(CUndergrounds.BuildUp, Assert.IsType<CChangeUndergroundMissionCommand>(Assert.Single(state.Outbox)).Action);
	}

	[Fact]
	public void The_player_and_the_rebels_are_left_alone()
	{
		CGameState state = RebelWorld.State();
		Place(state, "0.6", CUndergrounds.BuildUp);
		state.Played.Add(RebelWorld.Pol);
		CUndergroundAi.RunHourly(state);
		Assert.Empty(state.Outbox);
	}

	[Fact]
	public void The_occupiers_units_on_the_front_score_a_tenth_of_their_power_and_an_even_side_nothing()
	{
		CGameState state = RebelWorld.State();
		Place(state, "0.6", CUndergrounds.BuildUp);
		CCountry poland = state.Country(RebelWorld.Pol)!;
		CSupplyArea area = CSupplyNetworks.Refresh(state).AreaOf(4)!;
		Assert.Equal(RebelWorld.Ger, area.Controller);
		Assert.Equal(0, CUndergroundAi.Score(state, poland, area));

		CUnit german = Garrison(state, RebelWorld.Ger, 4);
		long power = CUnitPower.Of(german);
		Assert.True(power > 0);
		Assert.Equal(System.Math.Min(power * 100 / 1000, 1000), CUndergroundAi.Score(state, poland, area));

		// Poland's own unit on the front as strong: under a tenth more, the front is passed over.
		Garrison(state, RebelWorld.Pol, 4);
		Assert.Equal(0, CUndergroundAi.Score(state, poland, area));
	}
}
