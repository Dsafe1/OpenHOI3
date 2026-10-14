using System.Collections.Generic;

namespace OpenHOI3.Sim.Politics;

/// <summary>
/// A country's strategic warfare: three week-long lists of what was done to it or for it - convoys, allies,
/// bombing - each an hour and a value, and each list's sum, held to -2.0..2.0, its impact on national unity a
/// month. Saved as <c>strategic_warfare</c>. See <c>docs/mechanics/politics.md</c>, section 6.
/// </summary>
public sealed class CStrategicWarfare
{
	/// <summary>How long an entry counts: a week.</summary>
	public const long KeptHours = 168;

	/// <summary>The most an impact may be either way.</summary>
	public const long MostImpact = 2000;

	public List<(long Hour, long Value)> Convoys { get; } = new();

	public List<(long Hour, long Value)> Allies { get; } = new();

	public List<(long Hour, long Value)> Bombing { get; } = new();

	public long ConvoyImpact { get; set; }

	public long AlliesImpact { get; set; }

	public long BombingImpact { get; set; }
}
