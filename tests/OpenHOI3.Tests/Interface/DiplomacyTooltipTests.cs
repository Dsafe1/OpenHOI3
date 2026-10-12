using System.Collections.Generic;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The diplomacy screen's tooltips, against <c>docs/mechanics/diplomacyscreen.md</c>, sections 6 and 7,
/// on <see cref="ScriptWorld"/>'s strip of ground.
/// </summary>
public class DiplomacyTooltipTests
{
	private static readonly Dictionary<string, string> Texts = new()
	{
		["AND"] = "and ",
		["DIP_SURPLUS"] = "$NAME$ is producing a surplus of §Y$VAL$§W $RES$ each day.",
		["DIP_NO_SURPLUS"] = "$NAME$ is in deficit of §Y$VAL$§W $RES$ each day.",
		["ENERGY_IRO"] = "Energy",
		["RELATION_CHANGE_VALUE"] = "Estimated yearly change:",
		["RELATION_CAPPED"] = "Can not go over this relation with gradual improvements.",
		["DIPRELCH_WAR"] = "War",
		["DIPRELCH_NBOR_PEACE"] = "Neighbours at Peace",
		["DIPRELCH_GUARANTEE"] = "Guarantees",
		["DIPRELCH_DEPENDENCY"] = "Puppet-Master Relationship",
		["WARNE_THREAT"] = "Mutual Threat is §Y$VAL$§W.\n",
		["WARNE_CONTINENT"] = "Different Continent: §R-$VAL$§W.\n",
		["WARNE_FACTION"] = "Member of $NAME$ : $VAL$§W.\n",
		["FACTION_MEMBER_OF"] = "$NAME$ is a $FACTION$ member.",
		["RULE_LIMITED_WAR"] = "Limited war",
		["YES"] = "Yes",
		["axis"] = "Axis",
		["allies"] = "Allies",
		["comintern"] = "Comintern",
		["DIP_ALIGNED_DRIFT"] = "$NAME$ is a $FACTION$ member and will not drift.",
		["FACTION_DRIFT_INSTANT"] = "$NAME$ is drifting §Y$VAL$§W towards $FACTION$",
		["FACTION_DRIFT_TOP_INSTANT"] = "$NAME$ is mostly drifting towards §G$FACTION$§W with a value of §Y$VAL$§W",
		["FACTION_DRIFT_PROXIMITY"] = "§Y$VAL$§W due to proximity to faction members.",
		["CAN_GIVE_STRAT_BONUS_BUT_NONE"] = "Closely enough aligned to provide strategic resources to §Y$WHO$§W, but they have none..",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	[Fact]
	public void A_resource_icon_says_surplus_or_deficit_and_at_nought_a_surplus_its_icon_red()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = C(state, ScriptWorld.Ger);

		Assert.Equal("GER is producing a surplus of §Y0.00§W Energy each day.",
			CDiplomacyTooltips.ResourceIcon(Localise, germany, EGoods.Energy).Text);
		Assert.False(CDiplomacyScreenText.InSurplus(germany, EGoods.Energy));

		germany.HomeProduced[EGoods.Energy] = CFixedPoint.Parse("1.5");
		Assert.Equal("GER is producing a surplus of §Y1.50§W Energy each day.",
			CDiplomacyTooltips.ResourceIcon(Localise, germany, EGoods.Energy).Text);
		Assert.True(CDiplomacyScreenText.InSurplus(germany, EGoods.Energy));

		germany.Usage[EGoods.Energy] = CFixedPoint.Parse("2.256");
		Assert.Equal("GER is in deficit of §Y0.76§W Energy each day.",                   // rounded, not cut
			CDiplomacyTooltips.ResourceIcon(Localise, germany, EGoods.Energy).Text);
	}

	[Fact]
	public void A_relation_row_says_a_year_of_the_drift_and_its_causes_at_their_constants()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = C(state, ScriptWorld.Ger);
		CCountry poland = C(state, ScriptWorld.Pol);

		CTooltip tooltip = CDiplomacyTooltips.Relation(Localise, state, germany, poland);

		Assert.Equal("Estimated yearly change: §G0.60§W", tooltip.Text);
		Assert.Equal("§G+0.60§W: Neighbours at Peace\n", tooltip.Delayed);

