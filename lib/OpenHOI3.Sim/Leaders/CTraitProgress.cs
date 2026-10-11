using System.Collections.Generic;

namespace OpenHOI3.Sim.Leaders;

/// <summary>
/// How far a leader is towards each trait he may earn, and the hours of battle he has
/// banked and not yet had counted. See <c>docs/mechanics/leaders.md</c>, section 11.
/// </summary>
public sealed class CTraitProgress
{
	/// <summary>A trait is earned at this much progress: a hundred, in thousandths.</summary>
	public const int Whole = 100000;

	/// <summary>The hours of battle banked since they were last counted.</summary>
	public int Duration { get; set; }

	/// <summary>The hour of the day last banked, so that an hour is banked once however many battles ask; -1 for none.</summary>
	public int CurrentHour { get; set; } = -1;

	/// <summary>Each trait's progress, out of <see cref="Whole"/>.</summary>
	public Dictionary<CTrait, int> Progress { get; } = new();

	public int Of(CTrait trait)
	{
		return Progress.GetValueOrDefault(trait);
	}
}
