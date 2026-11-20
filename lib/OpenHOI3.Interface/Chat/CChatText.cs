using System;
using OpenHOI3.Interface.Text;

namespace OpenHOI3.Interface.Chat;

/// <summary>
/// What a line of the chat in play says in the window's list (<c>docs/mechanics/chat.md</c>, section 4): the clock,
/// who, and the text. The original's pieces are the clock <c>[HH:MM]</c>, CHAT_SYSTEM for the chat's own lines and
/// CHAT_WHISPER_TO for a whisper sent; **their order and colours are ours**, the routine that puts them together not
/// being read: the clock, the name in yellow, a colon, the text.
/// </summary>
public static class CChatText
{
	public static string Row(Func<string, string> localise, CChatLine line)
	{
		string clock = "[" + line.Clock + "] ";
		return line.Kind switch
		{
			EChatLineKind.System => clock + "§Y" + localise("CHAT_SYSTEM") + "§!: "
				+ CTextTemplate.Fill(localise(line.Text), ("NAME", line.Name)),
			EChatLineKind.WhisperOut => clock + "§Y" + CTextTemplate.Fill(localise("CHAT_WHISPER_TO"), ("NAME", line.Name)) + "§!: " + line.Text,
			// Ours: a star after the name marks a line sent to this player alone, as the original's country line marks one.
			EChatLineKind.WhisperIn => clock + "§Y" + line.Name + "*§!: " + line.Text,
			_ => clock + "§Y" + line.Name + "§!: " + line.Text,
		};
	}

	/// <summary>A channel's name on its tab.</summary>
	public static string Channel(Func<string, string> localise, int channel) => localise(CChatChannels.Keys[channel]);
}