		state.Guarantees.Add(new CGuarantee(ScriptWorld.Pol, ScriptWorld.Ger));
		Assert.Equal("§G+0.60§W: Neighbours at Peace\n§G+0.96§W: Guarantees\n",
			CDiplomacyTooltips.Breakdown(Localise, state, germany, poland));
	}

	[Fact]
	public void At_war_the_breakdown_has_war_and_no_neighbours_at_peace()
	{
		CGameState state = ScriptWorld.State(atWar: true);
		CCountry germany = C(state, ScriptWorld.Ger);
		CCountry poland = C(state, ScriptWorld.Pol);

		Assert.Equal("Estimated yearly change: §R-1.20§W", CDiplomacyTooltips.Relation(Localise, state, germany, poland).Text);
		Assert.Equal("§R-1.20§W: War\n", CDiplomacyTooltips.Breakdown(Localise, state, germany, poland));
	}

	[Fact]
	public void Above_100_with_a_drift_not_negative_the_relation_is_capped()
	{
		CGameState state = ScriptWorld.State();
		CCountry germany = C(state, ScriptWorld.Ger);
		CCountry italy = C(state, ScriptWorld.Ita);
		CRelations.Change(germany, italy, CFixedPoint.FromInt(150));

		// Nothing moves the pair at all, and still it is capped.
		Assert.Equal(Texts["RELATION_CAPPED"], CDiplomacyTooltips.Breakdown(Localise, state, germany, italy));
	}

	[Fact]
	public void The_ceiling_s_lines_name_the_larger_threat_another_continent_and_the_faction()
	{
		CGameState state = ScriptWorld.State(groups: new[]
		{
			new CIdeologyGroup("fascism", 200000, 200000) { Faction = "axis", FactionNeutrality = CFixedPoint.FromInt(25) },
		});
		state.Defines.Set(CNeutrality.Section, CNeutrality.DifferentContinent, CFixedPoint.FromInt(10));
		CCountry germany = C(state, ScriptWorld.Ger);
		CCountry italy = C(state, ScriptWorld.Ita);
		CGameState.Status(italy, germany).Threat = CFixedPoint.Parse("7.9");

		Assert.Equal("Mutual Threat is §Y7§W.\nDifferent Continent: §R-10§W.\nMember of Axis : §G+25§W.\n",
			CDiplomacyTooltips.CeilingLines(Localise, state, germany, italy));
		Assert.Equal(CFixedPoint.Parse("22.9"), CNeutrality.WarCeiling(state, germany, italy));
		Assert.Equal("Mutual Threat is §Y0§W.\n", CDiplomacyTooltips.CeilingLines(Localise, state, C(state, ScriptWorld.Pol), germany));
	}

	[Fact]
	public void The_ceiling_is_never_below_nought()
	{
		CGameState state = ScriptWorld.State();
		state.Defines.Set(CNeutrality.Section, CNeutrality.DifferentContinent, CFixedPoint.FromInt(10));

		Assert.Equal(CFixedPoint.Zero, CNeutrality.WarCeiling(state, C(state, ScriptWorld.Pol), C(state, ScriptWorld.Ita)));
	}

	[Fact]
	public void A_faction_says_its_rules_and_then_its_modifier()
	{
		var axis = new CIdeologyGroup("fascism", 200000, 200000) { Faction = "axis", FactionModifier = new CModifier() };
		axis.FactionRules.Add("limited_war");

		CTooltip tooltip = CDiplomacyTooltips.Faction(Localise, axis);

		Assert.Equal("Axis", tooltip.Text);
		Assert.Equal("Limited war: §GYes§W\n", tooltip.Delayed);
	}

	[Fact]
	public void A_member_s_flag_says_it_is_a_member()
	{
		CGameState state = ScriptWorld.State(groups: AlignmentTests.Groups());

		Assert.Equal("ITA is a Axis member.", CDiplomacyTooltips.FactionMember(Localise, state, C(state, ScriptWorld.Ita)).Text);
	}

	[Fact]
	public void A_member_s_marker_says_only_that_it_will_not_drift()
	{
		CGameState state = AlignmentTests.State();

		CTooltip tooltip = CDriftTooltip.For(Localise, state, C(state, ScriptWorld.Ger));

		Assert.Equal("GER is a Axis member and will not drift.", tooltip.Text);
		Assert.Equal("", tooltip.Delayed);
	}

	[Fact]
	public void A_marker_starts_with_the_drift_and_puts_the_strongest_faction_first()
	{
		CGameState state = AlignmentTests.State();
		CCountry poland = C(state, ScriptWorld.Pol);
		poland.AlignmentX = 100000;
		poland.AlignmentY = 100000;
		state.Defines.Set(CAlignment.Section, CDriftTooltip.StrategicBonusDistance, CFixedPoint.FromInt(150));

		CTooltip tooltip = CDriftTooltip.For(Localise, state, poland);
		string[] lines = tooltip.Text.Split('\n');
		(long x, long y) = CAlignment.Drift(state, poland);

		Assert.StartsWith("(" + (x > 0 ? "+" : ""), lines[0]);
		Assert.StartsWith("POL is mostly drifting towards §GAxis§W with a value of", lines[1]);
		// The Axis is the only faction with members; within 150 of its corner, which has nobody's
		// resources to give, the last line says so.
		Assert.Equal("Closely enough aligned to provide strategic resources to §YGER§W, but they have none..", lines[^1]);
		Assert.StartsWith("(Axis)\n", tooltip.Delayed);
		Assert.True(y != 0 || x != 0);
	}

	[Fact]
	public void The_top_line_s_figure_is_rounded_where_the_others_are_cut()
	{
		Assert.Equal("1.24", CDiplomacyTooltips.Rounded(1239, 1));
		Assert.Equal("3.00", CDiplomacyTooltips.Rounded(250, 12));
		Assert.Equal("-1.20", CDiplomacyTooltips.Rounded(-100, 12));
	}
}
