using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The diplomacy screen's war rows - Declare War, Declare Limited War, A Call to Arms! and Add war goal -
/// with their tooltips and the call to arms' dialog text. See <c>docs/mechanics/war.md</c>, section 10.
/// </summary>
public static class CWarActions
{
	private const string NewLine = "\n";

	/// <summary>The faction rule that lets its members declare a limited war.</summary>
	public const string LimitedWarRule = "limited_war";

	/// <summary>The rows the original lists first, at peace: Declare War and, where the player's faction has the rule, Declare Limited War.</summary>
	public static IEnumerable<COffer> DeclarationRows(CGameState state, CCountry player, CCountry chosen)
	{
		if (CCountryLists.AtWar(state, player.Tag, chosen.Tag) || CCountryLists.HasAlliance(state, player.Tag, chosen.Tag)
			|| COfferRules.OneRealFaction(player, chosen))
		{
			yield break;
		}
		yield return Row(state, EOfferKind.DeclareWar, player, chosen, true);
		if (CAlignment.FactionGroup(state, player)?.FactionRules.Contains(LimitedWarRule) == true)
		{
			yield return Row(state, EOfferKind.DeclareWar, player, chosen, false);
		}
	}

	/// <summary>
	/// A Call to Arms!, where the player is at war: against the first country it is at war with in a limited war
	/// the chosen country is not at war with, the chosen country an ally or of the player's real faction.
	/// </summary>
	public static COffer? CallRow(CGameState state, CCountry player, CCountry chosen)
	{
		if (!CCountryLists.HasAlliance(state, player.Tag, chosen.Tag) && !COfferRules.OneRealFaction(player, chosen))
		{
			return null;
		}
		foreach (CCountry enemy in CCountryLists.Enemies(state, player))
		{
			if (!CCountryLists.AtWar(state, chosen.Tag, enemy.Tag) && CWarSides.Between(state, player.Tag, enemy.Tag) is { IsLimited: true })
			{
				return Row(state, EOfferKind.CallAlly, player, chosen, false) with { Versus = enemy.Tag };
			}
		}
		return null;
	}

	/// <summary>Peace, at war with them; listed before Add war goal.</summary>
	public static COffer? PeaceRow(CGameState state, CCountry player, CCountry chosen)
	{
		return CCountryLists.AtWar(state, player.Tag, chosen.Tag) ? Row(state, EOfferKind.Peace, player, chosen, true) : null;
	}

	/// <summary>Add war goal, at war with them.</summary>
	public static COffer? GoalRow(CGameState state, CCountry player, CCountry chosen)
	{
		return CCountryLists.AtWar(state, player.Tag, chosen.Tag) ? Row(state, EOfferKind.AddWarGoal, player, chosen, true) : null;
	}

	private static COffer Row(CGameState state, EOfferKind kind, CCountry player, CCountry chosen, bool value) =>
		new(kind, player.Tag, chosen.Tag, EOfferStage.Proposed, value, state.TotalHours);

	/// <summary>The key a war row's texts are made from.</summary>
	public static string Prefix(COffer offer)
	{
		return offer.Kind switch
		{
			EOfferKind.DeclareWar => offer.Value ? "WAR" : "LIMWAR",
			EOfferKind.CallAlly => "CALL",
			EOfferKind.Peace => "PEACE",
			_ => "WARGOAL",
		};
	}

	/// <summary>A war row's tooltip.</summary>
	public static string Tooltip(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient,
		Func<CCountry, CCountry, string?> enRoute)
	{
		return offer.Kind switch
		{
			EOfferKind.DeclareWar => Declaration(localise, state, offer, actor, recipient, enRoute),
			EOfferKind.CallAlly => Call(localise, state, offer, actor, recipient, enRoute),
			EOfferKind.Peace => CPeaceText.Tooltip(localise, state, actor, recipient, enRoute),
			_ => Goal(localise, state, actor, recipient),
		};
	}

	/// <summary>
	/// Declare War's tooltip, the first that holds (<c>war.md</c>, section 10); a limited declaration adds its line.
	/// </summary>
	private static string Declaration(Func<string, string> localise, CGameState state, COffer offer, CCountry actor,
		CCountry recipient, Func<CCountry, CCountry, string?> enRoute)
	{
		string said = DeclarationReason(localise, state, offer, actor, recipient, enRoute);
		return offer.Value ? said : said + NewLine + localise("LIMITED_WARDESC");
	}

