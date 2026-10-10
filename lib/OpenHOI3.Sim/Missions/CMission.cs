using System.Collections.Generic;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// What a fleet's order keeps from hour to hour besides its kind and its province: how
/// bold it is, whether it has been told to go home, and each mission's own place in what
/// it is doing. A new one is made with every order. See
/// <c>docs/mechanics/navalmissions.md</c>.
/// </summary>
public sealed class CMission
{
	public const int Aggressive = 0;
	public const int Defensive = 1;
	public const int Passive = 2;

	/// <summary>What a unit is making for before it has been told: no province.</summary>
	public const int NoHeading = -1;

	/// <summary>0 aggressive, 1 defensive, 2 passive. An order made with nothing said is defensive.</summary>
	public int Stance { get; set; } = Defensive;

	/// <summary>From 0 to 4. A reserve sees to the fleets of the highest first.</summary>
	public int Priority { get; set; }

	/// <summary>
	/// The order has told its fleet to return to base. Nothing takes that back: it stays
	/// for as long as the order does.
	/// </summary>
	public bool ReturnToBase { get; set; }

	/// <summary>The fleet a patrol shadows or an interceptor is after, or null.</summary>
	public CUnit? Target { get; set; }

	/// <summary>The target, where there is one and it is still in the game.</summary>
	public CUnit? LiveTarget => Target is { IsRemoved: false } target ? target : null;

	/// <summary>A patrol and a raider: which of the province's neighbours is next on the round.</summary>
	public int Round { get; set; }

	/// <summary>A patrol and a raider: the hours until the next try at a convoy.</summary>
	public int RaidCountdown { get; set; }

	/// <summary>A sortie: the provinces it sails to, in order.</summary>
	public List<int> Waypoints { get; } = new();

	/// <summary>A sortie: the waypoint it is making for, or 0 before it has one.</summary>
	public int Current { get; set; }

	/// <summary>An escort: the number of the convoy it sails with.</summary>
	public int Convoy { get; set; }

	/// <summary>An escort: the province it is making for.</summary>
	public int Heading { get; set; } = NoHeading;

	/// <summary>An escort: the two ends of the part of the route it sails, or 0.</summary>
	public int BeatFirst { get; set; }

	public int BeatLast { get; set; }

	/// <summary>An escort: which end it is bound for - the first when set.</summary>
	public bool Turned { get; set; }
}
