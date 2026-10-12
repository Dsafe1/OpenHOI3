using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// <c>common/ideologies.txt</c>: the groups with their faction, its icon, its rules and its modifier,
/// as the diplomacy screen shows them (<c>docs/mechanics/diplomacyscreen.md</c>, section 6.1).
/// </summary>
public class IdeologyReaderTests
{
	private const string Text = """
		fascism = {
			national_socialist = { color = { 50 50 50 } }
			faction = {
				tag = axis
				icon = 2
				rule = { limited_war = yes allience_wide_guarantee = no }
				modifier = { territorial_pride = 0.1 }
				influence = align_towards_axis
				neutrality = 25
			}
			position = { x = 200 y = 200 }
		}
		democracy = {
			social_democrat = { color = { 0 0 255 } }
			faction = {
				tag = allies
				icon = 3
				rule = { endless_war = yes free_resource_gifts = maybe }
			}
			position = { x = 0 y = -146 }
		}
		""";

	[Fact]
	public void Reads_a_faction_s_icon_rules_and_modifier()
	{
		var warnings = new List<string>();
		List<CIdeologyGroup> groups = CIdeologyReader.Read(ClausewitzParser.Parse(Text), warnings.Add);

		CIdeologyGroup fascism = groups[0];
		Assert.Equal(2, fascism.FactionIcon);
		Assert.Equal(new[] { "limited_war" }, fascism.FactionRules);
		Assert.Equal(CFixedPoint.Parse("0.1"), fascism.FactionModifier["territorial_pride"]);
		Assert.Equal(CFixedPoint.FromInt(25), fascism.FactionNeutrality);
		Assert.Contains("national_socialist", fascism.Ideologies);
		Assert.DoesNotContain("faction", fascism.Ideologies);

		CIdeologyGroup democracy = groups[1];
		Assert.Equal(3, democracy.FactionIcon);
		Assert.Empty(democracy.FactionRules);
		Assert.Empty(democracy.FactionModifier.Values);
	}

	[Fact]
	public void A_key_that_is_no_rule_and_a_value_that_is_neither_yes_nor_no_are_reported()
	{
		var warnings = new List<string>();
		CIdeologyReader.Read(ClausewitzParser.Parse(Text), warnings.Add);

		Assert.Equal(2, warnings.Count);
		Assert.Contains(warnings, warning => warning.Contains("unknown rule 'endless_war'"));
		Assert.Contains(warnings, warning => warning.Contains("'free_resource_gifts'") && warning.Contains("neither yes nor no"));
	}
}
