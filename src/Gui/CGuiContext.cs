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

	/// <summary>The player's own message settings, <c>messagetypes_custom.txt</c> in the player's folder.</summary>
	public static string MessageTypesPath => System.IO.Path.Combine(Content.Settings.CUserSettingsReader.UserFolder(),
		CMessageTypes.CustomFile);

	/// <summary>
	/// The edit box that has the keyboard: the last one clicked or given it, kept until another is - hiding it, Enter and
	/// Escape take nothing away, as the original's (<c>docs/mechanics/interface.md</c>, section 8).
	/// </summary>
	public CGuiTextBox? KeyboardFocus { get; set; }

	/// <summary>The install's effect files as Godot shaders, a mod's over the install's (<c>docs/mechanics/shaders.md</c>).</summary>
	public required CEffectShaders Effects { get; init; }

	private CButtonShader? _buttonShader;

	/// <summary>What draws a button under the mouse, held down and switched off: <c>buttonstate.fx</c>.</summary>
	public CButtonShader ButtonShader => _buttonShader ??= new CButtonShader(Effects);

	private CTextIconTextures? _textIcons;

	/// <summary>The flags and counters a text draws in its line.</summary>
	public CTextIconTextures TextIcons => _textIcons ??= new CTextIconTextures(Textures, Sprites);

	public static CGuiContext Load(CDirectorySettings directories, CLocalisation localisation)
	{
		var textures = new CTextureManager(directories);
		var effects = new CEffectShaders(directories);
		return new CGuiContext
		{
			Localisation = localisation,
			Types = CGuiTypes.Load(directories),
			Sprites = CSpriteTypes.Load(directories),
			Textures = textures,
			Fonts = CGuiFonts.Load(directories),
			Shields = new CShields(textures, effects),
			// The player's choices over the install's, as the original reads them at every start (ingamemenu.md, section 5).
			MessageTypes = CMessageTypes.Load(directories, MessageTypesPath),
			Effects = effects,
		};
	}
}
