using System.Collections.Generic;

namespace OpenHOI3.Content.Settings;

/// <summary>
/// The player's settings, as the original keeps them in <c>settings.txt</c> in the Documents folder: every key its writer
/// writes, so that what is read is written back whole. The defaults are the original's constructors', for a player with no
/// file. A record, so that the game options window makes a changed copy. See <c>docs/mechanics/settings.md</c>.
/// </summary>
public sealed record CUserSettings
{
	/// <summary>What a player with no <c>outliner</c> key has. Before <see cref="Default"/>, which is made from it.</summary>
	public static readonly IReadOnlyList<int> DefaultOutliner = new[] { 10, 4, 5, 6 };

	/// <summary>What a player with no <c>categories</c> key has: six noughts. What they mean is not read.</summary>
	public static readonly IReadOnlyList<int> DefaultCategories = new[] { 0, 0, 0, 0, 0, 0 };

	/// <summary>The settings a player with no file has: the original's own defaults.</summary>
	public static CUserSettings Default { get; } = new();

	/// <summary>The file they were read from, or none.</summary>
	public string? Path { get; init; }

	/// <summary><c>gui</c>'s <c>language</c>, as the file writes it: <c>l_english</c>.</summary>
	public string Language { get; init; } = "l_english";

	/// <summary><c>gui</c>'s <c>has_set_language</c>, written only where set.</summary>
	public bool HasSetLanguage { get; init; }

	// graphics = { ... }. The original's constructor takes the desktop's size, which nothing here asks; 1024 by 768 is
	// the least it allows.
	public int Width { get; init; } = 1024;

	public int Height { get; init; } = 768;

	public int RefreshRate { get; init; } = 60;

	public bool FullScreen { get; init; } = true;

	public bool Shadows { get; init; }

	public int ShadowSize { get; init; } = 2048;

	/// <summary><c>multi_sampling</c>: the samples a pixel, nought for none.</summary>
	public int MultiSampling { get; init; }

	public int AnisotropicFiltering { get; init; }

	/// <summary><c>gamma</c>: 50 the screen as it is; a fiftieth of it is the brightness scale.</summary>
	public float Gamma { get; init; } = 50f;

	/// <summary><c>adapter</c>, written only where not nought.</summary>
	public int Adapter { get; init; }

	/// <summary><c>vsync</c>, written only where off.</summary>
	public bool Vsync { get; init; } = true;

	/// <summary><c>debugmonitor</c>, written only where on and the adapter nought.</summary>
	public bool DebugMonitor { get; init; }

	// The eight figures of nought to a hundred, in the file's order.
	public float SoundFxVolume { get; init; } = 100f;

	public float MusicVolume { get; init; } = 100f;

	public float ScrollSpeed { get; init; } = 50f;

	/// <summary>Read and written, and read by nothing else in the original.</summary>
	public float CameraRotationSpeed { get; init; } = 50f;

	public float ZoomSpeed { get; init; } = 50f;

	public float MouseSpeed { get; init; } = 50f;

	public float MasterVolume { get; init; } = 100f;

	/// <summary>Read and written, and read by nothing else in the original.</summary>
	public float AmbientVolume { get; init; } = 50f;

	/// <summary><c>pause_on_event</c>, written only where off.</summary>
	public bool PauseOnEvent { get; init; } = true;

	// mapRenderingOptions = { ... }
	public bool RenderTrees { get; init; } = true;

	public bool OnMap { get; init; } = true;

	public bool SimpleWater { get; init; }

	/// <summary>
	/// <c>alwaysCounters</c> as the file says it. Only <c>yes</c> is supported: the map always shows counters
	/// (<c>docs/compatibility.md</c>, entry 12). The original's constructor says no; ours yes, which is all the map does.
	/// </summary>
	public bool AlwaysCounters { get; init; } = true;

	/// <summary><c>counter_distance</c>: 100 in a new install's settings, 0 to 500 by the game options' slider.</summary>
	public float CounterDistance { get; init; } = 100f;

	public bool Waves { get; init; } = true;

	/// <summary><c>counter_scale</c>: 1 in a new install's settings, 0 to 2 by the game options' slider.</summary>
	public float CounterScale { get; init; } = 1f;

	public bool RenderHierarchy { get; init; } = true;

	public bool ShowUnitNames { get; init; }

	// The rest, at the top level.
	public string LastPlayer { get; init; } = "";

	public string LastHost { get; init; } = "";

	public string ServerAddress { get; init; } = "diplomacy.valkyrienet.com";

	public int DebugSaves { get; init; }

	/// <summary><c>autosave</c>, by its name: <c>NEVER</c>, <c>WEEKLY</c>, <c>MONTHLY</c>, <c>HALFYEAR</c>, <c>YEARLY</c>, <c>FIVE_YEAR</c>.</summary>
	public string Autosave { get; init; } = "YEARLY";

	/// <summary><c>simple</c>: what it means is not read.</summary>
	public bool Simple { get; init; } = true;

	public bool UseLegacyCamera { get; init; }

	/// <summary><c>categories</c>: what they mean is not read; written only where there are any.</summary>
	public IReadOnlyList<int> Categories { get; init; } = DefaultCategories;

	/// <summary>
	/// <c>outliner</c>: the outliner's sections switched on, 1 to 9, and 10 where it is open, in no order. With no key the
	/// original's own: open, with land units, fleets and air units. See <c>docs/mechanics/outliner.md</c>, section 3.
	/// </summary>
	public IReadOnlyList<int> Outliner { get; init; } = DefaultOutliner;
}
