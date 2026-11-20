using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Multiplayer;

namespace OpenHOI3.Multiplayer;

/// <summary>
/// A machine connected to a host, as the host keeps it across the games sent and loaded (<see cref="CNetHost"/>): from
/// its first line, through the country it is given, to its part in the session of the game now played.
/// </summary>
public sealed class CNetPeer
{
	public CNetPeer(CLineLink link)
	{
		Link = link;
	}

	public CLineLink Link { get; }

	/// <summary>The name its player gave; null until it has been let in.</summary>
	public string? Name { get; set; }

	/// <summary>The country its player asked for; null until it has said.</summary>
	public CCountryTag? Wanted { get; set; }

	/// <summary>The country its player plays; null until a game has been sent it with that country played.</summary>
	public CCountryTag? Country { get; set; }

	/// <summary>The sending of the game it was last offered; null where it has been offered none.</summary>
	public CGameSender? Sender { get; set; }

	/// <summary>Its part in the session of the game now played; null between a game's offer and the host's own load of it.</summary>
	public CHostPeer? HostPeer { get; set; }

	/// <summary>Whether it is still taking or loading the game it was offered: nothing it says before it has is of that game's play.</summary>
	public bool Loading => Sender is { Loaded: false };

	/// <summary>Whether it has been let in, has said what it would play, and waits for a game to be sent it.</summary>
	public bool WaitsToJoin => Name is not null && (Wanted is not null || AnyCountry) && Country is null;

	/// <summary>Whether it named no country and is to be given one.</summary>
	public bool AnyCountry { get; set; }

	/// <summary>The lobby's line it was last sent, while the host is in its lobby.</summary>
	public string? LobbySent { get; set; }
}
