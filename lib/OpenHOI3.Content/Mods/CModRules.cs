using System;

namespace OpenHOI3.Content.Mods;

/// <summary>
/// The rules of the original's mod layering that the layering here is built on, each in one place
/// (<c>docs/mechanics/mods.md</c>, section 2; <c>reversing/findings/FINDINGS-modlayer.md</c>).
/// </summary>
public static class CModRules
{
	/// <summary>
	/// The folder <c>.mod</c> files are listed in over the original's first search path - the player's folder, then
	/// <c>tfh/</c>, then the install - and the start of a descriptor's virtual path, <c>mod/&lt;file&gt;.mod</c>.
	/// </summary>
	public const string DescriptorFolder = "mod";

	/// <summary>A mod file's extension.</summary>
	public const string Extension = ".mod";

	/// <summary>
	/// How a <c>-mod=</c> value is matched against a descriptor's virtual path, and a listed folder against a
	/// <c>replace_path</c>: byte for byte, case and all (<i>confirmed</i>).
	/// </summary>
	public static readonly StringComparer Comparer = StringComparer.Ordinal;

	/// <summary>The order every selected mod starts from before its dependencies are raised above it (<i>confirmed</i>).</summary>
	public const int StartOrder = -1000;

	/// <summary>What the selected mods' <c>user_dir</c>s are joined with to name the player's folder (<i>confirmed</i>).</summary>
	public const string UserDirJoiner = "_";

	/// <summary>
	/// Whether a mod whose <c>path</c> is written with a backslash has its <c>replace_path</c>s do nothing: its mount is
	/// looked for under the path as written and is never found (<i>likely</i>, by the reading).
	/// </summary>
	public static readonly bool BackslashedPathDisablesReplace = true;

	/// <summary>
	/// Whether the player's folder is a layer of game files in front of the rest, as it is in the original's search path
	/// - the Documents folder without a mod, a mod's own folder with one (<i>confirmed</i>). Off here, a deliberate
	/// departure: the player's folder is asked by name only for what the game writes there - the settings, the saves, the
	/// message settings, the map cache, the logs - so that a stray file in it does not stand in for the install's
	/// (<c>docs/compatibility.md</c>, entry 28).
	/// </summary>
	public static readonly bool UserFolderIsLayer = false;
}
