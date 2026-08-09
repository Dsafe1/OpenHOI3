using OpenHOI3.Core;
using OpenHOI3.Gfx;

namespace OpenHOI3.Gui;

/// <summary>
/// The loaded game data a GUI element needs to build itself. Passed down rather than
/// reached for globally so a second set can be loaded side by side later, for example
/// to reload the interface after a mod change.
/// </summary>
public sealed class CGuiContext
{
	public required CDirectorySettings Directories { get; init; }
	public required CLocalisation Localisation { get; init; }
	public required CSpriteTypes Sprites { get; init; }
	public required CTextureManager Textures { get; init; }
	public required CBitmapFonts Fonts { get; init; }
}
