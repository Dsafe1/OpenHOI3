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
			// The end the history gave it, which nothing here acts on and the writer gives back (savegame.md, section 10).
			long? end = CSaveOrders.Hour(pact.Get("end_date")?.Value);
			switch (kind)
			{
				case "guarantee":
					// It ends twenty years after its start, worked out from that (agreements.md, section 6).
					state.Guarantees.Add(new CGuarantee(first.Tag, second.Tag, start) { Made = state.NextPact(), End = end });
					Unread(pact, report, "first", "second", "start_date", "end_date");
					break;
				case "alliance":
					state.Alliances.Add(new CAlliance(first.Tag, second.Tag) { Made = state.NextPact(), Start = start, End = end });
					Unread(pact, report, "first", "second", "start_date", "end_date");
					break;
				case "vassal":
					second.Overlord = first.Tag;
					second.PuppetMade = state.NextPact();
					second.PuppetStart = start;
					second.PuppetEnd = end;
					Unread(pact, report, "first", "second", "start_date", "end_date");
					break;
				case "nap":
					state.Naps.Add(new CNap(first.Tag, second.Tag, start)
					{
						Made = state.NextPact(),
						End = end,
						Balance = pact.Get("balance") is null ? CNapBalance.Unmeasured : CSaveValues.Fixed(pact, "balance").Raw,
						OurPower = pact.GetInt("our_power"),
						TheirPower = pact.GetInt("their_power"),
					});
					Unread(pact, report, "balance", "our_power", "their_power", "first", "second", "start_date", "end_date");
					break;
				case "embargo":
					state.Embargoes.Add(new CEmbargo(first.Tag, second.Tag) { Made = state.NextPact(), Start = start });
					Unread(pact, report, "first", "second", "start_date");
					break;
				// The faction an influence works for is its influencer's, and an alignment's its leader's, as each is
				// made (agreements.md, sections 9 and 10).
				case "influence":
					state.Influences.Add(new CInfluence(first.Tag, second.Tag, first.Faction, start) { Made = state.NextPact() });
					Unread(pact, report, "first", "second", "start_date");
					break;
				case "align":
					state.Aligns.Add(new CAlign(first.Tag, second.Tag, second.Faction, start) { Made = state.NextPact() });
					Unread(pact, report, "first", "second", "start_date");
					break;
				case "trade":
					routes.Add(ReadRoute(state, pact, start, report));
					break;
				// A casus belli the first holds on the second (war.md, section 2). One already run out the original files on
				// no record, and it is left out here.
				case "casus_belli":
					ReadCasusBelli(state, pact, first, second, start, end, report);
					Unread(pact, report, "type", "first", "second", "start_date", "end_date");
					break;
				default:
					report.Unread("diplomacy", kind);
					break;
			}
		}
		return routes;
	}

	/// <summary>What a casus belli of the null type is saved as: the key of the original's first type.</summary>
	public const string NullCasusBelli = "nocasusbelli";

	/// <summary>A casus belli: its type by name, the null type's key or a type of the install's; any other name is reported.</summary>
	private static void ReadCasusBelli(CGameState state, ClausewitzNode pact, CCountry first, CCountry second, long start,
		long? end, CSaveReport report)
	{
		string name = pact.Get("type")?.Value?.Trim('"') ?? NullCasusBelli;
		Sim.Wars.CCasusBelliType type = Sim.Wars.CCasusBellis.Find(state, name);
		if (!type.IsReal && name != NullCasusBelli)
		{
			report.Unread("casus_belli", $"a type of no name known, {name}");
			return;
		}
		if (end is { } ends && ends < state.TotalHours)
		{
			return;
		}
		CGameState.Status(first, second).CasusBelli.Add(new Sim.Wars.CCasusBelli(type.Index, end)
		{
			Start = pact.Get("start_date") is null ? null : start,
			Made = state.NextPact(),
		});
	}

	/// <summary>A route: who hands over what each day, both blocks in the goods' order, and its convoy's number.</summary>
	private static (CTradeRoute, int) ReadRoute(CGameState state, ClausewitzNode pact, long start, CSaveReport report)
	{
		CCountryTag from = Tag(state, pact, "from")?.Tag ?? Tag(state, pact, "first")!.Tag;
		CCountryTag to = Tag(state, pact, "to")?.Tag ?? Tag(state, pact, "second")!.Tag;
		var route = new CTradeRoute(from, to, CTradeTerms.Of(Goods(pact.Get("trade_from")), Goods(pact.Get("trade_to"))), start)
		{
			Made = state.NextPact(),
		};
		Unread(pact, report, "first", "second", "start_date", "from", "to", "trade_from", "trade_to", "convoy");
		// A convoy's id is of its own count, type 42, and its serial is its number here.
		return (route, pact.Get("convoy")?.GetInt("id") ?? 0);
	}

	/// <summary>A block of the seven goods' amounts, in their order; a good it leaves out is nought.</summary>
	internal static long[] Goods(ClausewitzNode? block)
	{
		var amounts = new long[CGoodsPool.Count];
		List<ClausewitzNode> values = block?.Children ?? new List<ClausewitzNode>();
		for (int i = 0; i < amounts.Length && i < values.Count; i++)
		{
			amounts[i] = CSaveValues.Fixed(values[i].Value).Raw;
		}
		return amounts;
	}

	/// <summary>
	/// <c>diplomacy</c>: every pact standing in the order it was made, as the original keeps them on one list - each
	/// kind's keys in the original's order (<c>savegame.md</c>, section 10).
	/// </summary>
	public static void Write(CSaveTextWriter writer, CGameState state)
	{
		var pacts = new List<(long Made, Action Write)>();
		foreach (CGuarantee guarantee in state.Guarantees)
		{
			pacts.Add((guarantee.Made, () => Pact(writer, "guarantee", guarantee.Guarantor, guarantee.Guaranteed, guarantee.Start, guarantee.End)));
		}
		foreach (CAlliance alliance in state.Alliances)
		{
			pacts.Add((alliance.Made, () => Pact(writer, "alliance", alliance.First, alliance.Second, alliance.Start, alliance.End)));
		}
		foreach (CCountry puppet in state.Countries)
		{
			if (puppet.Overlord is { } overlord)
			{
				pacts.Add((puppet.PuppetMade, () => Pact(writer, "vassal", overlord, puppet.Tag, puppet.PuppetStart, puppet.PuppetEnd)));
			}
		}
		foreach (CNap nap in state.Naps)
		{
			pacts.Add((nap.Made, () =>
			{
				writer.Begin("nap");
				writer.Fixed("balance", new CFixedPoint(nap.Balance));
				writer.Int("our_power", nap.OurPower);
				writer.Int("their_power", nap.TheirPower);
				writer.Quoted("first", nap.First.Value);
				writer.Quoted("second", nap.Second.Value);
				if (nap.End is { } ends)
				{
					writer.Date("end_date", ends);
				}
				writer.DateOrNull("start_date", nap.Start);
				writer.End();
			}));
		}
		foreach (CEmbargo embargo in state.Embargoes)
		{
			pacts.Add((embargo.Made, () => Pact(writer, "embargo", embargo.First, embargo.Second, embargo.Start, null)));
		}
		foreach (CInfluence influence in state.Influences)
		{
			pacts.Add((influence.Made, () => Pact(writer, "influence", influence.Influencer, influence.Influenced, influence.Start, null)));
		}
		foreach (CAlign align in state.Aligns)
		{
			pacts.Add((align.Made, () => Pact(writer, "align", align.Aligner, align.Leader, align.Start, null)));
		}
		foreach (CTradeRoute route in state.TradeRoutes)
		{
			pacts.Add((route.Made, () => WriteRoute(writer, route)));
		}
		foreach (CCountry holder in state.Countries)
		{
			foreach (CCountry target in state.Countries)
			{
				foreach (Sim.Wars.CCasusBelli casusBelli in CGameState.Status(holder, target).CasusBelli)
				{
					pacts.Add((casusBelli.Made, () => WriteCasusBelli(writer, state, holder.Tag, target.Tag, casusBelli)));
				}
			}
		}
		writer.Begin("diplomacy");
		foreach ((long _, Action write) in pacts.OrderBy(pact => pact.Made))
		{
			write();
		}
		writer.End();
	}

	/// <summary>A pact's two countries, the end the history gave it where one, and its start.</summary>
	private static void Pact(CSaveTextWriter writer, string kind, CCountryTag first, CCountryTag second, long? start, long? end)
	{
		writer.Begin(kind);
		writer.Quoted("first", first.Value);
		writer.Quoted("second", second.Value);
		if (end is { } ends)
		{
			writer.Date("end_date", ends);
		}
		writer.DateOrNull("start_date", start);
		writer.End();
	}

	/// <summary>A casus belli: its type's name, the null type's key for the null type, then as a pact.</summary>
	private static void WriteCasusBelli(CSaveTextWriter writer, CGameState state, CCountryTag holder, CCountryTag target,
		Sim.Wars.CCasusBelli casusBelli)
	{
		writer.Begin("casus_belli");
		Sim.Wars.CCasusBelliType? type = state.CasusBelliTypes.FirstOrDefault(each => each.Index == casusBelli.Type);
		writer.Quoted("type", type is { IsReal: true } ? type.Name : NullCasusBelli);
		writer.Quoted("first", holder.Value);
		writer.Quoted("second", target.Value);
		if (casusBelli.End is { } ends)
		{
			writer.Date("end_date", ends);
		}
		writer.DateOrNull("start_date", casusBelli.Start);
		writer.End();
	}

	/// <summary>A route: its two ends, both blocks in the goods' order, its convoy where it has one, then as a pact.</summary>
	private static void WriteRoute(CSaveTextWriter writer, CTradeRoute route)
	{
		writer.Begin("trade");
		writer.Quoted("to", route.To.Value);
		writer.Quoted("from", route.From.Value);
		writer.Figures("trade_from", Enumerable.Range(0, CGoodsPool.Count).Select(i => new CFixedPoint(route.Terms.From((EGoods)i))));
		writer.Figures("trade_to", Enumerable.Range(0, CGoodsPool.Count).Select(i => new CFixedPoint(route.Terms.To((EGoods)i))));
		if (route.Convoy is { } convoy)
		{
			writer.IdPair("convoy", convoy.Id, CSaveConvoys.IdType);
		}
		writer.Quoted("first", route.From.Value);
		writer.Quoted("second", route.To.Value);
		writer.Date("start_date", route.Start);
		writer.End();
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
