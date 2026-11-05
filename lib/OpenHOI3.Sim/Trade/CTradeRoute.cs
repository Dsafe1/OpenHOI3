using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Trade;

/// <summary>
/// A trade route: its <c>from</c> - who proposed it - and its <c>to</c>, its terms, the hour it began,
/// the convoy that serves it where the two need one, and the day's state - whether it could not run
/// and why. Nothing of the day's state is kept in a save. See <c>docs/mechanics/trade.md</c>,
/// sections 1, 5 and 6.
/// </summary>
public sealed class CTradeRoute
{
	public CTradeRoute(CCountryTag from, CCountryTag to, CTradeTerms terms, long start)
	{
		From = from;
		To = to;
		Terms = terms;
		Start = start;
	}

	public CCountryTag From { get; }

	public CCountryTag To { get; }

	public CTradeTerms Terms { get; }

	public long Start { get; }

	/// <summary>Its number among the pacts (<see cref="CGameState.NextPact"/>).</summary>
	public long Made { get; init; }

	/// <summary>The convoy serving it; none for a route over land, or one never made or since lost.</summary>
	public CConvoy? Convoy { get; set; }

	/// <summary>It could not run on its last day.</summary>
	public bool Inactive { get; set; }

	/// <summary>Why: <c>TRADE_INACTIVE_CONVOY</c> or <c>TRADE_INACTIVE_NO_RES</c>; empty while it runs.</summary>
	public string InactiveKey { get; set; } = "";

	/// <summary>The country the reason names.</summary>
	public CCountryTag? InactiveCountry { get; set; }

	/// <summary>The good the reason names, for <c>TRADE_INACTIVE_NO_RES</c>.</summary>
	public EGoods InactiveGoods { get; set; }

	/// <summary>The last hour it was found inactive; <see cref="long.MinValue"/> where never.</summary>
	public long LastInactive { get; set; } = long.MinValue;

	/// <summary>Whether it joins the two countries, either way round.</summary>
	public bool Joins(CCountryTag a, CCountryTag b) => (From == a && To == b) || (From == b && To == a);

	public override string ToString() => $"{From}->{To} {Terms}";
}
