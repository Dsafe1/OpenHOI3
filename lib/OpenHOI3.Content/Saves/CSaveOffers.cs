using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Trade;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// The offers on a country's list - a proposal on its recipient's, an answer on its actor's - each a key of the country
/// block named for its kind, as the original's pending diplomatic actions are saved: the stage, the two countries, the
/// hour, yes or no and the hour it was put on the list, then what its kind carries. See
/// <c>docs/mechanics/savegame.md</c>, section 19; <c>reversing/findings/FINDINGS-diploaction.md</c>, sections 1 and 3.
/// </summary>
public static class CSaveOffers
{
	/// <summary>Each kind's key, the original action class's own.</summary>
	private static readonly Dictionary<EOfferKind, string> KeyOf = new()
	{
		[EOfferKind.Alliance] = "allianceaction",
		[EOfferKind.NonAggressionPact] = "nap",
		[EOfferKind.Expedition] = "send_expedition",
		[EOfferKind.Guarantee] = "guaranteeaction",
		[EOfferKind.AskMilitaryAccess] = "milaccess",
		[EOfferKind.OfferMilitaryAccess] = "offermilaccess",
		[EOfferKind.Embargo] = "embargo_action",
		[EOfferKind.Influence] = "influence",
		[EOfferKind.Align] = "influence_leaders",
		[EOfferKind.Trade] = "trade_action",
		[EOfferKind.Debt] = "debtaction_action",
		[EOfferKind.ShareTechnology] = "share_technology",
		[EOfferKind.RequestLendLease] = "request_lendlease",
		[EOfferKind.OfferLendLease] = "offer_lendlease",
		[EOfferKind.Licence] = "licence_technology",
		[EOfferKind.DeclareWar] = "declarewar",
		[EOfferKind.CallAlly] = "callaction",
		[EOfferKind.AddWarGoal] = "add_war_goal",
		[EOfferKind.Peace] = "peace_action",
		[EOfferKind.Faction] = "faction_action",
	};

	/// <summary>
	/// The keys a load reads back: every kind's but <c>add_war_goal</c>, which the original writes and its country loader
	/// does not name, so an added war goal waiting for its answer is lost on a load (<c>docs/enhanced.md</c>).
	/// </summary>
	private static readonly Dictionary<string, EOfferKind> KindOf = KeyOf
		.Where(entry => entry.Key != EOfferKind.AddWarGoal)
		.ToDictionary(entry => entry.Value, entry => entry.Key, StringComparer.Ordinal);

	/// <summary>The keys of every block an offer's base writes; a kind's own are taken by its arm.</summary>
	private static readonly string[] BaseKeys = { "type", "actor", "recipient", "date", "value", "last_command_date" };

	/// <summary>Whether a country block's key is an offer this reads.</summary>
	public static bool IsOffer(string key) => KindOf.ContainsKey(key);

	/// <summary>An offer read onto the country's list, in the order its block stands; one it cannot make out is reported.</summary>
	public static void Read(CGameState state, CCountry country, ClausewitzNode node, CSaveIds ids, CSaveReport report)
	{
		string key = node.Key!;
		EOfferKind kind = KindOf[key];
		if (!CCountryTag.TryParse(node.Get("actor")?.Value?.Trim('"'), out CCountryTag actor)
			|| !CCountryTag.TryParse(node.Get("recipient")?.Value?.Trim('"'), out CCountryTag recipient)
			|| state.Country(actor) is null || state.Country(recipient) is null)
		{
			report.Unread(key, "an offer of a country there is not, left out");
			return;
		}
		EOfferStage? stage = CSaveValues.Whole(node, "type") switch
		{
			0 => EOfferStage.Proposed,
			1 => EOfferStage.Declined,
			2 => EOfferStage.Accepted,
			_ => null,
		};
		if (stage is null)
		{
			report.Unread(key, $"an offer of no stage known, {node.Get("type")?.Value}, left out");
			return;
		}
		long date = CSaveOrders.Hour(node.Get("date")?.Value) ?? state.TotalHours;
		var offer = new COffer(kind, actor, recipient, stage.Value, node.GetBool("value"), date)
		{
			Queued = CSaveOrders.Hour(node.Get("last_command_date")?.Value) ?? date,
		};
		var read = new List<string>(BaseKeys);
		switch (kind)
		{
			case EOfferKind.Trade when node.Get("trade") is { } trade:
				offer = offer with { Deal = ReadDeal(state, trade, actor, recipient) };
				read.Add("trade");
				break;
			case EOfferKind.CallAlly:
				offer = offer with
				{
					Versus = CCountryTag.TryParse(node.Get("versus")?.Value?.Trim('"'), out CCountryTag versus) ? versus : null,
				};
				read.Add("versus");
				break;
			case EOfferKind.ShareTechnology:
				offer = offer with { Category = node.Get("technology")?.Value?.Trim('"') ?? "" };
				read.Add("technology");
				break;
			case EOfferKind.Licence:
				if (node.Get("definition")?.Value?.Trim('"') is { Length: > 0 } type)
				{
					offer = offer with
					{
						Licence = new Sim.Aid.CLicenceTerms(type, (int)CSaveValues.Whole(node, "serial"),
							(int)CSaveValues.Whole(node, "parallel"), CSaveValues.Fixed(node, "money")),
					};
				}
				read.AddRange(new[] { "definition", "serial", "parallel", "money" });
				break;
			// Its claim type: 1 to send, or a host giving it back; 0 a lender taking it back.
			case EOfferKind.Expedition:
				offer = offer with
				{
					Unit = node.Get("unit") is { } unit ? ids.Of(unit) : 0,
					Recall = !offer.Value && node.Get("action") is { } action && CSaveValues.Whole(action.Value) == 0,
				};
				read.AddRange(new[] { "unit", "action" });
				break;
			case EOfferKind.DeclareWar when node.Get("war_goal") is { } goal:
				offer = offer with { WarGoal = CSaveWars.ReadGoalBlock(state, goal, report) };
				read.Add("war_goal");
				break;
		}
		foreach (ClausewitzNode entry in node.Children)
		{
			if (entry.Key is { } other && !read.Contains(other))
			{
				report.Unread(key, other);
			}
		}
		country.PendingOffers.Add(offer);
	}