	private static string DeclarationReason(Func<string, string> localise, CGameState state, COffer offer, CCountry actor,
		CCountry recipient, Func<CCountry, CCountry, string?> enRoute)
	{
		if (actor == recipient)
		{
			return localise("ACT_NO_SELF");
		}
		if (CTruces.Has(state, actor, recipient))
		{
			return localise("HAS_TRUCE_WITH") + recipient.Name;
		}
		if (enRoute(actor, recipient) is { } onTheWay)
		{
			return onTheWay;
		}
		// The NAP spoken of: the first it would break that may not be broken, else the recipient's own.
		CCountry napWith = CDeclarations.UnbreakableNap(state, actor, recipient) ?? recipient;
		if (COfferRules.Nap(state, actor.Tag, napWith.Tag) is not { } nap)
		{
			if (COfferRules.OneRealFaction(actor, recipient) || CCountryLists.HasAlliance(state, actor.Tag, recipient.Tag))
			{
				return localise("NO_WAR_ALLIED");
			}
			CFixedPoint cost = COfferRules.Cost(state, offer);
			if (actor.DiplomaticInfluence < cost)
			{
				return CTextTemplate.Fill(localise("DIP_NO_INFL"), ("INF", CNumberText.Format(cost, 2)));
			}
			CFixedPoint ceiling = CNeutrality.WarCeiling(state, actor, recipient);
			if (actor.EffectiveNeutrality > ceiling)
			{
				return CTextTemplate.Fill(localise("DIP_WAR_NEUTR"), ("NEUT", CNumberText.Format(actor.EffectiveNeutrality, 2)),
					("THEM", recipient.Name), ("THREAT", CNumberText.Format(ceiling, 2)));
			}
			return localise(COfferRules.HasAccess(actor, recipient) ? "NO_WAR_MILITARY_ACCESS" : "ACT_WARDESC");
		}
		int unbreakable = (int)(COfferRules.Define(state, "NAP_UNBREAKABLE_MONTHS").Raw / 1000);
		int window = (int)(COfferRules.Define(state, "NAP_FORCE_BALANCE_RULE_MONTHS").Raw / 1000);
		long breakable = COfferRules.AddMonths(nap.Start, unbreakable);
		if (state.TotalHours < breakable)
		{
			return CTextTemplate.Fill(localise("DECLARE_WAR_NAP_LIMIT_DATE"), ("DATE", CDiplomacyActions.HourDate(localise, breakable)));
		}
		for (int step = 1; step <= 3; step++)
		{
			if (state.TotalHours < COfferRules.AddMonths(nap.Start, unbreakable + step * window))
			{
				CFixedPoint limit = COfferRules.Define(state, "NAP_BREAK_FORCE_BALANCE_" + step);
				long balance = CNapBalance.As(nap, actor.Tag);
				if (balance < limit.Raw)
				{
					return CTextTemplate.Fill(localise("DECLARE_WAR_NAP_LIMIT_TROOPS_BALANCE"),
						("LIMIT_PERC", CNumberText.Trimmed(limit * 100)), ("CURR_PERC", CNumberText.Trimmed(new CFixedPoint(balance) * 100)));
				}
			}
		}
		return localise("DECLARE_WAR_NAP_BREAKABLE");
	}

	/// <summary>The call to arms' tooltip: a diplomat, too little influence, a NAP with the other side where the caller leads its own, else what it does.</summary>
	private static string Call(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient,
		Func<CCountry, CCountry, string?> enRoute)
	{
		if (enRoute(actor, recipient) is { } onTheWay)
		{
			return onTheWay;
		}
		CFixedPoint cost = COfferRules.Cost(state, offer);
		if (actor.DiplomaticInfluence < cost)
		{
			return CTextTemplate.Fill(localise("DIP_NO_INFL"), ("INF", CNumberText.Format(cost, 2)));
		}
		if (state.Country(offer.Versus) is { } enemy && CWarSides.Between(state, actor.Tag, enemy.Tag) is { } war
			&& (war.Attackers.IndexOf(actor.Tag) == 0 || war.Defenders.IndexOf(actor.Tag) == 0)
			&& CCallsToArms.NapWithEnemySide(state, war, actor, recipient) is { } napped)
		{
			return CTextTemplate.Fill(localise("CAN_NOT_JOIN_WAR_NAP"), ("COUNTRY", napped.Name));
		}
		return localise("CALLDESC");
	}

	/// <summary>Add war goal's tooltip: no war (a key with no text), the cooldown's date, else nothing.</summary>
	private static string Goal(Func<string, string> localise, CGameState state, CCountry actor, CCountry recipient)
	{
		if (!CCountryLists.AtWar(state, actor.Tag, recipient.Tag))
		{
			return localise("DIPL_NOT_AT_WAR");
		}
		if (!CWarGoals.CooledDown(state, actor, recipient))
		{
			return CTextTemplate.Fill(localise("CAN_NOT_ADD_WARGOAL_UNTIL"),
				("DATE", CDiplomacyActions.HourDate(localise, CGameState.Status(actor, recipient).NextWarGoal)));
		}
		return "";
	}

	/// <summary>The call to arms' request dialog description: <c>CALLNEWDESC</c> and the cost.</summary>
	public static string CallDescription(Func<string, string> localise, CGameState state, COffer offer, CCountry recipient)
	{
		return CTextTemplate.Fill(localise("CALLNEWDESC"), ("COUNTRY", recipient.Name)) + NewLine
			+ CTextTemplate.Fill(localise("DIP_INFLUENCECOST"), ("COST", CNumberText.Format(COfferRules.Cost(state, offer), 2)));
	}
}
