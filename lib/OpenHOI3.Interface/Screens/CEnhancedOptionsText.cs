using System;
using System.Globalization;
using OpenHOI3.Content.Settings;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the enhanced rows of the game options' Game tab say (<c>docs/mechanics/gameoptions.md</c>, section 5). Each
/// text has a key a mod's localisation may give - <c>OPENHOI_OPT_...</c> - and, the original's tables having none of
/// them, an English one of the engine's own where no table does. <c>$DAYS$</c> and <c>$MINUTES$</c> stand for the figure.
/// </summary>
public static class CEnhancedOptionsText
{
	/// <summary>A row's label.</summary>
	public static string Label(string row, Func<string, string?> find) => row switch
	{
		"premonth" => Text(find, "OPENHOI_OPT_PREMONTH", "Save before the month ends"),
		"timed" => Text(find, "OPENHOI_OPT_TIMED", "Save on a timer"),
		"edge" => Text(find, "OPENHOI_OPT_EDGE", "Scroll at the screen's edge"),
		"event" => Text(find, "OPENHOI_OPT_EVENT", "Event windows"),
		_ => "",
	};

	/// <summary>What a row shows between its arrows.</summary>
	public static string Value(string row, CEnhancedOptions options, Func<string, string?> find) => row switch
	{
		"premonth" => options.PreMonthDays switch
		{
			0 => Off(find),
			1 => Text(find, "OPENHOI_OPT_PREMONTH_ONE", "On its last day"),
			int days => Text(find, "OPENHOI_OPT_PREMONTH_DAYS", "$DAYS$ days before").Replace("$DAYS$", Figure(days)),
		},
		"timed" => options.TimedMinutes == 0
			? Off(find)
			: Text(find, "OPENHOI_OPT_TIMED_MINUTES", "Every $MINUTES$ minutes").Replace("$MINUTES$", Figure(options.TimedMinutes)),
		"edge" => options.EdgeScrolling ? Text(find, "OPENHOI_OPT_ON", "On") : Off(find),
		"event" => options.EventWindow switch
		{
			EEventWindowPlace.Left => Text(find, "OPENHOI_OPT_EVENT_LEFT", "On the left"),
			EEventWindowPlace.Centre => Text(find, "OPENHOI_OPT_EVENT_CENTRE", "In the middle"),
			// Short: the value has the width between the two arrows and no more.
			_ => Text(find, "OPENHOI_OPT_EVENT_DEFAULT", "Default"),
		},
		_ => "",
	};

	private static string Off(Func<string, string?> find) => Text(find, "OPENHOI_OPT_OFF", "Off");

	private static string Text(Func<string, string?> find, string key, string ours) => find(key) ?? ours;

	private static string Figure(int value) => value.ToString(CultureInfo.InvariantCulture);
}
