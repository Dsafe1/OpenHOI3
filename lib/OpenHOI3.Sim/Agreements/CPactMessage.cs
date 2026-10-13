using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Agreements;

/// <summary>
/// What a pact's happening tells the players: three message types, for the actor, for the recipient
/// and for everybody else - empty where nobody of the three is told - and the two countries; for an
/// alignment, its faction and the key of what ended. See <c>docs/mechanics/agreements.md</c>, section 8.
/// </summary>
public sealed record CPactMessage(string We, string They, string Other, CCountryTag Actor, CCountryTag Recipient)
{
	/// <summary>The faction an influence or an alignment is towards, by its tag; empty for the rest.</summary>
	public string Faction { get; init; } = "";

	/// <summary>The key of what has ended, for <c>DIPLOEXPIRE</c>'s <c>$ACTION$</c>; empty for the rest.</summary>
	public string Action { get; init; } = "";

	/// <summary>
	/// A trade's route, for the goods its messages name (<c>trade.md</c>, section 10): set by a trade's
	/// acceptance and its cancellation, none for the rest.
	/// </summary>
	public Trade.CTradeDeal? Deal { get; init; }

	/// <summary>
	/// Whether its ACTOR and RECIPIENT are written the other way round, so that what the recipient is told
	/// reads from the actor's side - lend-lease's cancellations (<c>aid.md</c>, section 3).
	/// </summary>
	public bool TurnedRound { get; init; }

	/// <summary>Values of its own kind, after the rest: a licence's NUM and NAME, and its ACCEPTED or DECLINED.</summary>
	public System.Collections.Generic.IReadOnlyList<(string Name, string Value)> Values { get; init; } =
		System.Array.Empty<(string, string)>();
}
