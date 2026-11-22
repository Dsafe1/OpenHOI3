using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHOI3.Content.Messages;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Messages;

/// <summary>
/// The five messages of a battle that is over (<c>docs/mechanics/messages.md</c>, sections 9.1 and 9.2):
/// <c>LANDBATTLEOVER</c>, <c>AIRBATTLEOVER</c> and <c>NAVALBATTLEOVER</c> as a battle is decided, and
/// <c>BOMBENDTHEM</c> and <c>BOMBENDUS</c> as a bombing ends. Each is the player's alone, where he is on one of the
/// sides, and whether it pops up, is logged or stands on the map is its type's in <c>messagetypes.txt</c>.
/// </summary>
public static class CBattleOverMessages
{
	/// <summary>The land message's two leftovers: the same text whatever happened, as the original's.</summary>
	private const string Exhaustion = "WE: §Y0.00";

	/// <summary>
	/// A battle decided, for the player on one of its sides: nothing for a bombing, for a side with no country left
	/// in the fight, or for a player on neither.
	/// </summary>
	public static CMessage? BattleOver(Func<string, string> localise, CMessageTypes types, CGameState state, CCountryTag? player,
		CCombat combat, bool attackerLost)
	{
		if (player is not { } own || state.Country(own) is not { } country
			|| combat.Kind is not (ECombatKind.Land or ECombatKind.Air or ECombatKind.Naval)
			|| combat.Attacker.Standing.Count == 0 || combat.Defender.Standing.Count == 0)
		{
			return null;
		}
		bool attacking = combat.Attacker.Standing.Contains(own);
		bool defending = combat.Defender.Standing.Contains(own);
		if (!attacking && !defending)
		{
			return null;
		}
		// Won or lost is the defender's where he is on both sides; what is his and theirs the attacker's, asked first.
		bool lost = defending ? !attackerLost : attackerLost;
		CCombatSide ours = attacking ? combat.Attacker : combat.Defender;
		CCombatSide theirs = attacking ? combat.Defender : combat.Attacker;
		bool oursWon = attacking ? !attackerLost : attackerLost;
		bool naval = combat.Kind == ECombatKind.Naval;

		string name = combat.Kind switch { ECombatKind.Land => "LANDBATTLEOVER", ECombatKind.Air => "AIRBATTLEOVER", _ => "NAVALBATTLEOVER" };
		// At sea the first country is the attacking side's and the second the defending side's; on land and in the air
		// the player is first and the defender's first country second, whichever side he is on.
		CCountryTag first = naval && !attacking ? combat.Attacker.Standing[0] : own;
		CCountryTag second = naval && !attacking ? own : combat.Defender.Standing[0];
		var message = new CMessage(types.Find(name), CMessageText.Date(localise, state.Date, state.Hour))
		{
			FirstCountry = first,
			SecondCountry = second,
			Province = combat.Province,
		};
		Standing(localise, state, country, message);
		if (combat.Kind == ECombatKind.Land)
		{
			message.Variables.Add(("OUREXHAUSTION", Exhaustion));
			message.Variables.Add(("ENEMYEXHAUSTION", Exhaustion));
		}
		message.Variables.Add(("WINORLOSE", localise(lost ? "YOU_LOST" : "YOU_WON")));
		message.Variables.Add(("MESSENGER", "MESSAGE_HEAD_MARS"));
		message.Variables.Add(("NAME", localise("BATTLE_OF") + " " + state.Province(combat.Province).Name));
		string others = CBattleRowTooltip.CountryNames(localise, state, theirs);
		if (naval)
		{
			message.Variables.Add(("RESULT", localise(oursWon ? "RESULT_WON" : "RESULT_LOSS")));
			message.Variables.Add(("OTHER", others));
			message.Variables.Add(("OTHERRESULT", localise(oursWon ? "COMBAT_FLEE" : "COMBAT_TRIUMPH")));
			message.Variables.Add(("SHIPS", SunkShips(localise, state, ours) + "\n\n"));
			message.Variables.Add(("THEIRSHIP", SunkShips(localise, state, theirs)));
			message.Variables.Add(("WINNER", localise(oursWon ? "COMBAT_WINNERTYPE_OUR" : "COMBAT_WINNERTYPE_THEIR")));
			return message;
		}
		message.Variables.Add(("USLOSS", Whole(Losses(ours))));
		message.Variables.Add(("THEIRLOST", Whole(Losses(theirs))));
		message.Variables.Add(("USNUM", Whole(Men(ours))));
		message.Variables.Add(("THEIRNUM", Whole(Men(theirs))));
		message.Variables.Add(("OTHER", others));
		message.Variables.Add(("RESULT", localise(oursWon ? "RESULT_WON" : "RESULT_LOSS")));
		message.Variables.Add(("OTHERRESULT", localise(oursWon ? "RESULT_LOSS" : "RESULT_WON")));
		return message;
	}

