using System;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A country's ten pools of goods, both ways: an exile's own stockpile, then the day's ledgers so far - what it made at
/// home, what its industry used, the supplies and fuel sent out and back, what its convoys carried out, and what it was
/// paid and paid for allied supply and trade. Each written whatever it holds. See <c>docs/mechanics/savegame.md</c>,
/// section 15.
/// </summary>
public static class CSaveLedgers
{
	/// <summary>The keys, in the order the original writes them.</summary>
	public static readonly string[] Keys =
	{
		"pool", "home", "usage", "to", "back", "convoyed_out", "traded_for", "traded_for_sans_allied", "traded_away",
		"traded_away_sans_allied",
	};

	private static CGoodsPool Of(CCountry country, string key) => key switch
	{
		"pool" => country.PoolInExile,
		"home" => country.HomeProduced,
		"usage" => country.Usage,
		"to" => country.SentTo,
		"back" => country.SentBack,
		"convoyed_out" => country.ConvoyedOut,
		"traded_for" => country.TradedFor,
		"traded_for_sans_allied" => country.TradedForSansAlliedSupply,
		"traded_away" => country.TradedAway,
		"traded_away_sans_allied" => country.TradedAwaySansAlliedSupply,
		_ => throw new ArgumentOutOfRangeException(nameof(key), key, "no pool of that key"),
	};

	public static void Read(CCountry country, ClausewitzNode node)
	{
		foreach (string key in Keys)
		{
			CGoodsPool pool = Of(country, key);
			ClausewitzNode? block = node.Get(key);
			foreach (EGoods goods in Enum.GetValues<EGoods>())
			{
				pool[goods] = CSaveValues.Fixed(block?.Get(CGoodsPool.KeyOf(goods))?.Value);
			}
		}
	}

	public static void Write(CSaveTextWriter writer, CCountry country)
	{
		foreach (string key in Keys)
		{
			CSaveWriter.WritePool(writer, key, Of(country, key));
		}
	}
}
