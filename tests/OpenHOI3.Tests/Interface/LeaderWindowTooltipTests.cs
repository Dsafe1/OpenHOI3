using System.Collections.Generic;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the window a leader is chosen in says in a tooltip, against
/// <c>docs/mechanics/leaders.md</c>, section 13, with Their Finest Hour's own strings where
/// the test needs their shape.
/// </summary>
public class LeaderWindowTooltipTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static readonly Dictionary<string, string> Texts = new()
	{
		["INACTIVE"] = "(inactive)",
		["SEA_RANK1"] = "Commander",
		["NO_LEADER"] = "(no leader)",
		["LEADER_COMMAND_SIZE"] = "Max command",
		["LEADER_POSITIONING"] = "Positioning Bonus",
		["LEADER_SKILL"] = "Skill",
		["LEADER_SKILL_AT_MAX"] = "Max",
		["LEADER_XP"] = "Experience",
		["TO_GAIN_TRAITS_DESC"] = "Traits to gain:",
		["NO_TRAIT_TO_GAIN_DESC"] = "Currently no traits to gain.",
		["TRAIT_GAIN_PROGRESS"] = "§G$PROG$%§! progress toward trait §Y$TRAIT$§!",
		["TRAIT_FILTER_DESC"] = "Filter leaders by traits.",
		["TRAIT_FILTER_ENTRY_DESC"] = "§YRMB§! - To toggle all traits.",
		["UW_LEADER_BUTTON_IRO"] = "Assign new leader.",
		["UW_LEADER_BUTTON_DRO"] = "Clicking here will allow you to replace the current leader",
		["TRAIT_OFFENCE_MODIFIER"] = "Offence",
		["TRAIT_SUPPLY_CONSUMPTION"] = "Supply consumption",
		["TRAIT_WINTER_ATTRITION"] = "Winter attrition",
		["TRAIT_DISENGAGE_TIMER"] = "Disengage Timer",
		["TRAIT_DEFENCE_MODIFIER"] = "Defence",
		["TRAIT_TERRAIN_ATTACK"] = "Attack in terrain §Y$TYPE$§!",
		["forest"] = "Forest",
		["panzer_leader"] = "Panzer Leader",
		["winter_specialist"] = "Winter Specialist",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static CGameState State(out CCountry country, params CTrait[] traits)
	{
		country = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 };
		return new CGameState(new CDate(1936, 1, 1), new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
		}, new[] { country })
		{
			Traits = traits,
		};
	}

	private static CUnit Unit(CCountry country, EUnitKind kind)
	{
		var unit = new CUnit(kind, country.Tag) { Id = country.Units.Count + 1, Location = 1, Name = "1. Flotte" };
		country.Units.Add(unit);
		return unit;
	}

	private static CTrait Trait(string key, params CTraitEffect[] effects)
	{
		var trait = new CTrait(key);
		trait.Effects.AddRange(effects);
		return trait;
	}

	// ---- a trait's icon

	[Fact]
	public void ATraitsIconIsItsNameAndThenALineForEachEffect()
	{
		CTrait trait = Trait("panzer_leader",
			new CTraitEffect(ETraitEffect.OffenceModifier, CFixedPoint.Parse("0.1"), ""),
			new CTraitEffect(ETraitEffect.DisengageTimer, CFixedPoint.FromInt(2), ""));

		CTooltip tooltip = CLeaderWindowTooltips.TraitIcon(Localise, trait);

		Assert.Equal("Panzer Leader", tooltip.Text);
		Assert.Equal("Offence §G+10%§W\nDisengage Timer §G+2", tooltip.Delayed);
	}

	[Theory]
	[InlineData(ETraitEffect.SupplyConsumption, "-0.25", "Supply consumption §G-25%§W")]   // less is better
	[InlineData(ETraitEffect.WinterAttrition, "0.1", "Winter attrition §R+10%§W")]
	[InlineData(ETraitEffect.DefenceModifier, "0", "Defence §R0%§W")]                   // nothing is red
	[InlineData(ETraitEffect.DefenceModifier, "-0.05", "Defence §R-5%§W")]
	[InlineData(ETraitEffect.ParadropMission, "0.5", " §G+50%§W")]                       // no label of its own
	[InlineData(ETraitEffect.DefenceModifier, "0.125", "Defence §G+12%§W")]               // cut, not rounded
	public void AnEffectIsGreenWhereItHelpsAndAPercentage(ETraitEffect kind, string value, string line)
	{
		CTrait trait = Trait("t", new CTraitEffect(kind, CFixedPoint.Parse(value), ""));

		Assert.Equal(line, CLeaderWindowTooltips.Effects(Localise, trait));
	}

	[Fact]
	public void ATerrainEffectNamesItsTerrain()
	{
		CTrait trait = Trait("t", new CTraitEffect(ETraitEffect.TerrainAttack, CFixedPoint.Parse("0.2"), "forest"));

		Assert.Equal("Attack in terrain §YForest§! §G+20%§W", CLeaderWindowTooltips.Effects(Localise, trait));
	}

	// ---- the trait filter

	[Fact]
	public void AFilterRowsCheckboxSaysWhatItsTraitDoesAndWhatTheOtherButtonDoes()
	{
		CTrait trait = Trait("t", new CTraitEffect(ETraitEffect.OffenceModifier, CFixedPoint.Parse("0.1"), ""));

		CTooltip tooltip = CLeaderWindowTooltips.FilterRow(Localise, trait, "background");
		CTooltip none = CLeaderWindowTooltips.FilterRow(Localise, null, "background");

		Assert.Equal("Offence §G+10%§W", tooltip.Text);
		Assert.Equal("§YRMB§! - To toggle all traits.", tooltip.Delayed);
		Assert.Equal("", none.Text);
		Assert.Equal("§YRMB§! - To toggle all traits.", none.Delayed);
		Assert.Equal(CTooltip.Empty, CLeaderWindowTooltips.FilterRow(Localise, trait, "trait_name"));
	}

	// ---- the window

	[Fact]
	public void TheTraitsToGainAreHisProgressOrThatThereAreNone()
	{
		var winter = new CTrait("winter_specialist");
		CGameState state = State(out _, winter);
		var leader = new CLeader(1) { Name = "Erich Raeder" };

		Assert.Equal("Currently no traits to gain.", CLeaderWindowTooltips.TraitsToGain(Localise, state, leader));
		Assert.Equal("Currently no traits to gain.", CLeaderWindowTooltips.TraitsToGain(Localise, state, null));

		leader.TraitProgress.Progress[winter] = 2500;

		Assert.Equal("Traits to gain:\n- §G2.50%§! progress toward trait §YWinter Specialist§!",
			CLeaderWindowTooltips.TraitsToGain(Localise, state, leader));
	}

	[Fact]
	public void WithNoLeaderTheHeadsPortraitIsTheEmptyLeadersTooltip()
	{
		CGameState state = State(out CCountry country);
		CUnit unit = Unit(country, EUnitKind.Division);

		CTooltip tooltip = CLeaderWindowTooltips.Panel(Localise, state, country, unit, null, "selected_photo");

		Assert.Equal("(inactive) §Y(no leader)§W\nSkill §Y0 (Max)§W\nExperience §Y0.00%§W\n", tooltip.Text);
	}

	[Fact]
	public void TheFilterButtonSaysHowItWorks()
	{
		CGameState state = State(out CCountry country);
		CUnit unit = Unit(country, EUnitKind.Division);

		Assert.Equal("Filter leaders by traits.",
			CLeaderWindowTooltips.Panel(Localise, state, country, unit, null, "filter_traits").Text);
	}

	[Fact]
	public void AnAdmiralCommandingNothingDoesNotSayHowManyShipsHeMayCommand()
	{
		CGameState state = State(out CCountry country);
		CUnit fleet = Unit(country, EUnitKind.Navy);
		var admiral = new CLeader(2)
		{
			Name = "Erich Raeder", Type = ELeaderType.Sea, Rank = 1, Skill = 2, MaxSkill = 4, Experience = CFixed15.FromInt(2000),
		};

		CTooltip tooltip = CLeaderWindowTooltips.Row(Localise, state, country, fleet, null, admiral, "photo");

		Assert.Equal("Commander §YErich Raeder§W\nPositioning Bonus §Y20%§W\nSkill §Y2§W\nExperience §Y0.00%§W\n",
			tooltip.Text);
	}

	[Fact]
	public void ARowsButtonSaysNothingOfItsOwnAndSoSaysWhatTheHeadersLeaderButtonDoes()
	{
		CGameState state = State(out CCountry country);
		CUnit unit = Unit(country, EUnitKind.Division);
		var leader = new CLeader(3) { Name = "Heinz Guderian", Rank = 1 };

		CTooltip button = CLeaderWindowTooltips.Row(Localise, state, country, unit, null, leader, "leader_button");
		CTooltip name = CLeaderWindowTooltips.Row(Localise, state, country, unit, null, leader, "leader_name");

		Assert.Equal("Assign new leader.", button.Text);
		Assert.Equal("Clicking here will allow you to replace the current leader", button.Delayed);
		Assert.Equal(CTooltip.Empty, name);
	}
}
