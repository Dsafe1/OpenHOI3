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

	/// <summary>
	/// The one country told, with the key <see cref="We"/>, where it is not the actor or the recipient - a
	/// faction's member brought into a war (<c>war.md</c>, section 6); none for the rest.
	/// </summary>
	public CCountryTag? Told { get; init; }

	/// <summary>Who reports it, <c>$MESSENGER$</c>: the foreign office, or the general staff for a war.</summary>
	public string Messenger { get; init; } = "MESSAGE_HEAD_CHAN";

	/// <summary>The province it is about, where it is about one; a Goto goes there and otherwise to a capital.</summary>
	public int Province { get; init; }

	/// <summary>An election's parties' scores, by ideology, for the popup's pie; none for anything else.</summary>
	public long[]? Scores { get; init; }

	/// <summary>
	/// The unit it is about, by its number, for a Goto that selects it - an expeditionary force returned
	/// (<c>expeditions.md</c>, section 3); 0 for the rest.
	/// </summary>
	public int Unit { get; init; }

	/// <summary>
	/// The sound the original raises with it, heard by whoever is told its first key, <see cref="We"/> - the actor, or
	/// <see cref="Told"/> where that is set: <see cref="Success"/> or <see cref="Failure"/> for the answer to one of the twelve
	/// offers that sound one (<c>docs/mechanics/sound.md</c>, section 7); empty for the rest.
	/// </summary>
	public string Sound { get; init; } = "";

	public const string Success = "diplomaticsuccess";
	public const string Failure = "diplomaticfailure";

	/// <summary>Values of its own kind, after the rest: a licence's NUM and NAME, and its ACCEPTED or DECLINED.</summary>
	public System.Collections.Generic.IReadOnlyList<(string Name, string Value)> Values { get; init; } =
		System.Array.Empty<(string, string)>();
}
