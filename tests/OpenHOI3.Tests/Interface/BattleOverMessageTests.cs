using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Messages;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The five messages of a battle that is over, against <c>docs/mechanics/messages.md</c>, sections 9.1 and 9.2: who is
/// told, which message, and what each carries.
/// </summary>
public class BattleOverMessageTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Eng = new("ENG");

	private const int Poznan = 1;

	private static readonly CBuilding Guns = new(0, "anti_air", 10, CBombingOrders.AntiAir, CFixedPoint.One);
	private static readonly CBuilding Factory = new(1, "industry", 10, CBombingOrders.Industry, CFixedPoint.One);

	private static readonly Dictionary<string, string> Table = new()
	{
		["YOU_WON"] = "You Won",
		["YOU_LOST"] = "You Lost",
		["BATTLE_OF"] = "Battle of",
		["RESULT_WON"] = "won",
		["RESULT_LOSS"] = "lost",
		["COMBAT_FLEE"] = "fleeing",
		["COMBAT_TRIUMPH"] = "triumphant",
		["COMBAT_WINNERTYPE_OUR"] = "our",
		["COMBAT_WINNERTYPE_THEIR"] = "their",
		["NONE"] = "None",
		["anti_air"] = "Anti-Aircraft Guns",
		["industry"] = "Industrial Capacity",
		["SUPPLIES_IRO"] = "Supplies",
		["METAL_IRO"] = "Metal",
		["BOMB_DAMAGE_BUILDING"] = "$NAME$ damaged by $SIZE$",
		["BOMB_DAMAGE_STOCKPILE"] = "$SIZE$ $NAME$ destroyed",
		["BOMB_DAMAGE_PRODUCTION"] = "$NAME$ production down $SIZE$",
		["BOMB_DAMAGE_MEN"] = "$SIZE$ soldiers killed",
		["BOMB_SINK_SHIP"] = "Sunk:$SUNK$",
		["BOMB_DAMAGING_SHIPS"] = "Damaging ships",
	};

	private static string Localise(string key) => Table.GetValueOrDefault(key, key);

	private static CMessageTypes Types()
	{
		return CMessageTypes.Read(ClausewitzParser.Parse(string.Concat(
			new[] { "LANDBATTLEOVER", "AIRBATTLEOVER", "NAVALBATTLEOVER", "BOMBENDTHEM", "BOMBENDUS" }
				.Select(name => name + " = { log = yes onmap = yes popup = yes option = yes categories = \"cat_combat\" }\n"))), _ => { });
	}

	private static CGameState State()
	{
		return new CGameState(new CDate(1936, 1, 4),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(Poznan, "Poznan", isSea: false) { Owner = Pol, Controller = Pol },
			},
			new[]
			{
				new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Adjective = "German" },
				new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" },
				new CCountry { Tag = Eng, Name = "United Kingdom", DefinitionFile = "" },
			}, buildings: new[] { Guns, Factory });
	}

	private static CUnit Unit(CGameState state, CCountryTag owner, EUnitKind kind, EUnitBranch branch, string type, string strength = "75")
	{
		var unit = new CUnit(kind, owner) { Id = ++state.LastUnitId, Location = Poznan, Name = "Unit" };
		var subUnitType = new CSubUnitType(type, branch);
		var subUnit = new CSubUnit(subUnitType)
		{
			Id = ++state.LastUnitId, Unit = unit, Name = "the " + type, Strength = CFixedPoint.Parse(strength),
		};
		unit.SubUnits.Add(subUnit);
		state.Country(owner)!.Units.Add(unit);
		return unit;
	}

	private static CCombat Battle(CGameState state, ECombatKind kind)
	{
		EUnitKind unitKind = kind switch { ECombatKind.Land => EUnitKind.Division, ECombatKind.Naval => EUnitKind.Navy, _ => EUnitKind.Air };
		EUnitBranch branch = kind switch { ECombatKind.Land => EUnitBranch.Land, ECombatKind.Naval => EUnitBranch.Naval, _ => EUnitBranch.Air };
		string type = kind switch { ECombatKind.Land => "infantry_brigade", ECombatKind.Naval => "destroyer", _ => "interceptor" };
		var battle = new CCombat(Poznan, kind);
		battle.Attacker.Add(Unit(state, Ger, unitKind, branch, type));
		battle.Defender.Add(Unit(state, Pol, unitKind, branch, type, "50"));
		return battle;
	}

	private static string Value(CMessage message, string key) => message.Variables.Last(v => v.Key == key).Value;

	[Fact]
	public void A_land_battle_tells_the_player_on_either_side_his_and_their_losses_and_men()
	{
		CGameState state = State();
		CCombat battle = Battle(state, ECombatKind.Land);
		battle.Attacker.Losses = CFixedPoint.Parse("312.9");
		battle.Defender.Losses = CFixedPoint.Parse("1200.4");

		CMessage won = CBattleOverMessages.BattleOver(Localise, Types(), state, Ger, battle, attackerLost: false)!;
		Assert.Equal("LANDBATTLEOVER", won.Type.Name);
		Assert.Equal((Ger, Pol, Poznan), (won.FirstCountry, won.SecondCountry, won.Province));
		Assert.Equal("You Won", Value(won, "WINORLOSE"));
		Assert.Equal("Battle of Poznan", Value(won, "NAME"));
		Assert.Equal(("312", "7500", "1200", "5000"),
			(Value(won, "USLOSS"), Value(won, "USNUM"), Value(won, "THEIRLOST"), Value(won, "THEIRNUM")));
		Assert.Equal(("won", "Poland", "lost"), (Value(won, "RESULT"), Value(won, "OTHER"), Value(won, "OTHERRESULT")));
		Assert.Equal("WE: §Y0.00", Value(won, "OUREXHAUSTION"));
		Assert.Equal(Value(won, "OUREXHAUSTION"), Value(won, "ENEMYEXHAUSTION"));
		Assert.Equal("MESSAGE_HEAD_MARS", Value(won, "MESSENGER"));

		CMessage lost = CBattleOverMessages.BattleOver(Localise, Types(), state, Pol, battle, attackerLost: false)!;
		Assert.Equal((Pol, Pol), (lost.FirstCountry, lost.SecondCountry));
		Assert.Equal("You Lost", Value(lost, "WINORLOSE"));
		Assert.Equal(("1200", "5000", "312", "7500"),
			(Value(lost, "USLOSS"), Value(lost, "USNUM"), Value(lost, "THEIRLOST"), Value(lost, "THEIRNUM")));
		Assert.Equal(("lost", "Germany", "won"), (Value(lost, "RESULT"), Value(lost, "OTHER"), Value(lost, "OTHERRESULT")));
	}

	[Fact]
	public void Nobody_else_is_told_and_nothing_is_said_of_a_battle_with_a_side_nobody_stands_on()
	{
		CGameState state = State();
		CCombat battle = Battle(state, ECombatKind.Land);

		Assert.Null(CBattleOverMessages.BattleOver(Localise, Types(), state, Eng, battle, attackerLost: true));
		Assert.Null(CBattleOverMessages.BattleOver(Localise, Types(), state, null, battle, attackerLost: true));
		Assert.Null(CBattleOverMessages.BattleOver(Localise, Types(), state, Ger, new CCombat(Poznan, ECombatKind.GroundBombing), attackerLost: true));

		battle.Defender.Standing.Clear();
		Assert.Null(CBattleOverMessages.BattleOver(Localise, Types(), state, Ger, battle, attackerLost: false));
	}

	[Fact]
	public void An_air_battle_is_a_land_battles_message_without_the_exhaustion()
	{
		CGameState state = State();
		CCombat battle = Battle(state, ECombatKind.Air);
		battle.Attacker.Losses = CFixedPoint.Parse("12.5");

		CMessage message = CBattleOverMessages.BattleOver(Localise, Types(), state, Ger, battle, attackerLost: true)!;
		Assert.Equal("AIRBATTLEOVER", message.Type.Name);
		Assert.DoesNotContain(message.Variables, v => v.Key is "OUREXHAUSTION" or "ENEMYEXHAUSTION");
		Assert.Equal(("You Lost", "lost", "won"), (Value(message, "WINORLOSE"), Value(message, "RESULT"), Value(message, "OTHERRESULT")));
		Assert.Equal(("12", "1", "0", "1"),
			(Value(message, "USLOSS"), Value(message, "USNUM"), Value(message, "THEIRLOST"), Value(message, "THEIRNUM")));
	}

	[Fact]
	public void A_sea_battle_names_the_ships_each_side_lost_and_puts_the_attacker_first()
	{
		CGameState state = State();
		CCombat battle = Battle(state, ECombatKind.Naval);
		CSubUnit sunk = battle.Defender.Units[0].SubUnits[0];
		battle.Defender.Sunk.Add(sunk);
		state.SunkShips.Add(new CSunkShip(Pol, sunk));

		CMessage message = CBattleOverMessages.BattleOver(Localise, Types(), state, Ger, battle, attackerLost: false)!;
		Assert.Equal("NAVALBATTLEOVER", message.Type.Name);
		Assert.Equal((Ger, Pol), (message.FirstCountry, message.SecondCountry));
		Assert.Equal(("won", "Poland", "fleeing", "our"),
			(Value(message, "RESULT"), Value(message, "OTHER"), Value(message, "OTHERRESULT"), Value(message, "WINNER")));
		Assert.Equal("None\n\n", Value(message, "SHIPS"));
		Assert.Equal("\n@POL £destroyer the destroyer", Value(message, "THEIRSHIP"));
		Assert.DoesNotContain(message.Variables, v => v.Key is "USLOSS" or "THEIRNUM");

		CMessage defender = CBattleOverMessages.BattleOver(Localise, Types(), state, Pol, battle, attackerLost: false)!;
		Assert.Equal((Ger, Pol), (defender.FirstCountry, defender.SecondCountry));
		Assert.Equal(("lost", "Germany", "triumphant", "their"),
			(Value(defender, "RESULT"), Value(defender, "OTHER"), Value(defender, "OTHERRESULT"), Value(defender, "WINNER")));
		Assert.Equal("\n@POL £destroyer the destroyer\n\n", Value(defender, "SHIPS"));
		Assert.Equal("None", Value(defender, "THEIRSHIP"));
	}

	private static CCombat Bombing(CGameState state, EUnitOrder order, ECombatKind kind = ECombatKind.GroundBombing)
	{
		CUnit bombers = Unit(state, Ger, EUnitKind.Air, EUnitBranch.Air, "tactical_bomber");
		bombers.Order = order;
		var bombing = new CCombat(Poznan, kind);
		bombing.Attacker.Add(bombers);
		bombing.Defender.AddCountry(Pol);
		return bombing;
	}

	[Fact]
	public void A_bombing_that_ends_tells_the_bomber_one_message_and_the_bombed_the_other_with_the_bombers_flags()
	{
		CGameState state = State();
		CCombat bombing = Bombing(state, EUnitOrder.StrategicBombing);

		CMessage ours = CBattleOverMessages.BombingEnded(Localise, Types(), state, Ger, bombing)!;
		Assert.Equal("BOMBENDTHEM", ours.Type.Name);
		Assert.Equal((Ger, Ger, Poznan), (ours.FirstCountry, ours.SecondCountry, ours.Province));
		Assert.Equal(("Poznan", "Germany", "German"), (Value(ours, "PROV"), Value(ours, "ATTACKER"), Value(ours, "ATTACKER_ADJ")));
		Assert.Equal("None", Value(ours, "DAMAGE"));
		Assert.Equal(Value(ours, "DAMAGE"), Value(ours, "DAMAGE_SHORT"));

		CMessage theirs = CBattleOverMessages.BombingEnded(Localise, Types(), state, Pol, bombing)!;
		Assert.Equal("BOMBENDUS", theirs.Type.Name);
		Assert.Equal((Ger, Ger), (theirs.FirstCountry, theirs.SecondCountry));

		Assert.Null(CBattleOverMessages.BombingEnded(Localise, Types(), state, Eng, bombing));
		bombing.Attacker.Units[0].Order = EUnitOrder.None;
		Assert.Null(CBattleOverMessages.BombingEnded(Localise, Types(), state, Ger, bombing));
	}

	[Fact]
	public void The_damage_lists_buildings_in_the_files_order_then_the_stockpile_then_production()
	{
		CGameState state = State();
		CCombat bombing = Bombing(state, EUnitOrder.StrategicBombing);
		bombing.Attacker.BuildingsDestroyed["industry"] = CFixedPoint.Parse("1.25");
		bombing.Attacker.BuildingsDestroyed["anti_air"] = CFixedPoint.Parse("2");
		bombing.Attacker.StockpileDestroyed[EGoods.Supplies] = CFixedPoint.Parse("14.0309");
		bombing.Attacker.ProductionDestroyed[EGoods.Metal] = CFixedPoint.Parse("3");
		bombing.Attacker.StrengthDealt = CFixedPoint.Parse("46.039");

		Assert.Equal("Anti-Aircraft Guns damaged by 2.000\nIndustrial Capacity damaged by 1.250\n"
			+ "14.030 Supplies destroyed\nMetal production down 3.000\n", CBattleOverMessages.Damage(Localise, state, bombing));
	}

	[Fact]
	public void A_ground_attack_ends_with_the_strength_dealt_in_whole_points_and_under_a_tenth_with_nothing()
	{
		CGameState state = State();
		CCombat bombing = Bombing(state, EUnitOrder.GroundAttack, ECombatKind.LandBombing);
		bombing.Attacker.StrengthDealt = CFixedPoint.Parse("46.039");
		Assert.Equal("46 soldiers killed", CBattleOverMessages.Damage(Localise, state, bombing));

		bombing.Attacker.StrengthDealt = CFixedPoint.Parse("0.099");
		Assert.Equal("None", CBattleOverMessages.Damage(Localise, state, bombing));
	}

	[Fact]
	public void A_strike_at_ships_names_those_it_sank_and_else_says_it_damaged_some()
	{
		CGameState state = State();
		CCombat bombing = Bombing(state, EUnitOrder.NavalStrike, ECombatKind.NavalBombing);
		Assert.Equal("None", CBattleOverMessages.Damage(Localise, state, bombing));

		bombing.Attacker.StrengthDealt = CFixedPoint.Parse("3");
		Assert.Equal("Damaging ships", CBattleOverMessages.Damage(Localise, state, bombing));

		CSubUnit ship = Unit(state, Pol, EUnitKind.Navy, EUnitBranch.Naval, "destroyer").SubUnits[0];
		bombing.Defender.Sunk.Add(ship);
		state.SunkShips.Add(new CSunkShip(Pol, ship));
		Assert.Equal("Sunk:\n@POL £destroyer the destroyer", CBattleOverMessages.Damage(Localise, state, bombing));
	}
}
