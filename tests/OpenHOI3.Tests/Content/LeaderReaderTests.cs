using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.History;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Leaders;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// <c>common/traits.txt</c> and <c>history/leaders</c> as <c>docs/mechanics/leaders.md</c>,
/// sections 1 and 3, read them.
/// </summary>
public class LeaderReaderTests
{
	private static List<CTrait> Traits(string text, List<string>? warnings = null)
	{
		return CTraitReader.Read(ClausewitzParser.Parse(text), message => warnings?.Add(message));
	}

	[Fact]
	public void ATraitKeepsWhoItIsForAndWhatItDoes()
	{
		List<CTrait> traits = Traits("""
			old_guard = { allowed_leader = { land sea air } xp_gain = -0.5 }
			seawolf = { allowed_leader = { sea } submarine_attack = 0.1 }
			desert_fox = {
				allowed_leader = { land }
				terrain_speed = { type = desert value = 0.05 }
				terrain_attack = { type = desert value = 0.1 }
			}
			""");

		Assert.Equal(new[] { "old_guard", "seawolf", "desert_fox" }, traits.Select(trait => trait.Key));
		Assert.True(traits[0].IsAllowed(ELeaderType.Air));
		Assert.False(traits[1].IsAllowed(ELeaderType.Land));
		Assert.True(traits[1].IsAllowed(ELeaderType.Sea));
		Assert.Equal(new CTraitEffect(ETraitEffect.XpGain, CFixedPoint.Parse("-0.5"), ""), traits[0].Effects.Single());
		Assert.Equal(new CTraitEffect(ETraitEffect.TerrainAttack, CFixedPoint.Parse("0.1"), "desert"), traits[2].Effects[1]);
	}

	[Fact]
	public void ATraitWithNoAllowedLeaderIsForEveryKind()
	{
		CTrait trait = Traits("trickster = { surprise_chance = 0.1 }").Single();

		Assert.True(trait.IsAllowed(ELeaderType.Land) && trait.IsAllowed(ELeaderType.Sea) && trait.IsAllowed(ELeaderType.Air));
	}

	[Fact]
	public void EveryEffectKeyIsItsNameInLowerCaseWithUnderscores()
	{
		Assert.Equal("xp_gain", CTraitReader.KeyOf(ETraitEffect.XpGain));
		Assert.Equal("digin_bonus", CTraitReader.KeyOf(ETraitEffect.DiginBonus));
		Assert.Equal("amph_attack", CTraitReader.KeyOf(ETraitEffect.AmphAttack));
		Assert.Equal("out_of_supply_modifier", CTraitReader.KeyOf(ETraitEffect.OutOfSupplyModifier));
	}

	[Fact]
	public void ASeventeenthEffectIsDroppedAndSaidSo()
	{
		string effects = string.Join(" ", Enumerable.Repeat("xp_gain = 0.1", 17));
		var warnings = new List<string>();

		CTrait trait = Traits($"busy = {{ {effects} unheard_of = 1 }}", warnings).Single();

		Assert.Equal(CTrait.MostEffects, trait.Effects.Count);
		Assert.Contains(warnings, warning => warning.Contains("unheard_of"));
		Assert.Contains(warnings, warning => warning.Contains("busy"));
	}

	private static readonly Dictionary<string, CTrait> Known = new()
	{
		["engineer"] = new CTrait("engineer"),
		["old_guard"] = new CTrait("old_guard"),
		["panzer_leader"] = new CTrait("panzer_leader"),
	};

	private static CLeader Read(string text, CDate start, CLeaderReader.Problems? problems = null)
	{
		ClausewitzNode block = ClausewitzParser.Parse(text).Children.Single();
		return CLeaderReader.Read(block, int.Parse(block.Key!), Known, start, problems);
	}

	[Fact]
	public void ALeaderIsHisFileFoldedToTheStart()
	{
		CLeader leader = Read("""
			798 = {
				name = "von Böhm-Ermolli"
				country = GER
				type = land
				skill = 2
				max_skill = 4
				loyalty = 1.00
				picture = L798
				add_trait = old_guard
				history = {
					1930.1.1 = { rank = 1 }
					1930.1.1 = { rank = 2 }
					1935.6.1 = { add_trait = engineer }
					1940.1.1 = { rank = 3 remove_trait = old_guard }
				}
			}
			""", new CDate(1936, 1, 1));

		Assert.Equal(798, leader.Id);
		Assert.Equal("von Böhm-Ermolli", leader.Name);
		Assert.Equal("GER", leader.Country!.Value.Value);
		Assert.Equal(ELeaderType.Land, leader.Type);
		Assert.Equal(2, leader.Rank);
		Assert.Equal(2, leader.Skill);
		Assert.Equal(4, leader.MaxSkill);
		Assert.Equal(CFixedPoint.One, leader.Loyalty);
		Assert.Equal("L798", leader.Picture);
		Assert.Equal(new[] { "old_guard", "engineer" }, leader.Traits.Select(trait => trait.Key));
		Assert.Equal(new CDate(1930, 1, 1), leader.FirstRankDate);
		Assert.Equal(CFixed15.FromInt(2000), leader.Experience);
	}

	[Fact]
	public void ALeaderNotYetInServiceKnowsTheDayHeWillBe()
	{
		CLeader leader = Read("""
			3 = { name = "Andersen" country = GER type = air skill = 1 history = { 1945.1.1 = { rank = 1 } } }
			""", new CDate(1936, 1, 1));

		Assert.Equal(0, leader.Rank);
		Assert.Equal(ELeaderType.Air, leader.Type);
		Assert.Equal(new CDate(1945, 1, 1), leader.FirstRankDate);
	}

	[Fact]
	public void ADatedSkillDoesNothingAndAnUnknownTraitIsReported()
	{
		var problems = new CLeaderReader.Problems();
		CLeader leader = Read("""
			9 = { country = GER skill = 1 add_trait = no_such_trait history = { 1930.1.1 = { skill = 5 rank = 1 } } }
			""", new CDate(1936, 1, 1), problems);

		Assert.Equal(1, leader.Skill);
		Assert.Empty(leader.Traits);
		Assert.Contains("no_such_trait", problems.UnknownTraits);
	}
}
