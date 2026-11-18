using System;
using System.Collections.Generic;

namespace OpenHOI3.Content.Launch;

/// <summary>What the launcher remembers between runs (<c>docs/launcher.md</c>, section 6): the install and the mods ticked.</summary>
/// <param name="Install">The install last played, or null where none was chosen.</param>
/// <param name="Mods">The mods ticked, by virtual path.</param>
public sealed record CLauncherSettings(string? Install, IReadOnlyList<string> Mods)
{
	/// <summary>Nothing remembered: a first run.</summary>
	public static CLauncherSettings Empty { get; } = new(null, Array.Empty<string>());
}
