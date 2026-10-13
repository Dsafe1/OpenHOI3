using System;
using System.Collections.Generic;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the Peace row and its dialog say: the row's tooltip, the dialog's description, and the account of what
/// changes hands. See <c>docs/mechanics/war.md</c>, section 11.
/// </summary>
public static class CPeaceText
{
	private const string NewLine = "\n";

	/// <summary>
	/// The row's tooltip, the first that holds: itself, a diplomat, both in factions, a negotiator who may not
	/// speak for its side; else what it does. Whether they are at war is not asked.
	/// </summary>
	public static string Tooltip(Func<string, string> localise, CGameState state, CCountry actor, CCountry recipient,
		Func<CCountry, CCountry, string?> enRoute)
	{
		if (actor == recipient)
		{
			return localise("ACT_NO_SELF");
		}
		if (enRoute(actor, recipient) is { } onTheWay)
		{
			return onTheWay;
		}
		if (COfferRules.RealFaction(actor) && COfferRules.RealFaction(recipient))
		{
			return localise("PEACE_FACTION");
		}
		return localise(CPeace.Negotiators(state, actor, recipient) ? "PEACEDESC" : "PEACE_NOT_LEADER");
	}

	/// <summary>
	/// The request dialog's description, <c>PEACENEWDESC</c>: the recipient, whether it speaks for its allies - as
	/// the first of its side - or for itself, and what would change hands.
	/// </summary>
	public static string Description(Func<string, string> localise, CGameState state, CCountry actor, CCountry recipient)
	{
		CWar? war = CWarSides.Between(state, actor.Tag, recipient.Tag);
		bool leads = war is not null && ((war.Attackers.Count > 0 && war.Attackers[0] == recipient.Tag)
			|| (war.Defenders.Count > 0 && war.Defenders[0] == recipient.Tag));
		return CTextTemplate.Fill(localise("PEACENEWDESC"), ("COUNTRY", recipient.Name),
			("SELF", localise(leads ? "PEACENEWALLY" : "PEACESELF")), ("RESULT", Result(localise, state, actor, recipient)));
	}

	/// <summary>
	/// What would change hands, the original's <c>BuildResultText</c>: nothing, <c>PEACENEWWITHE</c>; else the
	/// attackers' losses and then the defenders', a line for each country gaining, in the order first met, and
	/// each country losing to it, in its side's order - red where the loser is on the actor's side, green where not.
	/// </summary>
	public static string Result(Func<string, string> localise, CGameState state, CCountry actor, CCountry recipient)
	{
		if (CWarSides.Between(state, actor.Tag, recipient.Tag) is not { } war)
		{
			return localise("PEACENEWWITHE");
		}
		(CCountryTag attacking, CCountryTag defending) = CPeace.Parties(war, actor.Tag, recipient.Tag);
		List<CPeaceTransfer> attackersLose = CPeace.AttackersLose(state, war, defending);
		List<CPeaceTransfer> defendersLose = CPeace.DefendersLose(state, war, attacking);
		if (attackersLose.Count == 0 && defendersLose.Count == 0)
		{
			return localise("PEACENEWWITHE");
		}
		bool actorAttacks = war.Attackers.Contains(actor.Tag);
		var text = new StringBuilder();
		Lines(localise, state, text, attackersLose, war.Attackers, actorAttacks);
		Lines(localise, state, text, defendersLose, war.Defenders, !actorAttacks);
		return text.ToString();
	}

	private static void Lines(Func<string, string> localise, CGameState state, StringBuilder text, List<CPeaceTransfer> transfers,
		List<CCountryTag> side, bool ours)
	{
		var gainers = new List<CCountryTag>();
		foreach (CPeaceTransfer transfer in transfers)
		{
			if (!gainers.Contains(transfer.Gainer))
			{
				gainers.Add(transfer.Gainer);
			}
		}
		foreach (CCountryTag gainer in gainers)
		{
			foreach (CCountryTag loser in side)
			{
				var names = new List<string>();
				foreach (CPeaceTransfer transfer in transfers)
				{
					if (transfer.Gainer == gainer && transfer.Loser == loser)
					{
						names.Add(state.Province(transfer.Province).Name);
					}
				}
				if (names.Count == 0)
				{
					continue;
				}
				text.Append(CTextTemplate.Fill(localise(ours ? "PEACENEWPROVUS" : "PEACENEWPROVTHEM"), ("NATION", Name(state, loser))));
				text.Append(ProvinceList(localise, names));
				text.Append("§W ").Append(CTextTemplate.Fill(localise("PEACETO"), ("REC", Name(state, gainer)))).Append(NewLine);
			}
		}
	}

	/// <summary>Province names as "A, B and C".</summary>
	public static string ProvinceList(Func<string, string> localise, List<string> names)
	{
		if (names.Count == 1)
		{
			return names[0];
		}
		return string.Join(", ", names.GetRange(0, names.Count - 1)) + " " + localise("AND") + names[^1];
	}

	private static string Name(CGameState state, CCountryTag tag) => state.Country(tag)?.Name ?? tag.Value;
}
