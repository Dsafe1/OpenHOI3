using System;
using System.Globalization;
using OpenHOI3.Content.Settings;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the enhanced rows of the game options' Game tab show between their arrows (<c>docs/mechanics/gameoptions.md</c>,
/// section 5): always a text of the localisation, by a key of ours - <c>OPENHOI_OPT_...</c> - which the mod's tables
/// give; nothing is worded here, and a key no table has shows as itself, as every missing text does. <c>$DAYS$</c> and
/// <c>$MINUTES$</c> in a text stand for the figure. A row's label is its interface file's own text.
/// </summary>
public static class CEnhancedOptionsText
{
	public const string Off = "OPENHOI_OPT_OFF";
	public const string On = "OPENHOI_OPT_ON";
	public const string PreMonthOne = "OPENHOI_OPT_PREMONTH_ONE";
	public const string PreMonthDays = "OPENHOI_OPT_PREMONTH_DAYS";
	public const string TimedMinutes = "OPENHOI_OPT_TIMED_MINUTES";
	public const string EventDefault = "OPENHOI_OPT_EVENT_DEFAULT";
	public const string EventLeft = "OPENHOI_OPT_EVENT_LEFT";
	public const string EventCentre = "OPENHOI_OPT_EVENT_CENTRE";

	/// <summary>The key of what a row shows.</summary>
	public static string ValueKey(string row, CEnhancedOptions options) => row switch
	{
		"premonth" => options.PreMonthDays switch
		{
			0 => Off,
			1 => PreMonthOne,
			_ => PreMonthDays,
		},
		"timed" => options.TimedMinutes == 0 ? Off : TimedMinutes,
		"edge" => options.EdgeScrolling ? On : Off,
		"event" => options.EventWindow switch
		{
			EEventWindowPlace.Left => EventLeft,
			EEventWindowPlace.Centre => EventCentre,
			_ => EventDefault,
		},
		_ => "",
	};

	/// <summary>What a row shows: its key's text with the figures put in.</summary>
	public static string Value(string row, CEnhancedOptions options, Func<string, string> localise)
	{
		return localise(ValueKey(row, options))
			.Replace("$DAYS$", options.PreMonthDays.ToString(CultureInfo.InvariantCulture))
			.Replace("$MINUTES$", options.TimedMinutes.ToString(CultureInfo.InvariantCulture));
	}
}
