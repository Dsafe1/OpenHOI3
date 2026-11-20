using System;
using System.Collections.Generic;

namespace OpenHOI3.Interface.Chat;

/// <summary>
/// The five channels of the chat in play, in the original's order, and which of them a country's player is put in
/// by its faction. See <c>docs/mechanics/chat.md</c>, section 2; <c>reversing/findings/FINDINGS-ingamechat.md</c>,
/// section 2.
/// </summary>
public static class CChatChannels
{
	public const int General = 0;
	public const int Axis = 1;
	public const int Allies = 2;
	public const int Comintern = 3;
	public const int Neutral = 4;

	/// <summary>Each channel's localisation key, by its number.</summary>
	public static readonly IReadOnlyList<string> Keys = new[]
	{
		"CHAT_CHANNEL_GENERAL", "CHAT_AXIS", "CHAT_ALLIES", "CHAT_COMINTERN", "CHAT_NEUTRAL",
	};

	/// <summary>Whether a number names one of the five.</summary>
	public static bool Is(int channel) => channel >= 0 && channel < Keys.Count;

	/// <summary>
	/// The faction channel of a country by its faction's tag: Axis, Allies or Comintern by the three tags the original
	/// names, **and Neutral for no faction and for any other tag** - a mod's own faction shares it.
	/// </summary>
	public static int OfFaction(string? factionTag)
	{
		return factionTag switch
		{
			"axis" => Axis,
			"allies" => Allies,
			"comintern" => Comintern,
			_ => Neutral,
		};
	}
}
