using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the province view and the revolt map mode say of revolt risk, against <c>docs/mechanics/revolt.md</c>, section
/// 7, on <see cref="ScriptWorld"/> at war with Germany holding Warsaw.
/// </summary>
public class RevoltRiskTextTests
{
	private static string Localise(string key) => key;

	private static (CGameState State, CProvince Warsaw, CCountry Germany) World()
	{
		CGameState state = ScriptWorld.State(atWar: true);
		CProvince warsaw = state.Province(4);
		warsaw.Controller = ScriptWorld.Ger;
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		var local = new CModifier("nationalism");
		local.Set(CRevoltRisk.LocalRevoltRisk, CFixedPoint.Parse("0.6"));
		warsaw.Modifier.Add(local);
		warsaw.ModifierSources.Add(new CModifierSource(local));
		warsaw.RevoltRisk = CFixedPoint.Parse("1.25");
		warsaw.Suppression = CFixedPoint.Parse("0.5");
		return (state, warsaw, germany);
	}

	[Fact]
	public void Under_the_fog_it_cannot_be_told_and_the_figure_wants_everything_known()
	{
		(CGameState state, CProvince warsaw, CCountry germany) = World();
		Assert.Equal("CANT_SEE_UNDER_FOW", CRevoltRiskTooltip.Text(Localise, state, warsaw, germany.Tag));
		Assert.Equal("?", CRevoltRiskTooltip.Readout(state, warsaw, germany.Tag));

		warsaw.Intel[germany.Index] = 9;
		Assert.Equal("§Y1.3%", CRevoltRiskTooltip.Readout(state, warsaw, germany.Tag));
	}

	[Fact]
	public void The_tooltip_is_the_support_the_sources_the_suppression_and_the_minimum()
	{
		(CGameState state, CProvince warsaw, CCountry germany) = World();
		warsaw.Intel[germany.Index] = 2;

		string[] lines = CRevoltRiskTooltip.Text(Localise, state, warsaw, germany.Tag).Split('\n');

		Assert.Equal("PARTISAN_SUPPORT: §R+1.25§W", lines[0]);
		Assert.StartsWith("nationalism: ", lines[1]);
		Assert.Equal("SUPPRESSION: §G-0.50§W", lines[2]);
		Assert.Equal("MINIMUM_REVOLT_RISK: §Y0.00%§W", lines[3]);
		Assert.Equal(4, lines.Length);
	}

	[Fact]
	public void The_map_is_grey_where_not_known_and_green_to_red_by_the_figure()
	{
		(CGameState state, CProvince warsaw, CCountry germany) = World();
		warsaw.Intel[germany.Index] = 9;
		CColor Colour() => CRevoltMapColours.Of(state, warsaw, germany.Tag);

		// 1.25 + 0.6 - 0.5: yellow.
		Assert.Equal(new CColor(0.9f, 0.9f, 0.3f), Colour());
		warsaw.RevoltRisk = CFixedPoint.FromInt(3);
		Assert.Equal(new CColor(0.9f, 0.6f, 0.3f), Colour());
		warsaw.RevoltRisk = CFixedPoint.FromInt(20);
		Assert.Equal(new CColor(1f, 0.3f, 0.3f), Colour());
		warsaw.RevoltRisk = CFixedPoint.Zero;
		warsaw.Suppression = CFixedPoint.FromInt(3);
		Assert.Equal(new CColor(0.3f, 0.9f, 0.3f), Colour());

		// Poland's land Germany does not hold and hardly knows.
		CProvince posen = state.Province(3);
		posen.Controller = ScriptWorld.Pol;
		Assert.Equal(new CColor(0.45f, 0.45f, 0.45f), CRevoltMapColours.Of(state, posen, germany.Tag));
	}
}
