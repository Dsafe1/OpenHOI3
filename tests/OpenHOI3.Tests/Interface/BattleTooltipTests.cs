using System.Collections.Generic;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What a battle's window and its row in a unit's panel say in a tooltip, against
/// <c>docs/mechanics/combatview.md</c>, section 12, with Their Finest Hour's strings where the
/// test needs their shape.
/// </summary>
public class BattleTooltipTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Posen = 2;

	private static readonly Dictionary<string, string> Texts = new()
	{
		["CLOSE"] = "Close",
		["COMBAT_RETREAT"] = "Retreat all your units from combat.",
		["NONE_COUNTERED"] = "Neither countered.",
		["DEFENDER_COUNTERED"] = "The defender was countered.",
		["COMEV_COMBATWIDTH"] = "Width of Front",
		["COMEV_DAMAGE_ATTACKER"] = "Attacker Damage Bonus",
		["COMEV_DAMAGE_DEFENDER"] = "Defender Damage Bonus",
		["COMEV_MOVEMENTSPEED"] = "Attacker Speed",
		["TACTIC_COUNTERED_BY"] = "Countered by: §Y$NAME$§!\n",
		["TACTIC_COUNTERS"] = "Counters:",
		["tactic_assault"] = "Assault",
		["tactic_delay"] = "Delay",
		["tactic_ambush"] = "Ambush",
		["tactic_breakthrough"] = "Breakthrough",
		["BM_DIG_IN"] = "Dug in",
		["BM_DUGIN_MODIFIER"] = "Dug In",
		["BM_FORT_MODIFIER"] = "Forts",
		["LEADER_COMMAND_INFO"] = "Led by §Y$NAME$§W (Skill: §Y$SKILL$§W)\n",
		["NO_LEADER_FOR_THIS_1"] = "This division lacks a leader.",
		["NO_LEADER_FOR_THIS_2"] = "A single brigade cannot have a general.",
		["BATTLE_ATTACKMOD"] = "Attack Modifier",
		["BATTLE_DEFENDMOD"] = "Defend Modifier",
		["AP_ATTACK"] = "Piercing Attack",
		["ARMOR_VALUE"] = "Armor",
		["CURR_COMB_STR"] = "Current Strength at ",
		["COMB_TT_VS"] = "§RVERSUS§W",
		["AND"] = "and ",
		["UNIT_ATTACK_INFO_ARMY"] = "With $BRIG$ brigades in $UNIT$ units, $MEN$ soldiers.",
		["UNIT_ATTACK_INFO_NAVY"] = "With $CAP$ capital ships, $SCR$ screens, $SUB$ submarines & $TRA$ transports.",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static CGameState State(out CCountry germany, params CCombatTactic[] tactics)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		return new CGameState(new CDate(1936, 1, 1), new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
			new CProvince(Posen, "Posen", isSea: false) { Owner = Pol, Controller = Pol },
		}, new[] { germany, poland }, wars: new[] { war })
		{
			Tactics = tactics,
		};
	}

	private static CUnit Unit(CCountryTag owner, EUnitKind kind, string name, params string[] types)
	{
		var unit = new CUnit(kind, owner) { Name = name, Location = Berlin };
		int id = 1;
		foreach (string key in types)
		{
			var type = new CSubUnitType(key, kind == EUnitKind.Navy ? EUnitBranch.Naval : EUnitBranch.Land);
			type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(10));
			var subUnit = new CSubUnit(type) { Unit = unit, Id = id++, Name = key, Strength = CFixedPoint.FromInt(10) };
			subUnit.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(subUnit);
		}
		return unit;
	}

	// ---- the window

	[Fact]
	public void TheWindowsOwnElementsSayTheirLine()
	{
		CGameState state = State(out _);
		var combat = new CCombat(Posen);

		Assert.Equal("Close", CBattleTooltips.Window(Localise, state, combat, "closebutton").Text);
		Assert.Equal("Retreat all your units from combat.", CBattleTooltips.Window(Localise, state, combat, "retreat_button").Text);
		Assert.Equal("Neither countered.", CBattleTooltips.Window(Localise, state, combat, "combat_attack_indicator").Text);

		var assault = new CCombatTactic("tactic_assault", 1);
		var delay = new CCombatTactic("tactic_delay", 2) { CounteredByNumber = 1 };
		combat.Attacker.Tactic = assault;
		combat.Defender.Tactic = delay;
		Assert.Equal("The defender was countered.", CBattleTooltips.Window(Localise, state, combat, "combat_attack_indicator").Text);
		Assert.Equal("", CBattleTooltips.Window(Localise, state, new CCombat(Posen, ECombatKind.Naval), "combat_attack_indicator").Text);
	}

	[Fact]
	public void ATacticSaysItsFiguresWhatCountersItAndWhatItCounters()
	{
		var assault = new CCombatTactic("tactic_assault", 1)
		{
			Width = CFixedPoint.FromInt(2), Attacker = CFixedPoint.Parse("0.1"), Defender = CFixedPoint.Parse("-0.05"),
			CounteredByNumber = 3,
		};
		var delay = new CCombatTactic("tactic_delay", 2) { CounteredByNumber = 1 };
		var ambush = new CCombatTactic("tactic_ambush", 3);
		var breakthrough = new CCombatTactic("tactic_breakthrough", 4) { CounteredByNumber = 1 };
		CGameState state = State(out _, assault, delay, ambush, breakthrough);

		Assert.Equal(
			"Width of Front: §G+2§W\n"
			+ "Attacker Damage Bonus: §G+10%§W\n"
			+ "Defender Damage Bonus: §R-5%§W\n"
			+ "\n"
			+ "Countered by: §YAmbush§!\n"
			+ "Counters:\n§Y  Delay§!\n§Y  Breakthrough§!\n",
			CBattleTooltips.Tactic(Localise, state, assault));
		// One countered tactic follows the heading on its line.
		Assert.Equal("\nCounters:§Y  Assault§!\n", CBattleTooltips.Tactic(Localise, state, ambush));
	}

	[Fact]
	public void ASideFeelsOnlyItsOwnHalfOfATactic()
	{
		var assault = new CCombatTactic("tactic_assault", 1)
		{
			Attacker = CFixedPoint.Parse("0.1"), Defender = CFixedPoint.Parse("0.2"), Speed = CFixedPoint.Parse("0.3"),
		};

		Assert.Equal("Attacker Damage Bonus: §G+10%§W\nAttacker Speed: §G+30%§W\n",
			CBattleTooltips.TacticFigures(Localise, assault, true, true, false));
		Assert.Equal("Defender Damage Bonus: §G+20%§W\n", CBattleTooltips.TacticFigures(Localise, assault, true, false, true));
	}

	[Fact]
	public void AModifierWritesOneFigureOrTheAttacksAndTheDefences()
	{
		Assert.Equal("Dug In: §G+25.00§W%\nForts: §R-10.00§W%/§G+5.00§W%\n",
			CBattleTooltips.Modifiers(Localise, new[]
			{
				new CCombatModifier(ECombatModifier.DugIn, CFixedPoint.Parse("0.25"), CFixedPoint.Parse("0.25")),
				new CCombatModifier(ECombatModifier.Fort, CFixedPoint.Parse("-0.1"), CFixedPoint.Parse("0.05")),
			}));
		Assert.Equal("§R0.00", CBattleTooltips.ModifierFigure(CFixedPoint.Zero));
	}

	// ---- a land unit

	[Fact]
	public void ALandUnitSaysItsLeaderAttackDefenceModifiersPiercingAndArmour()
	{
		CGameState state = State(out _);
		var combat = new CCombat(Posen);
		CUnit division = Unit(Ger, EUnitKind.Division, "1. Inf", "infantry_brigade", "infantry_brigade");
		division.SubUnits[1].Stats.Values.Set(CUnitTotals.Piercing, CFixedPoint.FromInt(3));
		division.SubUnits[0].Stats.Values.Set(CUnitTotals.Armour, CFixedPoint.Parse("1.5"));
		division.CombatAttack = CFixedPoint.Parse("1.25");
		division.CombatDefence = CFixedPoint.Parse("0.9");
		division.CombatModifiers.Add(new CCombatModifier(ECombatModifier.DugIn, CFixedPoint.Parse("0.1"), CFixedPoint.Parse("0.1")));
		combat.Attacker.Add(division);

		Assert.Equal(
			"This division lacks a leader.\n"
			+ "Attack Modifier: §Y125.00§!%\n"
			+ "Defend Modifier: §Y90.00§!%\n"
			+ "Dug In: §G+10.00§W%\n"
			+ "Piercing Attack: §Y3§!\n"
			+ "Armor: §Y1.5§!\n",
			CBattleUnitTooltips.Unit(Localise, state, combat, division, "name").Text);

		var leader = new CLeader(1) { Name = "Guderian", Skill = 4, Rank = 1 };
		division.Leader = leader;
		Assert.StartsWith("Led by §YGuderian§W (Skill: §Y4§W)\nAttack", CBattleUnitTooltips.Unit(Localise, state, combat, division, "name").Text);

		division.SubUnits[0].Strength = CFixedPoint.FromInt(5);
		Assert.Equal("Current Strength at §Y75.0%§W\n", CBattleUnitTooltips.Unit(Localise, state, combat, division, "str_bar").Text);
	}

	[Fact]
	public void AShipWithNoTargetWritesTwoSpaces()
	{
		CGameState state = State(out _);
		var combat = new CCombat(Posen, ECombatKind.Naval);
		CUnit fleet = Unit(Ger, EUnitKind.Navy, "1. Flotte", "destroyer");
		CUnit enemy = Unit(Pol, EUnitKind.Navy, "Flota", "destroyer");
		enemy.SubUnits[0].Id = 99;
		enemy.SubUnits[0].Name = "Burza";
		combat.Attacker.Add(fleet);
		combat.Defender.Add(enemy);
		CSubUnit ship = fleet.SubUnits[0];
		ship.Distance = CFixedPoint.FromInt(12);

		Assert.StartsWith("==><>  (12km)\n", CBattleUnitTooltips.Ship(Localise, state, combat, ship, "name").Text);
		ship.TargetId = 99;
		Assert.StartsWith("==>§GBurza§W (12km)\n", CBattleUnitTooltips.Ship(Localise, state, combat, ship, "name").Text);
	}

	// ---- a battle's row in a unit's panel

	[Fact]
	public void ASidesNamesAreJoinedAndOneFleetsShipsRunTogether()
	{
		var combat = new CCombat(Posen);
		combat.Attacker.Add(Unit(Ger, EUnitKind.Division, "1. Inf", "infantry_brigade"));
		combat.Attacker.Add(Unit(Ger, EUnitKind.Army, "8. Armee", "hq_brigade"));
		combat.Attacker.Add(Unit(Ger, EUnitKind.Division, "3. Inf", "infantry_brigade"));

		Assert.Equal("@GER £infantry_brigade 1. Inf, @GER £ 8. Armee and  @GER £infantry_brigade 3. Inf",
			CBattleRowTooltip.Names(Localise, combat.Attacker));

		var sea = new CCombat(Posen, ECombatKind.Naval);
		sea.Attacker.Add(Unit(Ger, EUnitKind.Navy, "1. Flotte", "heavy_cruiser", "destroyer"));
		Assert.Equal("@GER £heavy_cruiser 1. Flotte@GER £destroyer 1. Flotte", CBattleRowTooltip.Names(Localise, sea.Attacker));
	}

	[Fact]
	public void TheRowSaysBothSidesWithVersusBetween()
	{
		var combat = new CCombat(Posen);
		combat.Attacker.Add(Unit(Ger, EUnitKind.Division, "1. Inf", "infantry_brigade", "infantry_brigade"));
		combat.Defender.Add(Unit(Pol, EUnitKind.Division, "7. DP", "infantry_brigade"));

		Assert.Equal(
			"@GER £infantry_brigade 1. Inf\nWith 2 brigades in 1 units, 2,000 soldiers."
			+ "\n§RVERSUS§W\n"
			+ "@POL £infantry_brigade 7. DP\nWith 1 brigades in 1 units, 1,000 soldiers.",
			CBattleRowTooltip.For(Localise, combat).Text);
	}
}
