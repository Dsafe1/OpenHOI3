using System;
using System.Collections.Generic;
using OpenHOI3.Content.Messages;
using OpenHOI3.Interface.Text;

namespace OpenHOI3.Interface.Messages;

/// <summary>
/// What the in-game menu's message settings say and do to a type: a row's text, which types a tab lists, its four
/// boxes - ticked where the setting is off - and their tooltips and the tabs', the original's two slips with them. See
/// <c>docs/mechanics/ingamemenu.md</c>, section 4.
/// </summary>
public static class CMessageSettingsText
{
	public const string PauseBox = "messageicon_pause";
	public const string LogBox = "messageicon_log";
	public const string DialogBox = "messageicon_dialog";
	public const string MapBox = "messageicon_map";

	/// <summary>A row's four boxes, in the row's order.</summary>
	public static IReadOnlyList<string> Boxes { get; } = new[] { PauseBox, LogBox, DialogBox, MapBox };

	public const string AllTab = "messagecat_all";

	/// <summary>The seven tabs and what each lists, null being every type.</summary>
	public static IReadOnlyList<(string Name, EMessageCategory? Category)> Tabs { get; } = new (string, EMessageCategory?)[]
	{
		(AllTab, null),
		("messagecat_combat", EMessageCategory.Combat),
		("messagecat_diplomacy", EMessageCategory.Diplomacy),
		("messagecat_units", EMessageCategory.Units),
		("messagecat_provinces", EMessageCategory.Province),
		("messagecat_events", EMessageCategory.Events),
		("messagecat_others", EMessageCategory.Other),
	};

	/// <summary>A row's <c>text</c>: the type's <c>_SETUP</c> line.</summary>
	public static string RowText(Func<string, string> localise, CMessageType type)
	{
		return localise(type.Name + "_SETUP");
	}

	/// <summary>The rows a tab lists, in the types' order; a null category lists every type.</summary>
	public static IEnumerable<CMessageType> Listed(CMessageTypes types, EMessageCategory? category)
	{
		foreach (CMessageType type in types.All)
		{
			if (category is null || type.Category == category)
			{
				yield return type;
			}
		}
	}

	/// <summary>Whether a box is ticked: where its setting is off.</summary>
	public static bool Ticked(CMessageType type, string box)
	{
		return box switch
		{
			PauseBox => !type.PausePopup,
			LogBox => !type.Log,
			DialogBox => !type.Popup,
			MapBox => !type.OnMap,
			_ => false,
		};
	}

	/// <summary>A click on any of a row's boxes: all four of them written into the type, a ticked one as off.</summary>
	public static void Apply(CMessageType type, bool pauseTicked, bool logTicked, bool dialogTicked, bool mapTicked)
	{
		type.PausePopup = !pauseTicked;
		type.Log = !logTicked;
		type.Popup = !dialogTicked;
		type.OnMap = !mapTicked;
	}

	/// <summary>
	/// A box's tooltip, by the type as it is now. The pause box's tests the log, as the original's does - so it says
	/// what the log box's says.
	/// </summary>
	public static string? BoxTooltip(Func<string, string> localise, CMessageType type, string box)
	{
		(string key, bool on) = box switch
		{
			LogBox => ("MESSAGE_IS_LOG", type.Log),
			DialogBox => ("MESSAGE_IS_POPUP", type.Popup),
			MapBox => ("MESSAGE_IS_ONMAP", type.OnMap),
			PauseBox => ("MESSAGE_IS_PAUSE", type.Log),
			_ => ("", false),
		};
		if (key.Length == 0)
		{
			return null;
		}
		return CTextTemplate.Fill(localise(key), ("TRUTH", localise(on ? "MESSAGE_SETTING_TRUE" : "MESSAGE_SETTING_FALSE")));
	}

	/// <summary>A tab's tooltip. <c>messagecat_all</c> has none: the original asks for <c>messagecat_others</c> twice.</summary>
	public static string? TabTooltip(Func<string, string> localise, string tab)
	{
		string? key = tab switch
		{
			"messagecat_combat" => "MESSAGECAT_COMBAT",
			"messagecat_diplomacy" => "MESSAGECAT_DIPLOMACY",
			"messagecat_units" => "MESSAGECAT_UNITS",
			"messagecat_provinces" => "MESSAGECAT_PROVS",
			"messagecat_events" => "MESSAGECAT_EVENTS",
			"messagecat_others" => "MESSAGECAT_OTHERS",
			_ => null,
		};
		return key is null ? null : localise(key);
	}
}
