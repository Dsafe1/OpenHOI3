using System;
using System.Collections.Generic;
using OpenHOI3.Content.Messages;
using OpenHOI3.Interface.Events;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Interface.Messages;

/// <summary>
/// The message an answered event, a taken decision and a pact raise, as the player on this machine is
/// told it: its type by whether it is the player's own, and its values - the player's government's
/// posts, the country, the event's or the decision's words, the pact's countries. See
/// <c>docs/mechanics/messages.md</c>, sections 2 and 3, and <c>docs/mechanics/agreements.md</c>,
/// section 8.
/// </summary>
public static class CMessages
{
	public static CMessage EventAnswered(Func<string, string> localise, CMessageTypes types, CGameState state,
		CCountryTag? player, CEvent answered, CEventScope scope, int option)
	{
		string name;
		// A province's event has no country of its own, so its owner is told "OTHER".
		if (scope.Country is { } own && own == player)
		{
			name = "EVENTHAPPENUS";
		}
		else if (answered.Options.Count <= 1)
		{
			name = answered.Major ? "MAJOREVENTHAPPENOTHER" : "EVENTHAPPENOTHER";
		}
		else
		{
			name = answered.Major ? "MAJOREVENTHAPPENOTHEROPTION" : "EVENTHAPPENOTHEROPTION";
		}
		CMessage message = Made(localise, types, state, name, scope);
		Government(localise, state, player, message);
		message.Variables.Add(("COUNTRY", CountryName(state, scope)));
		message.Variables.Add(("EVENT", CEventText.Localise(localise, state, answered.Title, scope)));
		message.Variables.Add(("EVENTDESC", CEventText.Localise(localise, state, answered.Description, scope)));
		string chosen = option >= 0 && option < answered.Options.Count ? answered.Options[option].Name : "";
		message.Variables.Add(("OPTION", CEventText.Localise(localise, state, chosen, scope)));
		return message;
	}

	public static CMessage DecisionTaken(Func<string, string> localise, CMessageTypes types, CGameState state,
		CCountryTag? player, CDecision taken, CEventScope scope)
	{
		bool province = scope.Province != 0;
		string name = scope.Country is { } own && own == player
			? province ? "PROVDECISIONUS" : "DECISIONUS"
			: province ? "PROVDECISIONOTHER"
			: taken.Major ? "MAJORDECISIONOTHER" : "DECISIONOTHER";
		CMessage message = Made(localise, types, state, name, scope);
		Government(localise, state, player, message);
		message.Variables.Add(("COUNTRY", CountryName(state, scope)));
		message.Variables.Add(("EVENT", CEventText.Localise(localise, state, taken.Name + "_title", scope)));
		message.Variables.Add(("DESC", CEventText.Localise(localise, state, taken.Name + "_desc", scope)));
		if (province)
		{
			message.Variables.Add(("PROVINCENAME", state.Province(scope.Province).Name));
		}
		return message;
	}

