using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// An underground's start, its day, partisans, spreading, Organized Revolt and a change of holder, against
/// <c>docs/mechanics/undergrounds.md</c>, on <see cref="RebelWorld"/>: Poland's network in Warsaw, which Germany holds.
/// </summary>
public class UndergroundTests
{
	private static CProvince Place(CGameState state, int province, CCountryTag owner, string strength, int action = CUndergrounds.BuildUp)
	{
		CProvince here = state.Province(province);
		here.SetBuildingLevel(RebelWorld.Underground, CFixedPoint.One);
		here.SetBuildingCurrentLevel(RebelWorld.Underground, CFixedPoint.Parse(strength));
		here.UndergroundOwner = owner;
		here.UndergroundAction = action;
		return here;
	}

	[Fact]
	public void A_finished_one_starts_by_its_distance_from_the_builders_nearest_air_base()
	{
		CGameState state = RebelWorld.State();
		CCountry poland = state.Country(RebelWorld.Pol)!;
		CProvince warsaw = state.Province(4);
		Assert.Equal(CFixedPoint.Zero, CUndergrounds.InitialStrength(state, warsaw, poland));

		// Krakow's base is two provinces, 400 of the original's units, from Warsaw: 0.4 x 0.6.
		state.Province(6).SetBuildingLevel(RebelWorld.AirBase, CFixedPoint.One);
		Assert.Equal(CFixedPoint.Parse("0.24"), CUndergrounds.InitialStrength(state, warsaw, poland));

		// An exile building in its own land has it all.
		poland.InExile = true;
		Assert.Equal(CFixedPoint.Parse("0.4"), CUndergrounds.InitialStrength(state, warsaw, poland));

		var messages = new List<CPactMessage>();
		state.PactMessaged += messages.Add;
		CUndergrounds.Finish(state, warsaw, poland);
		Assert.Equal(CFixedPoint.Parse("0.4"), CUndergrounds.Strength(state, warsaw));
		Assert.Equal(RebelWorld.Pol, warsaw.UndergroundOwner);
		Assert.Equal(RebelWorld.Ger, Assert.Single(messages, message => message.We == "BUILDING_DONE").Told);
	}

	[Fact]
	public void One_may_be_built_only_under_an_enemy_holder_in_a_friends_land()
	{
		CGameState state = RebelWorld.State();
		CCountry poland = state.Country(RebelWorld.Pol)!;
		Assert.True(CUndergrounds.CanPlaceIn(state, state.Province(4), poland));
		Assert.False(CUndergrounds.CanPlaceIn(state, state.Province(3), poland));
		Assert.False(CUndergrounds.CanPlaceIn(state, state.Province(5), poland));
		Place(state, 4, RebelWorld.Pol, "0.1");
		Assert.False(CUndergrounds.CanPlaceIn(state, state.Province(4), poland));
	}

	[Theory]
	[InlineData(false, 26, false)]
	[InlineData(false, 100, false)]
	[InlineData(false, 101, true)]
	[InlineData(true, 5, true)]
	public void A_country_builds_in_its_allies_land_as_a_major_or_above_a_hundred_whole_IC(bool major, int maxIc, bool large)
	{
		// Bulgaria, 26 IC in July 1941, builds only in its own land; the original keeps the figure in whole IC
		// (FINDINGS-liveread.md, section 108).
		var country = new CCountry { Tag = new CCountryTag("BUL"), Name = "Bulgaria", DefinitionFile = "", IsMajor = major, BaseIc = maxIc };
		Assert.Equal(large, CUndergrounds.IsLarge(country));
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(249, 0)]
	[InlineData(250, 0)]
	[InlineData(600, 1)]
	[InlineData(750, 2)]
	public void Being_found_is_the_suppression_times_two_rounded_half_to_even_in_a_thousand(long suppression, long threshold)
	{
		Assert.Equal(threshold, CUndergrounds.FindThreshold(suppression, 2000));
	}

