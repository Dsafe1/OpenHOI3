using System.Collections.Generic;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// The peace offer, the original's <c>CPeaceAction</c>: who may make it, what changes hands, and what an
/// answer does. It has no terms, costs nothing and enforces no war goal. See <c>docs/mechanics/war.md</c>,
/// section 11.
/// </summary>
public static class CPeace
{
	/// <summary>Whether an offer between the two does anything: they are at war.</summary>
	public static bool IsValid(CGameState state, CCountry actor, CCountry recipient)
	{
		return CCountryLists.AtWar(state, actor.Tag, recipient.Tag);
	}

	/// <summary>
	/// Whether its button may be pressed: not to itself, no diplomat on his way, not both in real factions,
	/// at war, and each side's negotiator one who may speak for it.
	/// </summary>
	public static bool IsSelectable(CGameState state, CCountry actor, CCountry recipient)
	{
		return actor != recipient && !COfferRules.DiplomatEnRoute(state, actor, recipient)
			&& !(COfferRules.RealFaction(actor) && COfferRules.RealFaction(recipient))
			&& IsValid(state, actor, recipient) && Negotiators(state, actor, recipient);
	}

	/// <summary>
	/// Whether each of the two may negotiate for its side, the actor first: one in a real faction it does not
	/// lead, on a side of two or more, may not where its faction's leader is at war with the other or it is not
	/// the first of its side; one in no real faction must be the first of its side. True where there is no war.
	/// </summary>
	public static bool Negotiators(CGameState state, CCountry actor, CCountry recipient)
	{
		if (CWarSides.Between(state, actor.Tag, recipient.Tag) is not { } war)
		{
			return true;
		}
		return MaySpeak(state, war, actor, recipient) && MaySpeak(state, war, recipient, actor);
	}

	private static bool MaySpeak(CGameState state, CWar war, CCountry party, CCountry other)
	{
		bool attacking = war.Attackers.Contains(party.Tag);
		List<CCountryTag> side = attacking ? war.Attackers : war.Defenders;
		bool first = side.Count > 0 && side[0] == party.Tag;
		if (!COfferRules.RealFaction(party))
		{
			return first;
		}
		CCountryTag? leader = COfferRules.Leader(state, party.Faction);
		if (leader == party.Tag || side.Count < 2)
		{
			return true;
		}
		if (leader is { } led && CCountryLists.AtWar(state, led, other.Tag))
		{
			return false;
		}
		return first;
	}

	/// <summary>
	/// The negotiators of the two sides: the actor's side's and the other's, the attacking side's first - the
	/// actor, unless it defends.
	/// </summary>
	public static (CCountryTag Attacking, CCountryTag Defending) Parties(CWar war, CCountryTag actor, CCountryTag recipient)
	{
		return war.Defenders.Contains(actor) ? (recipient, actor) : (actor, recipient);
	}

	/// <summary>What the attackers lose, each attacker in turn against the defending side's negotiator.</summary>
	public static List<CPeaceTransfer> AttackersLose(CGameState state, CWar war, CCountryTag defending)
	{
		var transfers = new List<CPeaceTransfer>();
		foreach (CCountryTag attacker in war.Attackers)
		{
			Lose(state, attacker, defending, war.Defenders, transfers);
		}
		return transfers;
	}

	/// <summary>What the defenders lose, each defender in turn against the attacking side's negotiator.</summary>
	public static List<CPeaceTransfer> DefendersLose(CGameState state, CWar war, CCountryTag attacking)
	{
		var transfers = new List<CPeaceTransfer>();
		foreach (CCountryTag defender in war.Defenders)
		{
			Lose(state, defender, attacking, war.Attackers, transfers);
		}
		return transfers;
	}

	/// <summary>
	/// The original's <c>CCountry_ListPeaceTransfers</c>: each province the loser owns and does not hold, once for
	/// each of its cores held by the party or a country of the other side. Held by the party, it goes to the
	/// party; held by another of that side, to the first such core holder, who need not hold it; held by
	/// anyone else, nowhere. So one province may be listed twice, to the same country.
	/// </summary>
	private static void Lose(CGameState state, CCountryTag loser, CCountryTag party, List<CCountryTag> otherSide,
		List<CPeaceTransfer> transfers)
	{
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner != loser || province.Controller == loser || province.Controller is not { } holder)
			{
				continue;
			}
			CCountryTag? firstCore = null;
			foreach (CCountryTag core in province.Cores)
			{
				if (core == loser || (core != party && !otherSide.Contains(core)))
				{
					continue;
				}
				firstCore ??= core;
				if (holder == party)
				{
					transfers.Add(new CPeaceTransfer(province.Id, loser, party));
				}
				else if (otherSide.Contains(holder))
				{
					transfers.Add(new CPeaceTransfer(province.Id, loser, firstCore.Value));
				}
			}
		}
	}

	/// <summary>
	/// An answer to a peace offer. Accepted: the provinces change hands, the defenders' first; the first attacker
	/// and each defender give back what they hold of each other; every attacker-defender pair stops being at
	/// war - where both sides have two or more by every country leaving, otherwise by the lone one of a side
	/// leaving; and the players who made it or are still among the attackers are told. Declined: the actor's
	/// player is told. Nothing else - no relation, no cost, no war goal.
	/// </summary>
	public static void Apply(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (offer.Stage == EOfferStage.Declined)
		{
			state.RaisePactMessage(new CPactMessage("PEACEDECLINE", "", "", actor.Tag, recipient.Tag) { Sound = CPactMessage.Failure });
			return;
		}
		if (offer.Stage != EOfferStage.Accepted || CWarSides.Between(state, actor.Tag, recipient.Tag) is not { } war)
		{
			return;
		}
		(CCountryTag attacking, CCountryTag defending) = Parties(war, actor.Tag, recipient.Tag);
		var transfers = DefendersLose(state, war, attacking);
		transfers.AddRange(AttackersLose(state, war, defending));
		foreach (CPeaceTransfer transfer in transfers)
		{
			CProvinceHands.ChangeOwner(state, state.Province(transfer.Province), transfer.Gainer, consequences: true);
		}
		var attackers = new List<CCountryTag>(war.Attackers);
		var defenders = new List<CCountryTag>(war.Defenders);
		if (attackers.Count > 0)
		{
			foreach (CCountryTag defender in defenders)
			{
				COccupiedLand.ReturnTo(state, attackers[0], defender);
				COccupiedLand.ReturnTo(state, defender, attackers[0]);
			}
		}
		if (attackers.Count >= 2 && defenders.Count >= 2)
		{
			foreach (CCountryTag defender in defenders)
			{
				CWarSides.Leave(state, war, defender);
			}
			foreach (CCountryTag attacker in attackers)
			{
				CWarSides.Leave(state, war, attacker);
			}
		}
		else
		{
			if (attackers.Count == 1)
			{
				CWarSides.Leave(state, war, attackers[0]);
			}
			if (defenders.Count == 1)
			{
				CWarSides.Leave(state, war, defenders[0]);
			}
		}
		// Each attacker's AI strategy made again, the defenders' not (aistrategy.md, section 1).
		foreach (CCountryTag attacker in attackers)
		{
			if (state.Country(attacker) is { } country)
			{
				Ai.CAiStrategies.Remake(state, country);
			}
		}
		foreach (CCountryTag played in state.Played)
		{
			if (played == actor.Tag || war.Attackers.Contains(played))
			{
				state.RaisePactMessage(new CPactMessage("PEACEACCEPT", "", "", actor.Tag, recipient.Tag)
				{
					Told = played,
					Sound = CPactMessage.Success,
				});
			}
		}
	}
}
