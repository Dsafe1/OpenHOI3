using System.Collections.Generic;

namespace OpenHOI3.Content.Settings;

/// <summary>
/// The player's settings, as the original keeps them in <c>settings.txt</c> in the Documents folder: what of them is
/// read. See <c>docs/mechanics/settings.md</c>.
/// </summary>
public sealed class CUserSettings
{
	/// <summary>What a player with no <c>outliner</c> key has. Before <see cref="Default"/>, which is made from it.</summary>
	public static readonly IReadOnlyList<int> DefaultOutliner = new[] { 10, 4, 5, 6 };

	/// <summary>The settings a player with no file has: the original's own defaults where they were read.</summary>
	public static CUserSettings Default { get; } = new();

	/// <summary>The file they were read from, or none.</summary>
	public string? Path { get; init; }

	/// <summary><c>gui</c>'s <c>language</c>, as the file writes it: <c>l_english</c>.</summary>
	public string Language { get; init; } = "l_english";

	/// <summary>
	/// <c>mapRenderingOptions</c>' <c>alwaysCounters</c> as the file says it. Only <c>yes</c> is supported: the map always
	/// shows counters (<c>docs/compatibility.md</c>, entry 12).
	/// </summary>
	public bool AlwaysCounters { get; init; } = true;

	/// <summary><c>counter_scale</c>: 1 in a new game's settings. What it does is not read, and nothing uses it yet.</summary>
	public float CounterScale { get; init; } = 1f;

	/// <summary><c>counter_distance</c>: 100 in a new game's settings. Nothing uses it yet.</summary>
	public float CounterDistance { get; init; } = 100f;

	/// <summary>
	/// <c>outliner</c>: the outliner's sections switched on, 1 to 9, and 10 where it is open, in no order. With no key the
	/// original's own: open, with land units, fleets and air units. See <c>docs/mechanics/outliner.md</c>, section 3.
	/// </summary>
	public IReadOnlyList<int> Outliner { get; init; } = DefaultOutliner;
}