	[Fact]
	public void It_grows_a_hundredth_a_day_more_with_the_policys_support_and_goes_when_its_owner_holds_it_again()
	{
		CGameState state = RebelWorld.State();
		CProvince warsaw = Place(state, 4, RebelWorld.Pol, "0.4");
		CUndergrounds.RunDaily(state);
		Assert.Equal(CFixedPoint.Parse("0.41"), CUndergrounds.Strength(state, warsaw));

		warsaw.Modifier.Set(CUndergrounds.PartisanSupport, CFixedPoint.FromInt(19));
		CUndergrounds.RunDaily(state);
		Assert.Equal(CFixedPoint.Parse("0.421"), CUndergrounds.Strength(state, warsaw));

		var messages = new List<CPactMessage>();
		state.PactMessaged += messages.Add;
		warsaw.Controller = RebelWorld.Pol;
		CUndergrounds.RunDaily(state);
		Assert.False(CUndergrounds.Has(state, warsaw));
		Assert.Null(warsaw.UndergroundOwner);
		Assert.Empty(messages);
	}

	[Fact]
	public void On_action_nought_it_raises_one_brigade_for_its_owner_beside_it_and_pays_half()
	{
		CGameState state = RebelWorld.State();
		CProvince warsaw = Place(state, 4, RebelWorld.Pol, "0.6", CUndergrounds.RaisePartisans);

		CUndergrounds.RunDaily(state);

		// Lodz is the one neighbour Poland owns; the rabble rise there, Poland's.
		CRebelFaction faction = Assert.Single(state.RebelFactions);
		CUnit army = CRebelFactions.FindUnit(state, faction.Armies[0])!;
		Assert.Equal(RebelWorld.Pol, army.Owner);
		Assert.Equal(5, army.Location);
		Assert.Single(army.SubUnits);
		Assert.Equal(CFixedPoint.Parse("0.11"), CUndergrounds.Strength(state, warsaw));
	}

	[Fact]
	public void On_action_one_a_full_one_spreads_to_a_neighbour_of_the_same_owner_and_pays_it_all()
	{
		CGameState state = RebelWorld.State();
		state.Province(5).Controller = RebelWorld.Ger;
		CProvince lodz = Place(state, 5, RebelWorld.Pol, "0.995", CUndergrounds.Spread);

		CUndergrounds.RunDaily(state);

		CProvince spread = state.Provinces.Single(province => province != lodz && CUndergrounds.Has(state, province));
		Assert.Contains(spread.Id, new[] { 4, 6 });
		Assert.Equal(RebelWorld.Pol, spread.UndergroundOwner);
		Assert.Equal(CFixedPoint.Zero, CUndergrounds.Strength(state, lodz));
		// The new one is first walked the next day - where Krakow, held by its owner, loses it.
		Assert.Equal(CFixedPoint.Parse("0.2"), CUndergrounds.Strength(state, spread));
		CUndergrounds.RunDaily(state);
		Assert.Equal(spread.Id == 6, !CUndergrounds.Has(state, spread));
	}

	[Fact]
	public void Retaken_by_its_owner_or_a_friend_it_is_dissolved()
	{
		CGameState state = RebelWorld.State();
		CProvince warsaw = Place(state, 4, RebelWorld.Pol, "0.3");
		CProvinceHands.SetController(state, warsaw, RebelWorld.Pol, consequences: true);
		Assert.False(CUndergrounds.Has(state, warsaw));
	}

	[Fact]
	public void Organized_revolt_needs_an_exile_with_one_strictly_above_half_in_its_own_land()
	{
		CGameState state = RebelWorld.State();
		CCountry poland = state.Country(RebelWorld.Pol)!;
		Place(state, 4, RebelWorld.Pol, "0.5");
		Assert.False(CUndergrounds.MayRevolt(state, poland));
		poland.InExile = true;
		Assert.False(CUndergrounds.MayRevolt(state, poland));
		Assert.Equal((1, 0), CUndergrounds.Readiness(state, poland));

		Place(state, 4, RebelWorld.Pol, "0.6");
		Assert.True(CUndergrounds.MayRevolt(state, poland));
		Assert.False(new CSpawnFullRevoltCommand(RebelWorld.Ger).MayExecute(state));
		new CSpawnFullRevoltCommand(RebelWorld.Pol).Execute(state);
		Assert.Single(state.RebelFactions);
	}

	[Fact]
	public void The_mission_stays_with_the_province()
	{
		CGameState state = RebelWorld.State();
		new CChangeUndergroundMissionCommand(4, CUndergrounds.Spread).Execute(state);
		CProvince warsaw = Place(state, 4, RebelWorld.Pol, "0.3", state.Province(4).UndergroundAction);
		CUndergrounds.Remove(state, warsaw);
		Assert.Equal(CUndergrounds.Spread, warsaw.UndergroundAction);
		Assert.False(new CSpawnPartisanCommand(4).MayExecute(state));
	}
}
