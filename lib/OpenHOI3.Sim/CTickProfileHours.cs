using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// How long a profile's hours took (<see cref="CTickProfile.Hours"/>), in stopwatch ticks: the hours that turned no
/// day and those that did, each counted, added up and with its longest, and the last hours one by one, the oldest
/// first.
/// </summary>
public readonly record struct CTickProfileHours(long Plain, long PlainTicks, long PlainMost, long Turning, long TurningTicks,
	long TurningMost, IReadOnlyList<long> Last);
