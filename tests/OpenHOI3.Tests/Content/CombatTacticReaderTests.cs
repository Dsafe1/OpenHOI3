using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The reading of <c>common/combat_tactics.txt</c>, against
/// <c>docs/mechanics/landcombat.md</c>, section 11. The text is written here in the
/// file's own shape and is not a copy of it.
/// </summary>
public class CombatTacticReaderTests
{
	private readonly List<string> _warnings = new();

	private List<CCombatTactic> Read(string text)
	{
		return CCombatTacticReader.Read(ClausewitzParser.Parse(text), _warnings.Add);
	}

	[Fact]
	public void Tactics_are_numbered_from_one_in_the_order_of_the_file()
	{
		List<CCombatTactic> tactics = Read(@"
			tactic_first = { base = 15 picture = attack attacker = 0.05 }
			tactic_second = { base = 1 aggressiveness = -1 combat_width = -5 defender = 0.25 movement_speed = -0.45 }
		");

		Assert.Equal(2, tactics.Count);
		Assert.Equal(1, tactics[0].Number);
		Assert.Equal("tactic_first", tactics[0].Name);
		Assert.Equal("attack", tactics[0].Picture);
		Assert.Equal(CFixedPoint.Parse("15"), tactics[0].Base);
		Assert.Equal(CFixedPoint.Parse("0.05"), tactics[0].Attacker);
		Assert.Null(tactics[0].Condition);
		Assert.Equal(2, tactics[1].Number);
		Assert.Equal(CFixedPoint.Parse("-1"), tactics[1].Aggressiveness);
		Assert.Equal(CFixedPoint.Parse("-5"), tactics[1].Width);
		Assert.Equal(CFixedPoint.Parse("0.25"), tactics[1].Defender);
		Assert.Equal(CFixedPoint.Parse("-0.45"), tactics[1].Speed);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void What_counters_a_tactic_may_be_declared_after_it()
	{
		List<CCombatTactic> tactics = Read(@"
			tactic_assault = { countered_by = tactic_counterattack }
			tactic_counterattack = { }
		");

		Assert.Equal(2, tactics[0].CounteredByNumber);
		Assert.Equal(0, tactics[1].CounteredByNumber);
	}

	[Fact]
	public void A_trigger_is_read_with_its_groups()
	{
		List<CCombatTactic> tactics = Read(@"
			tactic_blitz = {
				trigger = {
					is_attacker = yes
					has_armour_unit = yes
					OR = { skill = 3 trait = panzer_leader skill_advantage = 2 }
					NOT = { reserves = no }
					frontage_full = no
				}
			}
		");

		CTacticCondition trigger = tactics[0].Condition!;
		Assert.Equal(ETacticCondition.All, trigger.Kind);
		Assert.Equal(5, trigger.Children.Count);
		Assert.Equal(ETacticCondition.IsAttacker, trigger.Children[0].Kind);
		Assert.True(trigger.Children[0].Wanted);
		Assert.Equal(ETacticCondition.HasArmourUnit, trigger.Children[1].Kind);
		CTacticCondition any = trigger.Children[2];
		Assert.Equal(ETacticCondition.Any, any.Kind);
		Assert.Equal(ETacticCondition.Skill, any.Children[0].Kind);
		Assert.Equal(3, any.Children[0].Amount);
		Assert.Equal(ETacticCondition.Trait, any.Children[1].Kind);
		Assert.Equal("panzer_leader", any.Children[1].Name);
		Assert.Equal(ETacticCondition.SkillAdvantage, any.Children[2].Kind);
		Assert.Equal(2, any.Children[2].Amount);
		Assert.Equal(ETacticCondition.None, trigger.Children[3].Kind);
		Assert.False(trigger.Children[3].Children[0].Wanted);
		Assert.Equal(ETacticCondition.FrontageFull, trigger.Children[4].Kind);
		Assert.False(trigger.Children[4].Wanted);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void A_key_or_a_condition_nothing_understands_is_reported()
	{
		List<CCombatTactic> tactics = Read(@"
			tactic_odd = {
				wobble = 3
				trigger = { is_attacker = yes has_cavalry = yes }
			}
		");

		Assert.Contains(_warnings, warning => warning.Contains("wobble"));
		Assert.Contains(_warnings, warning => warning.Contains("has_cavalry"));
		Assert.Equal(ETacticCondition.Never, tactics[0].Condition!.Children[1].Kind);
	}

	[Fact]
	public void A_tactic_declared_twice_keeps_its_first_declaration()
	{
		List<CCombatTactic> tactics = Read(@"
			tactic_same = { base = 1 }
			tactic_same = { base = 9 }
		");

		Assert.Single(tactics);
		Assert.Equal(CFixedPoint.Parse("1"), tactics[0].Base);
		Assert.Contains(_warnings, warning => warning.Contains("more than once"));
	}
}
