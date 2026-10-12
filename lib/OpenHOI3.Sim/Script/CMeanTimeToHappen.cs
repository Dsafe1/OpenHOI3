using System.Collections.Generic;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// An event's <c>mean_time_to_happen</c>, an option's <c>ai_chance</c> or a decision's
/// <c>ai_will_do</c>: a number of days and the modifiers on it. See
/// <c>docs/mechanics/script.md</c>, section 4.
/// </summary>
public sealed class CMeanTimeToHappen
{
	/// <summary>
	/// Whole days - the last of <c>days</c>, <c>factor</c>, <c>months</c> (30 each) and
	/// <c>years</c> (365 each) written, 1 where none is.
	/// </summary>
	public int Days { get; set; } = 1;

	/// <summary>
	/// An <c>ai_chance</c>'s own <c>factor</c>, read with its decimals, to three places: one
	/// where none is written. Only an option's <c>ai_chance</c> has it; elsewhere a top-level
	/// <c>factor</c> is <see cref="Days"/>.
	/// </summary>
	public CFixedPoint AiFactor { get; set; } = CFixedPoint.One;

	public List<CMeanTimeToHappenModifier> Modifiers { get; } = new();
}