	/// <summary>
	/// A pact's message: the type for the player as actor, as recipient or as neither, and none where
	/// that type is empty. Its values are the player's country's standard ones, the player's ruler
	/// again, the two countries, and an alignment's action and faction - the action first, since its
	/// text holds <c>$FACTION$</c>. See <c>docs/mechanics/agreements.md</c>, section 8.
	/// </summary>
	public static CMessage? Pact(Func<string, string> localise, CMessageTypes types, CGameState state, CCountryTag? player,
		CPactMessage pact)
	{
		string name = pact.Told is { } told ? (player == told ? pact.We : "")
			: player == pact.Actor ? pact.We : player == pact.Recipient ? pact.They : pact.Other;
		if (name.Length == 0)
		{
			return null;
		}
		// A trade made or refused is not told to a player who has automated his trade (trade.md, section 12).
		if (player is { } own && state.AutomateTrade.Contains(own) && name is "ACTRAWITHUS" or "WETRADEAG" or "DETRAWITHUS" or "WEDETRA")
		{
			return null;
		}
		var message = new CMessage(types.Find(name), CMessageText.Date(localise, state.Date, state.Hour))
		{
			FirstCountry = pact.Actor,
			SecondCountry = pact.Recipient,
		};
		if (state.Country(player) is { } country)
		{
			message.Variables.Add(("COUNTRY", country.Name));
			message.Variables.Add(("MONARCHTITLE", RulerTitle(country)));
			message.Variables.Add(("TITLE", RulerTitle(country)));
			Government(localise, state, player, message);
			message.Variables.Add(("MONARCHTITLE", RulerTitle(country)));
		}
		string actorName = state.Country(pact.Actor)?.Name ?? pact.Actor.Value;
		string recipientName = state.Country(pact.Recipient)?.Name ?? pact.Recipient.Value;
		if (pact.TurnedRound)
		{
			(actorName, recipientName) = (recipientName, actorName);
		}
		message.Variables.Add(("ACTOR", actorName));
		// The same name again, which WEDETRA's text asks for.
		message.Variables.Add(("ACTORS", actorName));
		message.Variables.Add(("RECIPIENT", recipientName));
		message.Variables.Add(("MESSENGER", pact.Messenger));
		if (pact.Deal is { } deal && TradeSides(name, deal, player) is ({ } gives, { } gets))
		{
			message.Variables.Add(("ACT_TRADE", CTradeWindowText.Goods(localise, gives)));
			message.Variables.Add(("REC_TRADE", CTradeWindowText.Goods(localise, gets)));
		}
		if (pact.Action.Length > 0)
		{
			message.Variables.Add(("ACTION", pact.Action));
		}
		if (pact.Faction.Length > 0)
		{
			message.Variables.Add(("FACTION", pact.Faction));
		}
		foreach ((string key, string value) in pact.Values)
		{
			// A licence's NAME is the seller's current model of the type, as its dialog's row writes it (aid.md, section 4).
			if (key == "NAME" && pact.We.StartsWith("LICBUY", StringComparison.Ordinal) && state.Country(pact.Recipient) is { } seller
				&& state.SubUnitTypes.TryGetValue(value, out Sim.Units.CSubUnitType? type))
			{
				message.Variables.Add((key, CLicenceText.ModelName(localise, state, seller, type) ?? localise(type.Key)));
				continue;
			}
			// An election's two parties are named for the country (politics.md, section 5).
			if (key is "PREVIOUS_PARTY" or "WINNING_PARTY")
			{
				message.Variables.Add((key, CPartyText.Name(localise, value, pact.Actor.Value)));
				continue;
			}
			// A law enacted and a minister appointed (politics.md, section 9): the law named for the country, its
			// description with every §b made §W, its modifier's lines, and the group or the post localised.
			if (pact.We is Sim.Politics.CLaws.AppointedMessage or Sim.Commands.CChangeMinisterCommand.AppointedMessage)
			{
				message.Variables.Add((key, Appointed(localise, state, pact, key, value)));
				continue;
			}
			// A triggered modifier come or gone: its name, every value of its modifier and its description (modifiers.md,
			// section 5).
			if (pact.We is Sim.Modifiers.CTriggeredModifiers.Gained or Sim.Modifiers.CTriggeredModifiers.Lost)
			{
				message.Variables.Add((key, Triggered(localise, state, key, value)));
				continue;
			}
			// Spies' messages name countries and a technology by tag and name (espionage.md, section 12).
			if (pact.We.StartsWith("SPIESFOUND", StringComparison.Ordinal) || pact.We.Contains("TECH_STOLEN", StringComparison.Ordinal)
				|| pact.We.StartsWith("WE_STEAL_TECH", StringComparison.Ordinal))
			{
				message.Variables.Add((key, SpyValue(localise, state, pact, key, value)));
				continue;
			}
			// A war goal's type, a government, a faction and a faction's comment are written as their keys (war.md,
			// sections 13 and 14; factions.md, section 3).
			message.Variables.Add((key, key is "WARGOAL" or "GOVERNMENT" or "FACTION" or "COMMENT" or "BAD_COMMENT" or "COMMENT_BAD"
				? localise(value) : value));
		}
		return message;
	}

	/// <summary>
	/// One variable of a spies' message: a country's name for WHERE and RESPONSIBLE, its adjective for TARGET_ADJ, the
	/// technology's name for TYPE, and for EFFECT what the technology's levels do at the level stolen, its first §W made
	/// §b, or "none".
	/// </summary>
	private static string SpyValue(Func<string, string> localise, CGameState state, CPactMessage pact, string key, string value)
	{
		switch (key)
		{
			case "WHERE":
			case "RESPONSIBLE":
				return state.Country(new CCountryTag(value))?.Name ?? value;
			case "TARGET_ADJ":
				return state.Country(new CCountryTag(value)) is { } target ? CEventText.Adjective(localise, target) : value;
			case "TYPE":
				return localise(value);
			case "EFFECT":
				if (!state.Technologies.TryGetValue(value, out Sim.Research.CTechnology? technology)
					|| state.Country(pact.Actor) is not { } thief)
				{
					return localise("none");
				}
				string effect = CTechnologyEffectsText.For(localise, state, thief, technology, thief.TechnologyLevels.GetValueOrDefault(value));
				if (effect.Length == 0)
				{
					return localise("none");
				}
				int at = effect.IndexOf("§W", StringComparison.Ordinal);
				return at < 0 ? effect : effect[..at] + "§b" + effect[(at + 2)..];
			default:
				return value;
		}
	}

