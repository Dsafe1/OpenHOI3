using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Transport;

/// <summary>
/// The landings one country is making on a player's ground, in the order they were noted, and whether one is new since
/// the player was last told. See <c>docs/mechanics/transport.md</c>, section 11.
/// </summary>
public sealed class CInvasionWarning
{
	public CInvasionWarning(CCountryTag player, CCountryTag invader)
	{
		Player = player;
		Invader = invader;
	}

	/// <summary>The player whose ground it is.</summary>
	public CCountryTag Player { get; }

	/// <summary>The country landing.</summary>
	public CCountryTag Invader { get; }

	public List<CUnit> Units { get; } = new();

	/// <summary>Something new to tell.</summary>
	public bool IsNew { get; set; }
}
