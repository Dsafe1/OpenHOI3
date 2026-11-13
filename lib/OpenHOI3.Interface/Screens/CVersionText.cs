using OpenHOI3.Content.Checksum;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The main menu's <c>version_label</c> and the lobby's <c>checksum</c>: the original writes its version name, then the
/// file checksum's four letters in brackets (<c>docs/mechanics/checksum.md</c>, section 5).
/// </summary>
public static class CVersionText
{
	/// <summary>Ours in the name's place, the letters the original's for the same files.</summary>
	public const string Name = "OpenHOI3";

	/// <summary>The label for a checksum, or the name alone while there is none yet.</summary>
	public static string Label(int? checksum)
	{
		return checksum is { } figure ? $"{Name} ({CFileChecksum.Letters(figure)})" : Name;
	}
}
