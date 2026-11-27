using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// How the two sides of a land battle come by their tactics, against
/// <c>docs/mechanics/landcombat.md</c>, section 11. Germany attacks Posen out of Berlin.
/// The three defines are the base game's.
/// </summary>
public class CombatTacticTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Posen = 2;

	private static CCombatCondition Side(bool attacker)
	{
		var all = new CCombatCondition(ECombatCondition.All);
		all.Children.Add(new CCombatCondition(ECombatCondition.IsAttacker, attacker));
		return all;
	}

	private static CCombatTactic Tactic(int number, string name, bool attacker, string basis = "1",
		string counteredBy = "", string aggressiveness = "0", string attack = "0", string defence = "0",
		string width = "0", string speed = "0", CCombatCondition? also = null)
	{
		CCombatCondition condition = Side(attacker);
		if (also is not null)
		{
			condition.Children.Add(also);
		}
		return new CCombatTactic(name, number)
		{
			Base = CFixedPoint.Parse(basis),
			CounteredBy = counteredBy,
			Aggressiveness = CFixedPoint.Parse(aggressiveness),
			Attacker = CFixedPoint.Parse(attack),
			Defender = CFixedPoint.Parse(defence),
			Width = CFixedPoint.Parse(width),
			Speed = CFixedPoint.Parse(speed),
			Condition = condition,
		};
	}

	private static CGameState State(out CCountry germany, out CCountry poland, params CCombatTactic[] tactics)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = Posen };
		var defines = new CDefines();
		void Define(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Define(CFrontLine.BaseWidth, "10");
		Define(CFrontLine.AdditionalWidth, "5");
		Define(CCombatTactics.SwapFrequency, "24");
		Define(CCombatTactics.CounterAdvantage, "0.35");
		Define(CCombatTactics.AggressivenessImpact, "0.5");
		CCombatTactics.Link(tactics);
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(Posen, "Posen", isSea: false) { Owner = Pol, Controller = Pol },
			},
			new[] { germany, poland }, defines: defines)
		{
			Tactics = tactics,
		};
	}

	private static CUnit Division(CGameState state, CCountry country, int province, string type = "infantry_brigade",
		bool armour = false, string width = "1")
	{
		var unit = new CUnit(EUnitKind.Division, country.Tag) { Id = ++state.LastUnitId, Location = province, Name = "Division" };
		var kind = new CSubUnitType(type, EUnitBranch.Land);
		kind.Stats.Values.Set(CSubUnitType.CombatWidth, CFixedPoint.Parse(width));
		kind.Flags[CCombatCondition.ArmourFlag] = armour;
		var subUnit = new CSubUnit(kind) { Id = ++state.LastUnitId, Unit = unit };
		subUnit.Stats.CopyFrom(kind.Stats);
		unit.SubUnits.Add(subUnit);
		country.Units.Add(unit);
		return unit;
	}

	private static CCombat Battle(CGameState state, CCountry germany, CCountry poland)
	{
		var combat = new CCombat(Posen);
		state.Combats.Add(combat);
		combat.Attacker.Add(Division(state, germany, Berlin));
		combat.Defender.Add(Division(state, poland, Posen));
		CCombats.Begin(state, combat);
		return combat;
	}

	[Fact]
	public void Each_side_picks_from_the_tactics_whose_conditions_it_meets()
	{
		CGameState state = State(out CCountry germany, out CCountry poland,
			Tactic(1, "tactic_attack", attacker: true), Tactic(2, "tactic_defend", attacker: false));
		CCombat combat = Battle(state, germany, poland);

		CCombatTactics.Pick(state, combat);

		Assert.Equal("tactic_attack", combat.Attacker.Tactic?.Name);
		Assert.Equal("tactic_defend", combat.Defender.Tactic?.Name);
	}

	[Fact]
	public void A_pick_of_both_sides_makes_four_rolls()
	{
		CGameState state = State(out CCountry germany, out CCountry poland,
			Tactic(1, "tactic_attack", attacker: true), Tactic(2, "tactic_defend", attacker: false));
		CCombat combat = Battle(state, germany, poland);
		long before = state.Random.Draws;

		CCombatTactics.Pick(state, combat);

		Assert.Equal(before + 4, state.Random.Draws);
	}

	[Fact]
	public void Nothing_is_picked_and_nothing_rolled_with_no_tactics_to_pick_from()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CCombat combat = Battle(state, germany, poland);

		// An hour with tactics to pick from makes the pick's four rolls more than one without.
		CGameState other = State(out CCountry otherGermany, out CCountry otherPoland,
			Tactic(1, "tactic_attack", attacker: true), Tactic(2, "tactic_defend", attacker: false));
		CCombat otherCombat = Battle(other, otherGermany, otherPoland);

		Assert.False(CCombatTactics.IsDue(state, combat));
		CCombats.RunHour(state, combat);
		CCombats.RunHour(other, otherCombat);

		Assert.Null(combat.Attacker.Tactic);
		Assert.NotNull(otherCombat.Attacker.Tactic);
		Assert.Equal(state.Random.Draws + 4, other.Random.Draws);
	}

	[Fact]
	public void A_tactic_with_no_weight_is_never_picked()
	{
		CGameState state = State(out CCountry germany, out CCountry poland,
			Tactic(1, "tactic_never", attacker: true, basis: "0"),
			Tactic(2, "tactic_attack", attacker: true),
			Tactic(3, "tactic_defend", attacker: false));
		CCombat combat = Battle(state, germany, poland);

		for (uint seed = 1; seed <= 20; seed++)
		{
			state.Random.Seed(seed);
			CCombatTactics.Pick(state, combat);
			Assert.Equal("tactic_attack", combat.Attacker.Tactic?.Name);
		}
	}

	[Fact]
	public void A_battle_whose_defenders_have_all_left_picks_no_tactic_and_sums_no_speed()
	{
		CGameState state = State(out CCountry germany, out CCountry poland,
			Tactic(1, "tactic_attack", attacker: true, speed: "0.5"), Tactic(2, "tactic_defend", attacker: false));
		CCombat combat = Battle(state, germany, poland);
		combat.Defender.Remove(state, combat.Defender.Units[0]);
		long before = state.Random.Draws;

		CCombatTactics.Pick(state, combat);

		// The defender still counts Poland among the countries it fought as, but none is in the fight any more.
		Assert.Contains(Pol, combat.Defender.Countries);
		Assert.Null(combat.Attacker.Tactic);
		Assert.Equal(CFixedPoint.Zero, combat.TacticSpeed);
		Assert.Equal(before, state.Random.Draws);
	}

	[Fact]
	public void A_side_with_nothing_to_pick_has_no_tactic()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, Tactic(1, "tactic_attack", attacker: true));
		CCombat combat = Battle(state, germany, poland);

		CCombatTactics.Pick(state, combat);

		Assert.NotNull(combat.Attacker.Tactic);
		Assert.Null(combat.Defender.Tactic);
	}

	[Fact]
	public void A_tactic_weighs_its_base_and_what_the_country_s_technologies_add()
	{
		CCombatTactic assault = Tactic(1, "tactic_assault", attacker: true, basis: "1");
		CGameState state = State(out CCountry germany, out CCountry poland, assault);
		CCombat combat = Battle(state, germany, poland);
		germany.Technology.Set("tactic_assault", CFixedPoint.Parse("6"));   // two levels of three

		Assert.Equal(7000, CCombatTactics.Weight(state, combat, combat.Attacker, assault, 0, 0, 0));
		Assert.Equal(0, CCombatTactics.Weight(state, combat, combat.Defender, assault, 0, 0, 0));
	}

	[Fact]
	public void The_tactic_favoured_weighs_more_by_the_lead_in_skill()
	{
		CCombatTactic counter = Tactic(1, "tactic_counterattack", attacker: false, basis: "1");
		CGameState state = State(out CCountry germany, out CCountry poland, counter);
		CCombat combat = Battle(state, germany, poland);

		// 1 * (1 + 0.35 * 2)
		Assert.Equal(1700, CCombatTactics.Weight(state, combat, combat.Defender, counter, 0, favoured: 1, skillDifference: 2));
		Assert.Equal(1000, CCombatTactics.Weight(state, combat, combat.Defender, counter, 0, favoured: 2, skillDifference: 2));
	}

	[Fact]
	public void A_tactic_weighs_less_the_further_its_aggressiveness_is_from_the_leader_s()
	{
		CCombatTactic reckless = Tactic(1, "tactic_reckless_assault", attacker: true, basis: "2", aggressiveness: "1");
		CGameState state = State(out CCountry germany, out CCountry poland, reckless);
		CCombat combat = Battle(state, germany, poland);

		Assert.Equal(1000, CCombatTactics.Weight(state, combat, combat.Attacker, reckless, 0, 0, 0));        // 2 * (1 - 1 * 0.5)
		Assert.Equal(2000, CCombatTactics.Weight(state, combat, combat.Attacker, reckless, 1000, 0, 0));
		Assert.Equal(1000, CCombatTactics.Weight(state, combat, combat.Attacker, reckless, -1000, 0, 0));    // the distance is held to 1
	}

	[Fact]
	public void A_tactic_the_other_side_s_counters_does_nothing()
	{
		CCombatTactic assault = Tactic(1, "tactic_assault", attacker: true, counteredBy: "tactic_counterattack", attack: "0.25", width: "5");
		CCombatTactic counter = Tactic(2, "tactic_counterattack", attacker: false, defence: "0.25", speed: "-0.1");
		CGameState state = State(out CCountry germany, out CCountry poland, assault, counter);
		CCombat combat = Battle(state, germany, poland);

		CCombatTactics.Pick(state, combat);

		Assert.True(CCombatTactics.Counters(counter, assault));
		Assert.False(CCombatTactics.Counters(assault, counter));
		Assert.Equal(CFixedPoint.Zero, combat.TacticAttacker);
		Assert.Equal(CFixedPoint.Zero, combat.TacticWidth);
		Assert.Equal(CFixedPoint.Parse("0.25"), combat.TacticDefender);
		Assert.Equal(CFixedPoint.Parse("-0.1"), combat.TacticSpeed);
	}

	[Fact]
	public void A_surprise_takes_the_counter_tactic_outright_where_the_side_may_pick_it()
	{
		CCombatTactic assault = Tactic(1, "tactic_assault", attacker: true, counteredBy: "tactic_counterattack");
		CCombatTactic counter = Tactic(2, "tactic_counterattack", attacker: false, basis: "0.001");
		CCombatTactic delay = Tactic(3, "tactic_delay", attacker: false, basis: "1000");
		CGameState state = State(out CCountry germany, out CCountry poland, assault, counter, delay);
		CCombat combat = Battle(state, germany, poland);

		// A certain surprise: the counter, whatever it weighs.
		CCombatTactics.PickSecond(state, combat, combat.Defender, 0, counter.Number, 0, surprise: 1000);
		Assert.Same(counter, combat.Defender.Tactic);

		// A tactic the side may not pick is no surprise, and the pick is the plain one.
		CCombatTactics.PickSecond(state, combat, combat.Defender, 0, 0, 0, surprise: 1000, counter: assault.Number);
		Assert.NotSame(assault, combat.Defender.Tactic);
		Assert.NotNull(combat.Defender.Tactic);

		// Without the fix nobody has a chance of one.
		Assert.Equal(0, CCombatTactics.SurpriseChance(state, combat.Defender));
	}

	[Fact]
	public void Has_combined_arms_holds_whatever_was_written_and_an_enhanced_game_asks_it()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, Tactic(1, "tactic_assault", attacker: true));
		CCombat combat = Battle(state, germany, poland);                 // a brigade a side: nobody has combined arms
		var yes = new CCombatCondition(ECombatCondition.HasCombinedArms);
		var no = new CCombatCondition(ECombatCondition.HasCombinedArms, wanted: false);

		// The original's: what follows the key is not read, so "= no" is "= yes" - and nobody has it.
		Assert.False(yes.Holds(state, combat, combat.Attacker));
		Assert.False(no.Holds(state, combat, combat.Attacker));

		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		Assert.False(yes.Holds(state, combat, combat.Attacker));
		Assert.True(no.Holds(state, combat, combat.Attacker));

		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(new[] { OpenHOI3.Sim.Rules.EFix.CombinedArmsCondition }));
		Assert.False(no.Holds(state, combat, combat.Attacker));
	}

	[Fact]
	public void The_attacker_picks_with_the_defenders_aggressiveness_and_an_enhanced_games_sides_each_with_their_own()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, Tactic(1, "tactic_assault", attacker: true));
		CCombat combat = Battle(state, germany, poland);
		CUnit attacking = combat.Attacker.Units[0];
		CUnit defending = combat.Defender.Units[0];
		attacking.Leader = new OpenHOI3.Sim.Leaders.CLeader(1) { Skill = 2, Rank = 1, Name = "attacker", Unit = attacking };
		defending.Leader = new OpenHOI3.Sim.Leaders.CLeader(2) { Skill = 2, Rank = 1, Name = "defender", Unit = defending };
		attacking.HasOwnAggression = true;
		attacking.Aggression = new CFixedPoint(1000);
		defending.HasOwnAggression = true;
		defending.Aggression = new CFixedPoint(-500);

		// The original's: one value, the defender's leader's, and only the attacker's pick is given it.
		Assert.Equal(-500, CCombatTactics.Aggressiveness(state, combat, combat.Attacker));
		Assert.Equal(0, CCombatTactics.Aggressiveness(state, combat, combat.Defender));

		// The fix: each side its own leader's, and nought with no leader.
		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		Assert.Equal(1000, CCombatTactics.Aggressiveness(state, combat, combat.Attacker));
		Assert.Equal(-500, CCombatTactics.Aggressiveness(state, combat, combat.Defender));
		defending.Leader = null;
		Assert.Equal(0, CCombatTactics.Aggressiveness(state, combat, combat.Defender));

		// An enhanced game without it keeps the original's.
		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(new[] { OpenHOI3.Sim.Rules.EFix.TacticAggressiveness }));
		Assert.Equal(1000, CCombatTactics.Aggressiveness(state, combat, combat.Attacker));
		Assert.Equal(0, CCombatTactics.Aggressiveness(state, combat, combat.Defender));
	}

	[Fact]
	public void Two_tactics_that_do_not_counter_each_other_both_count()
	{
		CCombatTactic shock = Tactic(1, "tactic_shock", attacker: true, defence: "-0.25");
		CCombatTactic delay = Tactic(2, "tactic_delay", attacker: false, attack: "-0.25", defence: "-0.15", speed: "-0.25");
		CGameState state = State(out CCountry germany, out CCountry poland, shock, delay);
		CCombat combat = Battle(state, germany, poland);

		CCombatTactics.Pick(state, combat);

		Assert.Equal(CFixedPoint.Parse("-0.25"), combat.TacticAttacker);
		Assert.Equal(CFixedPoint.Parse("-0.4"), combat.TacticDefender);
		Assert.Equal(CFixedPoint.Parse("-0.25"), combat.TacticSpeed);
	}

	[Fact]
	public void Tactics_are_picked_in_a_battle_s_first_hour_and_again_every_swap_period()
	{
		CGameState state = State(out CCountry germany, out CCountry poland,
			Tactic(1, "tactic_attack", attacker: true), Tactic(2, "tactic_defend", attacker: false));
		CCombat combat = Battle(state, germany, poland);

		Assert.True(CCombatTactics.IsDue(state, combat));
		CCombatTactics.Pick(state, combat);
		combat.Duration = 1;
		Assert.False(CCombatTactics.IsDue(state, combat));
		combat.Duration = 23;
		Assert.False(CCombatTactics.IsDue(state, combat));
		combat.Duration = 24;
		Assert.True(CCombatTactics.IsDue(state, combat));
		combat.Duration = 48;
		Assert.True(CCombatTactics.IsDue(state, combat));
	}

	[Fact]
	public void A_full_front_and_reserves_are_what_their_conditions_ask_for()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CCombat combat = Battle(state, germany, poland);
		var full = new CCombatCondition(ECombatCondition.FrontageFull);
		var reserves = new CCombatCondition(ECombatCondition.Reserves);

		Assert.False(full.Holds(state, combat, combat.Attacker));
		Assert.False(reserves.Holds(state, combat, combat.Attacker));

		// A front is full only once it takes more than the width: ten is not over ten.
		combat.Attacker.Front.Add(Division(state, germany, Berlin, width: "9"));
		Assert.False(full.Holds(state, combat, combat.Attacker));
		combat.Attacker.Front.Add(Division(state, germany, Berlin, width: "1"));
		Assert.True(full.Holds(state, combat, combat.Attacker));

		combat.Attacker.Reserves.Add(Division(state, germany, Berlin));
		Assert.True(reserves.Holds(state, combat, combat.Attacker));
		Assert.False(new CCombatCondition(ECombatCondition.Reserves, wanted: false).Holds(state, combat, combat.Attacker));
	}

	[Fact]
	public void Armour_counts_only_on_the_front()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CCombat combat = Battle(state, germany, poland);
		var armour = new CCombatCondition(ECombatCondition.HasArmourUnit);
		CUnit tanks = Division(state, germany, Berlin, "armor_brigade", armour: true);

		combat.Attacker.Reserves.Add(tanks);
		Assert.False(armour.Holds(state, combat, combat.Attacker));

		combat.Attacker.Reserves.Remove(tanks);
		combat.Attacker.Front.Add(tanks);
		Assert.True(armour.Holds(state, combat, combat.Attacker));
		Assert.False(new CCombatCondition(ECombatCondition.HasArmourUnit, wanted: false).Holds(state, combat, combat.Attacker));
	}

	[Fact]
	public void With_no_leaders_a_test_of_skill_holds_only_where_it_asks_for_none()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CCombat combat = Battle(state, germany, poland);

		Assert.True(new CCombatCondition(ECombatCondition.Skill, amount: 0).Holds(state, combat, combat.Attacker));
		Assert.False(new CCombatCondition(ECombatCondition.Skill, amount: 3).Holds(state, combat, combat.Attacker));
		Assert.False(new CCombatCondition(ECombatCondition.SkillAdvantage, amount: 1).Holds(state, combat, combat.Attacker));
		Assert.False(new CCombatCondition(ECombatCondition.Trait, name: "panzer_leader").Holds(state, combat, combat.Attacker));
		Assert.False(new CCombatCondition(ECombatCondition.Never).Holds(state, combat, combat.Attacker));
	}

	[Fact]
	public void A_group_of_conditions_is_all_any_or_none_of_them()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CCombat combat = Battle(state, germany, poland);
		var any = new CCombatCondition(ECombatCondition.Any);
		any.Children.Add(new CCombatCondition(ECombatCondition.Skill, amount: 3));
		any.Children.Add(new CCombatCondition(ECombatCondition.IsAttacker));
		var none = new CCombatCondition(ECombatCondition.None);
		none.Children.Add(new CCombatCondition(ECombatCondition.Reserves));

		Assert.True(any.Holds(state, combat, combat.Attacker));
		Assert.False(any.Holds(state, combat, combat.Defender));
		Assert.True(none.Holds(state, combat, combat.Attacker));
	}

	[Fact]
	public void A_name_that_is_no_tactic_counters_nothing_and_is_reported()
	{
		var warnings = new List<string>();
		CCombatTactic assault = Tactic(1, "tactic_assault", attacker: true, counteredBy: "tactic_nonsense");

		CCombatTactics.Link(new[] { assault }, warnings.Add);

		Assert.Equal(0, assault.CounteredByNumber);
		Assert.Contains(warnings, warning => warning.Contains("tactic_nonsense"));
	}
}
