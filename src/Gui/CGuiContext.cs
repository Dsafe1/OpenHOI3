using OpenHOI3.Content;
using OpenHOI3.Content.Gui;
using OpenHOI3.Content.Messages;
using OpenHOI3.Gfx;

namespace OpenHOI3.Gui;

/// <summary>
/// What a live element needs to build itself: the interface declarations, the sprites,
/// textures and fonts they name, and the string table. Passed down rather than reached
/// for globally, so that a game owns its own and a second can be loaded beside it.
/// </summary>
public sealed class CGuiContext
{
	public required CLocalisation Localisation { get; init; }
	public required CGuiTypes Types { get; init; }
	public required CSpriteTypes Sprites { get; init; }
	public required CTextureManager Textures { get; init; }
	public required CGuiFonts Fonts { get; init; }
	public required CShields Shields { get; init; }

	/// <summary>The message types of <c>interface/messagetypes.txt</c>: <c>docs/mechanics/messages.md</c>.</summary>
	public required CMessageTypes MessageTypes { get; init; }

	/// <summary>What draws a button under the mouse, held down and switched off.</summary>
	public CButtonShader ButtonShader { get; } = new();

	public static CGuiContext Load(CDirectorySettings directories, CLocalisation localisation)
	{
		var textures = new CTextureManager(directories);
		return new CGuiContext
		{
			Localisation = localisation,
			Types = CGuiTypes.Load(directories),
			Sprites = CSpriteTypes.Load(directories),
			Textures = textures,
			Fonts = CGuiFonts.Load(directories),
			Shields = new CShields(textures),
			MessageTypes = CMessageTypes.Load(directories),
		};
	}
}