	/// <summary>
	/// A bombing ended, for the player among the bombers (<c>BOMBENDTHEM</c>) or, failing that, among the bombed
	/// (<c>BOMBENDUS</c>): nothing where the bombers have no country or no unit left, or their first unit has no order.
	/// It is told whatever was hit - nothing damaged reads "None".
	/// </summary>
	public static CMessage? BombingEnded(Func<string, string> localise, CMessageTypes types, CGameState state, CCountryTag? player,
		CCombat bombing)
	{
		CCombatSide bombers = bombing.Attacker;
		if (player is not { } own || state.Country(own) is not { } country || bombing.IsConvoyBattle
			|| bombers.Standing.Count == 0 || bombers.Units.Count == 0 || bombers.Units[0].Order == EUnitOrder.None)
		{
			return null;
		}
		string name;
		if (bombers.Standing.Contains(own))
		{
			name = "BOMBENDTHEM";
		}
		else if (bombing.Defender.Standing.Contains(own))
		{
			name = "BOMBENDUS";
		}
		else
		{
			return null;
		}
		// Both flags are the bombers' first country's, on the bombed player's message too.
		CCountryTag attacker = bombers.Standing[0];
		var message = new CMessage(types.Find(name), CMessageText.Date(localise, state.Date, state.Hour))
		{
			FirstCountry = attacker,
			SecondCountry = attacker,
			Province = bombing.Province,
		};
		Standing(localise, state, country, message);
		CCountry? bomber = state.Country(attacker);
		message.Variables.Add(("PROV", state.Province(bombing.Province).Name));
		message.Variables.Add(("ATTACKER", bomber?.Name ?? attacker.Value));
		message.Variables.Add(("ATTACKER_ADJ", bomber is { Adjective.Length: > 0 } ? bomber.Adjective : bomber?.Name ?? attacker.Value));
		message.Variables.Add(("ORDER", COrdersText.NameKey(bombers.Units[0].Order)));
		string damage = Damage(localise, state, bombing);
		message.Variables.Add(("DAMAGE", damage));
		// The original builds a one-line copy for this and then gives the whole text again: kept (enhanced.md).
		message.Variables.Add(("DAMAGE_SHORT", damage));
		return message;
	}

