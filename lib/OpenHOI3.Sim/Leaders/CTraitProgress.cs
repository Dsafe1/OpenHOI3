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

	/// <summary>
	/// Each gainable trait's progress, out of <see cref="Whole"/>, in the order each was first made: kept by the definition
	/// and not by the trait, as the original's map is - two definitions of one trait, BlackICE's <c>night_flyer</c> for land
	/// combat and for land bombing, are counted apart and a save writes both under the trait's name (savegame.md, section 5).
	/// </summary>
	public Dictionary<CGainableTrait, int> Progress { get; } = new();

	/// <summary>A definition's progress.</summary>
	public int Of(CGainableTrait gainable)
	{
		return Progress.GetValueOrDefault(gainable);
	}
}
