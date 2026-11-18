using System.Collections.Generic;
using OpenHOI3.Content.Mods;

namespace OpenHOI3.Content.Launch;

/// <summary>A descriptor as the launcher lists it: the mod it describes, or none, and what is wrong with it.</summary>
/// <param name="VirtualPath">The descriptor's virtual path, <c>mod/&lt;file&gt;.mod</c>: what the launcher passes on.</param>
/// <param name="File">The <c>.mod</c> file, absolute.</param>
/// <param name="Mod">The mod, or null where the descriptor is refused and cannot be played.</param>
/// <param name="Problems">What reading it reported that a player should know, each once: not a key the original drops,
/// which is a mod maker's note and goes to the engine's log.</param>
/// <param name="Refusal">Why it cannot be played, said to a player; null where it can.</param>
public sealed record CModEntry(string VirtualPath, string File, CMod? Mod, IReadOnlyList<string> Problems, string? Refusal = null);