	/// <summary>
	/// One variable of <c>TRIGMOD</c> or <c>TRIGMODLOST</c>, each given the entry's key: NAME its name, EFFECT every value
	/// of its modifier, EXPLANATION its <c>desc_</c> text.
	/// </summary>
	private static string Triggered(Func<string, string> localise, CGameState state, string key, string value)
	{
		switch (key)
		{
			case "NAME":
				return localise(value);
			case "EFFECT":
				return state.TriggeredModifiers.Find(entry => entry.Name == value) is { } triggered
					? Modifiers.CModifierText.AllValues(localise, triggered.Modifier)
					: "";
			case "EXPLANATION":
				return localise("desc_" + value);
			default:
				return value;
		}
	}

	/// <summary>One variable of <c>LAW_APPOINTED</c> or <c>MINISTER_APPOINTED</c>, from what the command wrote into it.</summary>
	private static string Appointed(Func<string, string> localise, CGameState state, CPactMessage pact, string key, string value)
	{
		switch (key)
		{
			case "LAWNAME":
				return CPartyText.Name(localise, value, pact.Actor.Value);
			case "DESC":
				return localise(value + "_desc").Replace("§b", "§W", StringComparison.Ordinal);
			case "EFFECT":
				foreach (Sim.Countries.CLawGroup group in state.LawGroups)
				{
					if (group.Laws.Find(law => law.Name == value) is { } law)
					{
						return Modifiers.CModifierText.AllValues(localise, law.Modifier);
					}
				}
				return "";
			case "PERSONALITY_DESC":
				// Ours: the minister type's name; how the original describes it was not read.
			case "POSITION":
				return localise(value);
			default:
				return value;
		}
	}

	/// <summary>
	/// A trade message's two blocks, <c>ACT_TRADE</c> and <c>REC_TRADE</c>: for the accepting messages the
	/// route's own from and to - turned round for the recipient, <c>WETRADEAG</c> - and for the one told of
	/// a cancellation, <c>BRKTRADEUS</c>, what the player gives and gets on the route. None for the rest.
	/// See <c>trade.md</c>, section 10.
	/// </summary>
	private static (Func<Sim.Economy.EGoods, long>?, Func<Sim.Economy.EGoods, long>?) TradeSides(string name,
		Sim.Trade.CTradeDeal deal, CCountryTag? player)
	{
		return name switch
		{
			"ACTRAWITHUS" or "OTHERTRADEAG" => (deal.Terms.From, deal.Terms.To),
			"WETRADEAG" => (deal.Terms.To, deal.Terms.From),
			"BRKTRADEUS" => player == deal.From ? (deal.Terms.From, deal.Terms.To) : (deal.Terms.To, deal.Terms.From),
			_ => (null, null),
		};
	}

	/// <summary>
	/// The key of a country's ruler's title, <c>&lt;government&gt;_ruler</c>, which is looked up when
	/// shown; a country with none has the original's null government, <c>nogovernment</c>.
	/// </summary>
	private static string RulerTitle(CCountry country) =>
		(country.GovernmentType.Length > 0 ? country.GovernmentType : "nogovernment") + "_ruler";

	private static CMessage Made(Func<string, string> localise, CMessageTypes types, CGameState state, string name,
		CEventScope scope)
	{
		return new CMessage(types.Find(name), CMessageText.Date(localise, state.Date, state.Hour))
		{
			FirstCountry = scope.Country ?? state.Province(scope.Province).Owner,
			Province = scope.Province,
		};
	}

	/// <summary>Each post of the player's government, by its key: "$TITLE$ $NAME$".</summary>
	private static void Government(Func<string, string> localise, CGameState state, CCountryTag? player, CMessage message)
	{
		if (state.Country(player) is not { } country)
		{
			return;
		}
		foreach ((string post, CMinister minister) in country.Government)
		{
			string entry = localise("MINISTER_MESSAGE_ENTRY").Replace("$TITLE$", localise(post)).Replace("$NAME$", minister.Name);
			message.Variables.Add((post, entry));
		}
	}

	/// <summary>The scope's country, or for a province's scope its owner, or the province.</summary>
	private static string CountryName(CGameState state, CEventScope scope)
	{
		if (state.Country(scope.Country) is { } country)
		{
			return country.Name;
		}
		Sim.Provinces.CProvince here = state.Province(scope.Province);
		return state.Country(here.Owner)?.Name ?? here.Name;
	}
}
