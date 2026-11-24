using System;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the lobby's window of rules says of the host's rule on other files, and the window the engine puts up where
/// the files played declare none (<c>docs/multiplayer.md</c>, section 15): the keys a mod may give texts for, and the
/// words in their place where no table has them - the base game's have none.
/// </summary>
public static class CLobbyRulesText
{
	public const string TitleKey = "OPENHOI_RULES_TITLE";
	public const string OtherFilesKey = "OPENHOI_RULE_OTHER_FILES";
	public const string OtherFilesTipKey = "OPENHOI_RULE_OTHER_FILES_TIP";

	public const string Title = "Game rules";
	public const string OtherFiles = "Allow other files";

	public const string OtherFilesTip = "Lets a player in whose game files are not the host's, as long as the engine, "
		+ "the rules and the original checksum are the same.\nThe player is shown which files differ. Where they change "
		+ "how the game goes, the machines part and the game is sent again each midnight.";

	/// <summary>A key's text, or the words given where no table has the key.</summary>
	public static string Text(Func<string, string> localise, string key, string words)
	{
		string text = localise(key);
		return text.Length == 0 || text == key ? words : text;
	}

	/// <summary>The file the engine's own window is read as: one of an enhanced game's folder, whose pictures the engine paints.</summary>
	public const string FallbackFile = "interface/openhoi/engine_lobbyrules.gui";

	/// <summary>
	/// The window the engine puts up where no file declares <c>openhoi_lobby_rules</c>: the one rule of any game, every
	/// picture a built-in element (<c>docs/mechanics/builtin.md</c>). Its texts are the keys above.
	/// </summary>
	public const string FallbackWindow = """
		guiTypes = {
			windowType = {
				name = "openhoi_lobby_rules"
				backGround = "background"
				position = { x = -232 y = 575 }
				size = { x = 224 y = 64 }
				moveable = 0
				dontRender = ""
				horizontalBorder = ""
				verticalBorder = ""
				fullScreen = no
				Orientation = "UPPER_RIGHT"

				iconType = { name = "background" position = { x = 0 y = 0 } built_in = { style = panel size = { x = 224 y = 64 } } }
				iconType = { name = "title_bar" position = { x = 4 y = 4 } built_in = { style = header size = { x = 216 y = 22 } } }
				instantTextBoxType = { name = "title" position = { x = 4 y = 7 } font = "Arial12_bold" text = "OPENHOI_RULES_TITLE" maxWidth = 216 maxHeight = 18 format = centre }
				guiButtonType = { name = "other_files" position = { x = 12 y = 34 } built_in = { style = field size = { x = 20 y = 20 } } buttonText = "" buttonFont = "Arial12_bold_black" }
				instantTextBoxType = { name = "other_files_label" position = { x = 40 y = 37 } font = "Arial12_bold_black" text = "OPENHOI_RULE_OTHER_FILES" maxWidth = 148 maxHeight = 18 }
				guiButtonType = { name = "other_files_help" position = { x = 194 y = 35 } built_in = { style = button size = { x = 18 y = 18 } } buttonText = "?" buttonFont = "Arial12_bold_black" }
			}
		}
		""";
}
