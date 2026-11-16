using OpenHOI3.Content.Checksum;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The main menu's <c>version_label</c> and the lobby's <c>checksum</c>: the original writes its version name, then the
/// file checksum's four letters in brackets, and where a mod is played four spaces, <c>Mod: </c> and its localised name
/// (<c>docs/mechanics/checksum.md</c>, section 5; <c>docs/mechanics/mods.md</c>, section 3).
/// </summary>
public static class CVersionText
{
	/// <summary>Ours in the name's place, the letters the original's for the same files.</summary>
	public const string Name = "OpenHOI3";

	/// <summary>The label for a checksum, or the name alone while there is none yet; and the mod's name where one is played.</summary>
	public static string Label(int? checksum, string? mod = null)
	{
		string label = checksum is { } figure ? $"{Name} ({CFileChecksum.Letters(figure)})" : Name;
		return mod is { Length: > 0 } ? $"{label}    Mod: {mod}" : label;
	}
}
