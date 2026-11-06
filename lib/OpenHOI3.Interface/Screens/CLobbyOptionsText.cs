using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim.Settings;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The lobby's Options panel, <c>options.gui</c>'s <c>option_window</c>: its rows, each value's text, the arrows' wrapping
/// and the line a change writes in the chat. See <c>docs/mechanics/frontend.md</c>, section 4;
/// <c>reversing/findings/FINDINGS-frontend.md</c>, C.2.
/// </summary>
public static class CLobbyOptionsText
{
	/// <summary>The difficulty's values, very easy to very hard.</summary>
	public static readonly string[] Difficulties = { "DIFF_VERY_EASY", "DIFF_EASY", "DIFF_NORMAL", "DIFF_HARD", "DIFF_VERY_HARD" };

	public static readonly string[] GameModes = { "STANDARD_MODE", "ARCADE_MODE" };

	private static readonly string[] TwoWays = { "OPT_PLAYER_CONTROL", "OPT_AICONTROL" };

	private static readonly string[] Technology = { "OPT_PLAYER_CONTROL", "OPT_AICONTROL", "TECH_AUTOMATED", "LEADERSHIP_AUTOMATED" };

	/// <summary>The five areas with a box, by their elements' name, their label and their area; Armies and deployment have none.</summary>
	public static readonly IReadOnlyList<(string Element, string Label, EAutomation Area)> Areas = new[]
	{
		("diplomacy", "FE_SETTING_DIPLOMACY", EAutomation.Diplomacy),
		("production", "FE_SETTING_PRODUCTION", EAutomation.Production),
		("technology", "FE_SETTING_TECHNOLOGY", EAutomation.Technology),
		("politics", "FE_SETTING_POLITICS", EAutomation.Politics),
		("intel", "FE_SETTING_INTEL", EAutomation.Intelligence),
	};

	/// <summary>An area's values: the Technology row's four, every other's two.</summary>
	public static string[] Values(EAutomation area) => area == EAutomation.Technology ? Technology : TwoWays;

	/// <summary>An area's value as the panel shows it: the Technology setting, or the flag.</summary>
	public static int Value(CPlayerAutomation automation, EAutomation area)
	{
		return area == EAutomation.Technology ? automation.Technology : automation.Flags[(int)area];
	}

	/// <summary>An area set to a value, as the lobby writes it: the Technology row to the setting alone, the others to their flag.</summary>
	public static void Set(CPlayerAutomation automation, EAutomation area, int value)
	{
		if (area == EAutomation.Technology)
		{
			automation.Technology = value;
		}
		else
		{
			automation.Flags[(int)area] = value;
		}
	}

	/// <summary>A step of an arrow: one on, wrapping past either end [exe].</summary>
	public static int Step(int value, int step, int count)
	{
		return ((value + step) % count + count) % count;
	}

	/// <summary>A value's text, nothing for one out of range.</summary>
	public static string Text(Func<string, string> localise, string[] values, int index)
	{
		return index >= 0 && index < values.Length ? localise(values[index]) : "";
	}

	/// <summary>OPTION_CHANGED "$FROM$ has changed to: $TO$" - the label's colon kept, "Difficulty: has changed to: Hard".</summary>
	public static string Changed(Func<string, string> localise, string label, string to)
	{
		return CTextTemplate.Fill(localise("OPTION_CHANGED"), ("FROM", localise(label)), ("TO", to));
	}
}
