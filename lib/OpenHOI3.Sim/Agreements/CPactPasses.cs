using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Sim.Agreements;

/// <summary>
/// The agreements' share of the hour and the day: each AI country answering the offers made to it,
/// offers left too long dropped, and pacts ending by themselves. See
/// <c>docs/mechanics/agreements.md</c>, sections 2 and 6.
/// </summary>
public static class CPactPasses
{
	/// <summary>A score at or below this is always declined.</summary>
	public const int DeclinedAtOrBelow = 30;

	/// <summary>How long an offer may wait on a list before the day drops it.</summary>
	private const long PendingHours = 96;

	/// <summary>How long a guarantee lasts: twenty years of hours.</summary>
	public const long GuaranteeHours = 175200;

	/// <summary>
	/// Every country nobody plays answers its offers, in the order they came: a kind the AI answers is
	/// scored by the install's Lua and accepted where the score is over 30 and a draw of 0 to 99 is
	/// below it, else declined; each answer is posted as a command; and the list is emptied. **One trade
	/// is accepted an hour**: a further trade the draw would accept stays on the list, still a proposal,
	/// for the next hour (<c>trade.md</c>, section 2). **A played country's offers are put to its player
	/// instead**, each once: under automate-trade a trade is kept for the hour's roll
	/// (<see cref="Trade.CAutomatedTrades"/>), and every other proposal of a kind with a dialog is asked
	/// (<see cref="CGameState.OfferAsked"/>); the rest are dropped (<c>agreements.md</c>, section 10).
	/// </summary>
	public static void AnswerOffers(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (country.AiPaused || country.PendingOffers.Count == 0)
			{
				continue;
			}
			var kept = new List<COffer>();
			if (state.Played.Contains(country.Tag))
			{
				bool automated = state.AutomateTrade.Contains(country.Tag);
				foreach (COffer offer in country.PendingOffers)
				{
					if (offer.Kind == EOfferKind.Trade && automated)
					{
						kept.Add(offer);
					}
					else if (offer.Kind == EOfferKind.CallAlly && offer.Stage == EOfferStage.Proposed)
					{
						// The screen's call is accepted for him unasked; one that asks, only while still valid (war.md, section 6).
						if (!offer.Value)
						{
							state.Outbox.Add(new COfferCommand(offer with { Stage = EOfferStage.Accepted }));
						}
						else if (Wars.CCallsToArms.MayAsk(state, offer))
						{
							state.RaiseOfferAsked(offer);
						}
					}
					else if (COfferRules.AsksPlayer(offer))
					{
						state.RaiseOfferAsked(offer);
					}
				}
			}
			else if (CCountryLists.OwnedCount(state, country.Tag) > 0)
			{
				bool tradeAccepted = false;
				foreach (COffer offer in country.PendingOffers)
				{
					if (offer.Stage != EOfferStage.Proposed || !COfferRules.AiAnswers(offer))
					{
						continue;
					}
					EOfferStage answer = Answer(state, offer);
					if (offer.Kind == EOfferKind.Trade && answer == EOfferStage.Accepted)
					{
						if (tradeAccepted)
						{
							kept.Add(offer);
							continue;
						}
						tradeAccepted = true;
					}
					state.Outbox.Add(new COfferCommand(offer with { Stage = answer }));
				}
			}
			country.PendingOffers.Clear();
			country.PendingOffers.AddRange(kept);
		}
	}

	/// <summary>The AI's answer to an offer: its score from the Lua, then a draw.</summary>
	public static EOfferStage Answer(CGameState state, COffer offer)
	{
		int score = Score(state, offer);
		if (score <= DeclinedAtOrBelow)
		{
			return EOfferStage.Declined;
		}
		return state.Random.Next(100) < score ? EOfferStage.Accepted : EOfferStage.Declined;
	}

	/// <summary>
	/// What the recipient's AI makes of the offer, by the kind's Lua function: 0 where the kind has none
	/// or there is no Lua. <paramref name="scripts"/> is the Lua asked, the game's own where none is given: a
	/// script asking from one of the trading's workers is answered on that worker, as the original re-enters
	/// the asking thread's state (<c>docs/mechanics/trade.md</c>, section 9).
	/// </summary>
	public static int Score(CGameState state, COffer offer, Ai.IAiScripts? scripts = null)
	{
		scripts ??= state.AiScripts;
		if (COfferRules.ScoreFunction(offer.Kind) is not { } function || state.Country(offer.Actor) is not { } actor
			|| state.Country(offer.Recipient) is not { } recipient)
		{
			return 0;
		}
		if (offer.Kind == EOfferKind.Trade)
		{
			return TradeScore(state, offer, actor, recipient, scripts);
		}
		if (offer.Kind == EOfferKind.Licence)
		{
			return scripts?.LicenceScore(state, offer) ?? 0;
		}
		// A join is put to the script the other way round: the faction's owner first, the joiner second (factions.md,
		// section 3). The recipient's AI answers either way.
		if (offer.Kind == EOfferKind.Faction && offer.Value)
		{
			return scripts?.DiploScore(state, function, recipient, actor, recipient) ?? 0;
		}
		return scripts?.DiploScore(state, function, actor, recipient, recipient) ?? 0;
	}

	/// <summary>
	/// An offer to trade's score: 100 between a country and itself; 0 where it is not valid, its convoy
	/// cannot be made, or the recipient already trades a good of it on a route that is not running;
	/// otherwise <c>DiploScore_OfferTrade</c>'s.
	/// </summary>
	private static int TradeScore(CGameState state, COffer offer, CCountry actor, CCountry recipient, Ai.IAiScripts? scripts)
	{
		if (actor == recipient)
		{
			return 100;
		}
		if (!COfferRules.IsValid(state, offer) || offer.Deal is not { } deal
			|| !Trade.CTradeRules.IsConvoyPossible(state, Supply.CSupplyNetworks.Refresh(state), deal)
			|| Trade.CTradeRules.AlreadyTradingDisabledResource(state, deal))
		{
			return 0;
		}
		return scripts?.OfferTradeScore(state, offer) ?? 0;
	}

	/// <summary>
	/// At the head of each country's day: offers waiting more than 96 hours dropped; the pacts that end
	/// by themselves ended; and each running influence's upkeep paid.
	/// </summary>
	public static void RunDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.PendingOffers.RemoveAll(o => o.Queued + PendingHours < state.TotalHours);
		}
		bool[] owns = CRelations.Owning(state);
		foreach (CCountry country in state.Countries)
		{
			if (!owns[country.Index])
			{
				continue;
			}
			foreach (CCountry other in state.Countries)
			{
				if (owns[other.Index] && other != country)
				{
					Expire(state, country, other, owns);
				}
			}
		}
		foreach (CCountry country in state.Countries)
		{
			PayUpkeep(state, country);
		}
	}

	/// <summary>What the country holds towards the other that has run its course, in the original's order.</summary>
	private static void Expire(CGameState state, CCountry country, CCountry other, bool[] owns)
	{
		if (state.Influences.Find(i => i.Influencer == other.Tag && i.Influenced == country.Tag) is { } influence
			&& InfluenceOver(state, influence, other, country, owns))
		{
			CPacts.EndInfluence(state, influence);
		}
		if (COfferRules.Align(state, country.Tag, other.Tag) is { } align && AlignOver(state, align, country, other, owns))
		{
			CPacts.EndAlign(state, align);
			state.RaisePactMessage(new CPactMessage("DIPLOEXPIRE", "", "", country.Tag, other.Tag)
			{
				Faction = align.Faction,
				Action = "DIPLOEXPIRE_ALIGN",
			});
		}
		foreach (CGuarantee guarantee in state.Guarantees.ToArray())
		{
			if (guarantee.Guarantor == other.Tag && guarantee.Guaranteed == country.Tag && guarantee.Start is { } start
				&& state.TotalHours > start + GuaranteeHours)
			{
				state.Guarantees.Remove(guarantee);
			}
		}
		if (COfferRules.Nap(state, country.Tag, other.Tag) is { } nap)
		{
			int months = (int)(COfferRules.Define(state, "NAP_EXPIRY_MONTHS").Raw / 1000);
			if (state.TotalHours > COfferRules.AddMonths(nap.Start, months))
			{
				state.Naps.Remove(nap);
			}
		}
		if (CCountryLists.HasAlliance(state, country.Tag, other.Tag) && COfferRules.OneRealFaction(country, other)
			&& country.Overlord != other.Tag && other.Overlord != country.Tag)
		{
			state.Alliances.RemoveAll(a => a.Joins(country.Tag, other.Tag));
			CPacts.Message(state, "MILALLWEBAN", "MILALLBAN", "MILALLBANOTHER", country, other);
		}
	}

	private static bool InfluenceOver(CGameState state, CInfluence influence, CCountry influencer, CCountry influenced, bool[] owns)
	{
		if (influencer.Faction == influenced.Faction || COfferRules.RealFaction(influenced) || COfferRules.IsPuppet(influenced)
			|| !owns[influencer.Index] || !owns[influenced.Index])
		{
			return true;
		}
		return Ran(state, influence.Start, "DAYS_OF_INFLUENCE_RELATION_CHANGE");
	}

	private static bool AlignOver(CGameState state, CAlign align, CCountry aligner, CCountry leader, bool[] owns)
	{
		if (aligner.Faction == leader.Faction || !owns[aligner.Index] || !owns[leader.Index])
		{
			return true;
		}
		return Ran(state, align.Start, "DAYS_OF_ALIGN_RELATION_CHANGE");
	}

	/// <summary>Whether a define's days have run since the start; never where they are below nought.</summary>
	private static bool Ran(CGameState state, long start, string define)
	{
		long days = COfferRules.Define(state, define).Raw / 1000;
		return days >= 0 && state.TotalHours > start + days * CGameState.HoursPerDay;
	}

	/// <summary>
	/// Each influence the country runs costs it <c>INFLUENCE_UPKEEP</c> a day; one it cannot pay leaves
	/// it at nought and ends the influence.
	/// </summary>
	private static void PayUpkeep(CGameState state, CCountry country)
	{
		CFixedPoint upkeep = COfferRules.Define(state, "INFLUENCE_UPKEEP");
		foreach (CInfluence influence in state.Influences.ToArray())
		{
			if (influence.Influencer != country.Tag || state.Country(influence.Influenced) is not { } influenced)
			{
				continue;
			}
			CFixedPoint left = country.DiplomaticInfluence - upkeep;
			if (left < CFixedPoint.Zero)
			{
				country.DiplomaticInfluence = CFixedPoint.Zero;
				CPacts.StopInfluence(state, country, influenced);
			}
			else
			{
				country.DiplomaticInfluence = left;
			}
		}
	}
}