	/// <summary>
	/// What a bombing did, a line each: the buildings in the building file's order, what was taken from the stockpile
	/// and from production good by good, and last one line by the first bomber unit's order - the soldiers killed for
	/// a ground attack or an interdiction, the ships sunk or "damaging ships" for a strike at ships. Under three
	/// characters, it is the text of <c>NONE</c>.
	/// </summary>
	public static string Damage(Func<string, string> localise, CGameState state, CCombat bombing)
	{
		CCombatSide bombers = bombing.Attacker;
		var text = new StringBuilder();
		foreach (CBuilding building in state.Buildings)
		{
			if (bombers.BuildingsDestroyed.TryGetValue(building.Key, out CFixedPoint levels) && levels.Raw != 0)
			{
				text.Append(CTextTemplate.Fill(localise("BOMB_DAMAGE_BUILDING"), ("NAME", localise(building.Key)),
					("SIZE", CNumberText.Format(levels, 3)))).Append('\n');
			}
		}
		foreach ((string key, CGoodsPool pool) in new[] { ("BOMB_DAMAGE_STOCKPILE", bombers.StockpileDestroyed), ("BOMB_DAMAGE_PRODUCTION", bombers.ProductionDestroyed) })
		{
			foreach (EGoods goods in Enum.GetValues<EGoods>())
			{
				if (pool[goods].Raw != 0)
				{
					text.Append(CTextTemplate.Fill(localise(key), ("NAME", localise(CTradeWindowText.GoodsName(goods))),
						("SIZE", CNumberText.Format(pool[goods], 3)))).Append('\n');
				}
			}
		}
		// A hundredth of the strength the bombs dealt, cut: under a tenth of a point it is nothing.
		long dealt = bombers.StrengthDealt.Raw * 10 / CFixedPoint.Scale;
		EUnitOrder order = bombers.Units.Count > 0 ? bombers.Units[0].Order : EUnitOrder.None;
		if (dealt != 0 && order is EUnitOrder.GroundAttack or EUnitOrder.Interdiction)
		{
			text.Append(CTextTemplate.Fill(localise("BOMB_DAMAGE_MEN"), ("SIZE", CBuilderText.Men(new CFixedPoint(dealt)))));
		}
		else if (order is EUnitOrder.NavalStrike or EUnitOrder.PortStrike or EUnitOrder.CarrierProtection)
		{
			// The ships the bombs finished are kept on the bombed side here, where the original keeps them on the bombers'.
			if (bombing.Defender.Sunk.Count > 0)
			{
				text.Append(CTextTemplate.Fill(localise("BOMB_SINK_SHIP"), ("SUNK", Ships(state, bombing.Defender.Sunk))));
			}
			else if (dealt != 0)
			{
				text.Append(localise("BOMB_DAMAGING_SHIPS"));
			}
		}
		return text.Length < 3 ? localise("NONE") : text.ToString();
	}

	/// <summary>A side's sunk ships for a sea battle's message: "None", or a line a ship.</summary>
	private static string SunkShips(Func<string, string> localise, CGameState state, CCombatSide side)
	{
		return side.Sunk.Count == 0 ? localise("NONE") : Ships(state, side.Sunk);
	}

	/// <summary>
	/// Ships sunk, a line each and a line feed before each: the owner's flag, the type's counter picture and the ship's
	/// name.
	/// </summary>
	private static string Ships(CGameState state, IEnumerable<CSubUnit> ships)
	{
		var text = new StringBuilder();
		foreach (CSubUnit ship in ships)
		{
			CCountryTag? owner = state.SunkShips.FirstOrDefault(sunk => ReferenceEquals(sunk.Ship, ship))?.Owner ?? ship.Builder;
			text.Append('\n').Append('@').Append(owner?.Value ?? "---").Append(' ').Append('£').Append(ship.Type.Key).Append(' ').Append(ship.Name);
		}
		return text.ToString();
	}

	/// <summary>A side's losses as its battle counts them, cut to a whole number: men on land, strength in the air.</summary>
	private static long Losses(CCombatSide side) => side.Losses.Raw / CFixedPoint.Scale;

	/// <summary>How many a side had in the fight: each type's tally cut to a whole number and those added.</summary>
	private static long Men(CCombatSide side) => side.Size.Values.Sum(size => size.Raw / CFixedPoint.Scale);

	private static string Whole(long number) => number.ToString(System.Globalization.CultureInfo.InvariantCulture);

	/// <summary>The values every message of the player's carries: his country, his ruler's title and his government.</summary>
	private static void Standing(Func<string, string> localise, CGameState state, CCountry country, CMessage message)
	{
		message.Variables.Add(("COUNTRY", country.Name));
		message.Variables.Add(("MONARCHTITLE", CMessages.RulerTitle(country)));
		CMessages.Government(localise, state, country.Tag, message);
	}
}
