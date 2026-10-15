using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// Organized Revolt's tooltip and an underground on the revolt map, against <c>docs/mechanics/undergrounds.md</c>,
/// sections 6 and 8, on <see cref="RebelWorld"/>.
/// </summary>
public class UndergroundTextTests
{
	private static string Localise(string key) => key switch
	{
		"UNDERGROUNDS_READY_TO_RISE" => "$VAL$ of our $TOTAL$",
		"UNDERGROUNDS_NOT_PREPARED" => "None of our $TOTAL$",
		_ => key,
	};

	[Fact]
	public void Organized_revolts_tooltip_counts_the_ready_of_the_undergrounds_in_its_own_land()
	{
		CGameState state = RebelWorld.State();
		CCountry poland = state.Country(RebelWorld.Pol)!;
		Assert.Equal("WE_NOT_GOVERNMENT_IN_EXILE", CPoliticsTooltips.Revolt(Localise, state, poland).Text);

		poland.InExile = true;
		CProvince warsaw = state.Province(4);
		warsaw.SetBuildingLevel(RebelWorld.Underground, CFixedPoint.One);
		warsaw.SetBuildingCurrentLevel(RebelWorld.Underground, CFixedPoint.Parse("0.5"));
		warsaw.UndergroundOwner = RebelWorld.Pol;
		Assert.Equal("None of our 1", CPoliticsTooltips.Revolt(Localise, state, poland).Text);
		warsaw.SetBuildingCurrentLevel(RebelWorld.Underground, CFixedPoint.Parse("0.51"));
		Assert.Equal("1 of our 1", CPoliticsTooltips.Revolt(Localise, state, poland).Text);
	}

	[Fact]
	public void An_underground_paints_a_province_red_on_the_revolt_map()
	{
		CGameState state = RebelWorld.State();
		CProvince warsaw = state.Province(4);
		Assert.Equal(new CColor(0.3f, 0.9f, 0.3f), CRevoltMapColours.Of(state, warsaw, RebelWorld.Ger));
		warsaw.SetBuildingLevel(RebelWorld.Underground, CFixedPoint.One);
		warsaw.Intel[state.Country(RebelWorld.Ger)!.Index] = 9;
		Assert.Equal(new CColor(1f, 0.3f, 0.3f), CRevoltMapColours.Of(state, warsaw, RebelWorld.Ger));
	}
}