	/// <summary>An offer to trade's route: who hands over what, both blocks in the goods' order.</summary>
	private static CTradeDeal ReadDeal(CGameState state, ClausewitzNode trade, CCountryTag actor, CCountryTag recipient)
	{
		CCountryTag from = CCountryTag.TryParse(trade.Get("from")?.Value?.Trim('"'), out CCountryTag f) ? f : actor;
		CCountryTag to = CCountryTag.TryParse(trade.Get("to")?.Value?.Trim('"'), out CCountryTag t) ? t : recipient;
		return new CTradeDeal(from, to, CTradeTerms.Of(CSaveDiplomacy.Goods(trade.Get("trade_from")), CSaveDiplomacy.Goods(trade.Get("trade_to"))))
		{
			Start = CSaveOrders.Hour(trade.Get("start_date")?.Value),
		};
	}

	/// <summary>Every offer on the country's list, in its order, after the pair blocks and before the ministers.</summary>
	public static void Write(CSaveTextWriter writer, CGameState state, CCountry country)
	{
		foreach (COffer offer in country.PendingOffers)
		{
			writer.Begin(KeyOf[offer.Kind]);
			writer.Int("type", offer.Stage switch
			{
				EOfferStage.Declined => 1,
				EOfferStage.Accepted => 2,
				_ => 0,
			});
			writer.Quoted("actor", offer.Actor.Value);
			writer.Quoted("recipient", offer.Recipient.Value);
			writer.Date("date", offer.Date);
			writer.Bool("value", offer.Value);
			writer.Date("last_command_date", offer.Queued);
			WriteCarried(writer, state, offer);
			writer.End();
		}
	}

	/// <summary>
	/// What a kind carries, after the base. The trade's block and the call's enemy are in the saves; the rest are in none,
	/// so their keys are the original's loaders' and the order of each kind's is inferred.
	/// </summary>
	private static void WriteCarried(CSaveTextWriter writer, CGameState state, COffer offer)
	{
		switch (offer.Kind)
		{
			case EOfferKind.Trade when offer.Deal is { } deal:
				writer.Begin("trade");
				writer.Quoted("to", deal.To.Value);
				writer.Quoted("from", deal.From.Value);
				writer.Figures("trade_from", Enumerable.Range(0, CGoodsPool.Count).Select(i => new CFixedPoint(deal.Terms.From((EGoods)i))));
				writer.Figures("trade_to", Enumerable.Range(0, CGoodsPool.Count).Select(i => new CFixedPoint(deal.Terms.To((EGoods)i))));
				// The route as a pact: its two countries, and the start of the route a cancellation copies, else none.
				writer.Quoted("first", deal.From.Value);
				writer.Quoted("second", deal.To.Value);
				writer.DateOrNull("start_date", deal.Start);
				writer.End();
				break;
			case EOfferKind.CallAlly when offer.Versus is { } versus:
				writer.Quoted("versus", versus.Value);
				break;
			case EOfferKind.ShareTechnology when offer.Category.Length > 0:
				writer.Quoted("technology", offer.Category);
				break;
			case EOfferKind.Licence when offer.Licence is { } licence:
				writer.Quoted("definition", licence.UnitType);
				writer.Int("serial", licence.Serial);
				writer.Int("parallel", licence.Parallel);
				writer.Fixed("money", licence.Money);
				break;
			case EOfferKind.Expedition:
				writer.Int("action", !offer.Value && offer.Recall ? 0 : 1);
				if (offer.Unit != 0)
				{
					writer.IdPair("unit", CSaveUnitWriter.Pair(state, offer.Unit));
				}
				break;
			case EOfferKind.DeclareWar or EOfferKind.AddWarGoal when offer.WarGoal is { } goal:
				CSaveWars.WriteGoal(writer, goal);
				break;
		}
	}
}
