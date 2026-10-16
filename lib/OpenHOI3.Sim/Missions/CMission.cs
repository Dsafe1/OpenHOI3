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

	/// <summary>An invasion: the sea province the landing is made from, or 0 where none was found.</summary>
	public int LandingSea { get; set; }

	// ---- an air mission's: docs/mechanics/aircombat.md

	/// <summary>An air mission flies by day only.</summary>
	public const int ByDay = 0;

	/// <summary>An air mission flies by night only.</summary>
	public const int ByNight = 1;

	/// <summary>An air mission flies day and night.</summary>
	public const int DayAndNight = 2;

	/// <summary>What <see cref="EndHour"/> is for a mission that never ends.</summary>
	public const long NoEnd = long.MaxValue;

	/// <summary>An air mission: the provinces it covers, in the order they were given.</summary>
	public List<int> Provinces { get; } = new();

	/// <summary>An air mission: when it flies - by day, by night, or both.</summary>
	public int Time { get; set; } = DayAndNight;

	/// <summary>An air mission: the hour of the game from which it may fly.</summary>
	public long StartHour { get; set; }

	/// <summary>An air mission: the hour of the game after which it is over once the unit is home.</summary>
	public long EndHour { get; set; } = NoEnd;

	/// <summary>An air mission: the province it is flying to, or 0.</summary>
	public int Destination { get; set; }

	/// <summary>An intercept: the province where it means to meet a target that is on its way in, or 0.</summary>
	public int Meeting { get; set; }

	/// <summary>An intercept: the provinces it watches - its mission's and two steps around them - in the order they were added.</summary>
	public List<int> Watched { get; } = new();

	/// <summary>The same provinces, for asking whether one is among them.</summary>
	public HashSet<int> WatchedSet { get; } = new();
}
