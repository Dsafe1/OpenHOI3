using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Convoys;

/// <summary>
/// What a raid on a convoy tells one country's player: the message type, the country told, the sea province, the two
/// countries the message shows, the convoy's two ends, and what was sunk or who found it. Its words are made where it
/// is shown. See <c>docs/mechanics/navalmissions.md</c>, section 8.2.
/// </summary>
/// <param name="Type">
/// <see cref="Lost"/> for the convoy's owner, <see cref="Sank"/> for the raider's country, <see cref="TooStrong"/> for a
/// raider that found a convoy and was too weak for its escorts.
/// </param>
/// <param name="Told">The country whose player is told; nobody else is.</param>
/// <param name="First">The message's first country: the convoy's owner, or the raider's country where it was too strong.</param>
/// <param name="Second">The other: the raider's country, or the convoy's owner where it was too strong.</param>
public sealed record CConvoyRaidMessage(string Type, CCountryTag Told, int Province, CCountryTag First, CCountryTag Second,
	int ConvoyStart, int ConvoyEnd)
{
	public const string Lost = "CONVOY_ATTACK_ON_US";
	public const string Sank = "WE_ATTACK_CONVOY";
	public const string TooStrong = "CONVOY_TOO_STRONG";

	/// <summary>The transports and the escorts the convoy lost, as many as it had at most.</summary>
	public int Transports { get; init; }

	public int Escorts { get; init; }

	/// <summary>The raider's name, which a convoy too strong names.</summary>
	public string Unit { get; init; } = "";
}
