using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Trade;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A save's top-level <c>diplomacy</c>: every pact standing - guarantee, alliance, puppet (<c>vassal</c>), NAP, embargo,
/// influence and alignment - and every trade route, each with its two countries and its start. A trade route's convoy is
/// one of its owner's, found by its number once every country is read. See <c>docs/mechanics/savegame.md</c>,
/// section 6.
/// </summary>
public static class CSaveDiplomacy
{
	/// <summary>Reads every pact; answers each trade route with the number of its convoy, nought for none.</summary>
	public static List<(CTradeRoute Route, int Convoy)> Read(CGameState state, ClausewitzNode block, CSaveReport report)
	{
		var routes = new List<(CTradeRoute, int)>();
		foreach (ClausewitzNode pact in block.Children)
		{
			if (pact.Key is not { } kind || !pact.IsBlock)
			{
				continue;
			}
			if (Tag(state, pact, "first") is not { } first || Tag(state, pact, "second") is not { } second)
			{
				report.Unread("diplomacy", $"a {kind} of a country there is not");
				continue;
			}
			long start = CSaveOrders.Hour(pact.Get("start_date")?.Value) ?? 0;
			switch (kind)
			{
				case "guarantee":
					// Its end is twenty years after its start, worked out from that (agreements.md, section 6).
					state.Guarantees.Add(new CGuarantee(first.Tag, second.Tag, start));
					Unread(pact, report, "first", "second", "start_date", "end_date");
					break;
				case "alliance":
					state.Alliances.Add(new CAlliance(first.Tag, second.Tag));
					Unread(pact, report, "first", "second", "start_date");
					break;
				case "vassal":
					second.Overlord = first.Tag;
					Unread(pact, report, "first", "second", "start_date");
					break;
				case "nap":
					state.Naps.Add(new CNap(first.Tag, second.Tag, start));
					Unread(pact, report, "first", "second", "start_date");
					break;
				case "embargo":
					state.Embargoes.Add(new CEmbargo(first.Tag, second.Tag));
					Unread(pact, report, "first", "second", "start_date");
					break;
				// The faction an influence works for is its influencer's, and an alignment's its leader's, as each is
				// made (agreements.md, sections 9 and 10).
				case "influence":
					state.Influences.Add(new CInfluence(first.Tag, second.Tag, first.Faction, start));
					Unread(pact, report, "first", "second", "start_date");
					break;
				case "align":
					state.Aligns.Add(new CAlign(first.Tag, second.Tag, second.Faction, start));
					Unread(pact, report, "first", "second", "start_date");
					break;
				case "trade":
					routes.Add(ReadRoute(state, pact, start, report));
					break;
				default:
					report.Unread("diplomacy", kind);
					break;
			}
		}
		return routes;
	}

	/// <summary>A route: who hands over what each day, both blocks in the goods' order, and its convoy's number.</summary>
	private static (CTradeRoute, int) ReadRoute(CGameState state, ClausewitzNode pact, long start, CSaveReport report)
	{
		CCountryTag from = Tag(state, pact, "from")?.Tag ?? Tag(state, pact, "first")!.Tag;
		CCountryTag to = Tag(state, pact, "to")?.Tag ?? Tag(state, pact, "second")!.Tag;
		var route = new CTradeRoute(from, to, CTradeTerms.Of(Goods(pact.Get("trade_from")), Goods(pact.Get("trade_to"))), start);
		Unread(pact, report, "first", "second", "start_date", "from", "to", "trade_from", "trade_to", "convoy");
		// A convoy's id is of its own count, type 42, and its serial is its number here.
		return (route, pact.Get("convoy")?.GetInt("id") ?? 0);
	}

	private static long[] Goods(ClausewitzNode? block)
	{
		var amounts = new long[CGoodsPool.Count];
		List<ClausewitzNode> values = block?.Children ?? new List<ClausewitzNode>();
		for (int i = 0; i < amounts.Length && i < values.Count; i++)
		{
			amounts[i] = CSaveValues.Fixed(values[i].Value).Raw;
		}
		return amounts;
	}

	/// <summary>Every key of a pact but those read, reported under the pact's kind.</summary>
	private static void Unread(ClausewitzNode pact, CSaveReport report, params string[] read)
	{
		foreach (ClausewitzNode entry in pact.Children)
		{
			if (entry.Key is { } key && System.Array.IndexOf(read, key) < 0)
			{
				report.Unread(pact.Key!, key);
			}
		}
	}

	private static CCountry? Tag(CGameState state, ClausewitzNode pact, string key)
	{
		return CCountryTag.TryParse(pact.Get(key)?.Value?.Trim('"'), out CCountryTag tag) ? state.Country(tag) : null;
	}
}
