using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The alignment triangle, against <c>docs/mechanics/alignment.md</c>, on <see cref="ScriptWorld"/>'s
/// strip of ground with Their Finest Hour's three corners: Germany and Italy in the Axis, the others in
/// no faction. Every province's centre is the same point, so every capital is nought apart.
/// </summary>
public class AlignmentTests
{
	public static CIdeologyGroup[] Groups()
	{
		var fascism = new CIdeologyGroup("fascism", 200000, 200000) { Faction = "axis", Influence = "align_towards_axis" };
		fascism.Ideologies.Add("national_socialist");
		var democracy = new CIdeologyGroup("democracy", 0, -146000) { Faction = "allies", Influence = "align_towards_allies" };
		democracy.Ideologies.Add("social_democrat");
		var communism = new CIdeologyGroup("communism", -200000, 200000) { Faction = "comintern", Influence = "align_towards_comintern" };
		communism.Ideologies.Add("stalinist");
		return new[] { fascism, democracy, communism };
	}

	public static CGameState State()
	{
		CGameState state = ScriptWorld.State(groups: Groups());
		state.Defines.Set(CAlignment.Section, CAlignment.Interval, CFixedPoint.FromInt(24));
		state.Defines.Set(CAlignment.Section, CAlignment.IdeologyWeight, CFixedPoint.FromInt(2));
		state.Defines.Set(CAlignment.Section, CAlignment.RepulsionWeight, CFixedPoint.Parse("0.01"));
		state.Defines.Set(CAlignment.Section, CAlignment.FactionThreatDistance, CFixedPoint.FromInt(400));
		return state;
	}

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	[Fact]
	public void A_move_past_the_top_stops_at_it()
	{
		CGameState state = State();

		Assert.Equal((0L, 200000L), CAlignment.ClampMove(state, 0, 0, 0, 300000));
	}

	[Fact]
	public void The_arm_below_the_least_x_has_its_sign_reversed_and_the_edge_overwrites_it()
	{
		CGameState state = State();

		// x - minX where minX - x was meant: 10000 to the right; then the left edge at y 150, 171.098
		// left of nought, puts it there.
		Assert.Equal((18902L, 0L), CAlignment.ClampMove(state, -190000, 150000, -20000, 0));
	}

	[Fact]
	public void A_point_outside_the_triangle_is_put_onto_its_edge_by_the_first_step()
	{
		CGameState state = State();
		CCountry poland = C(state, ScriptWorld.Pol);
		poland.AlignmentX = 20000;
		poland.AlignmentY = -128700;

		Assert.Equal((-10000L, 0L), CAlignment.ClampMove(state, poland.AlignmentX, poland.AlignmentY, 0, 0));
	}

	[Fact]
	public void A_faction_member_is_put_on_its_leader_and_the_leader_stays()
	{
		CGameState state = State();
		CCountry germany = C(state, ScriptWorld.Ger);
		CCountry italy = C(state, ScriptWorld.Ita);
		germany.AlignmentX = 200000;
		germany.AlignmentY = 200000;
		italy.AlignmentX = 150000;

		CAlignment.Step(state, germany);
		CAlignment.Step(state, italy);

		Assert.Equal((200000L, 200000L), (germany.AlignmentX, germany.AlignmentY));
		Assert.Equal((200000L, 200000L), (italy.AlignmentX, italy.AlignmentY));
	}

	[Fact]
	public void A_country_in_no_faction_drifts_toward_its_ideologys_corner_less_its_repulsion()
	{
		CGameState state = State();
		CCountry poland = C(state, ScriptWorld.Pol);
		poland.RulingIdeology = "social_democrat";

		CAlignment.Step(state, poland);

		// Democracy weighs 2000 for the ideology less 540 of repulsion at 146 away; the unit vector's
		// length is 1000000 / 146000 = 6, so the move is -146000 x 6 / 1000 x 1460 / 1000.
		Assert.Equal((0L, -1278L), (poland.AlignmentX, poland.AlignmentY));
	}

	[Fact]
	public void A_new_game_steps_on_its_first_day_and_then_every_interval_and_one_days()
	{
		CGameState state = State();
		CCountry poland = C(state, ScriptWorld.Pol);
		poland.RulingIdeology = "social_democrat";
		long start = state.TotalHours;

		CAlignment.RunDaily(state);
		long first = poland.AlignmentY;
		CAlignment.RunDaily(state);
		Assert.Equal(first, poland.AlignmentY);
		Assert.Equal(start + 24 * 24, poland.NextAlignmentStep);

		for (int hour = 0; hour < 24 * 24; hour++)
		{
			state.AdvanceOneHour();
		}
		CAlignment.RunDaily(state);
		Assert.Equal(first, poland.AlignmentY);                 // the twenty-fourth day: not yet
		for (int hour = 0; hour < 24; hour++)
		{
			state.AdvanceOneHour();
		}
		CAlignment.RunDaily(state);
		Assert.NotEqual(first, poland.AlignmentY);              // the twenty-fifth
	}

	[Fact]
	public void A_country_in_no_faction_counts_as_the_group_nearest_where_it_stands()
	{
		CGameState state = State();
		CCountry poland = C(state, ScriptWorld.Pol);

		Assert.Equal("democracy", CAlignment.GroupOf(state, poland)!.Name);
		poland.AlignmentX = -150000;
		poland.AlignmentY = 150000;
		Assert.Equal("communism", CAlignment.GroupOf(state, poland)!.Name);
		Assert.Equal("fascism", CAlignment.GroupOf(state, C(state, ScriptWorld.Ita))!.Name);
	}
}
