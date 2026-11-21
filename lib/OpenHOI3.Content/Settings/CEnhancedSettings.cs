using System;

namespace OpenHOI3.Content.Settings;

/// <summary>
/// The player's settings an enhanced game adds to the original's (<c>docs/mechanics/gameoptions.md</c>, section 5): kept
/// in a file of their own beside <c>settings.txt</c>, which the original reads too and has to find as it wrote it.
/// </summary>
/// <param name="PreMonthDays">
/// The autosave before the month ends: how many days before the 1st of the next month, 1 to 27, or nought for none.
/// </param>
/// <param name="TimedMinutes">The autosave on a timer: every so many minutes of real time, 1 to 240, or nought for none.</param>
/// <param name="EdgeScrolling">Whether the map moves with the pointer at the screen's edge, as the original's always does.</param>
/// <param name="EventWindow">Where a country event's window comes up.</param>
public sealed record CEnhancedSettings(int PreMonthDays = 0, int TimedMinutes = 0, bool EdgeScrolling = true,
	EEventWindowPlace EventWindow = EEventWindowPlace.AsFiled)
{
	/// <summary>The most days before the month's end: the 27 that keep the day inside February.</summary>
	public const int MostDays = 27;

	/// <summary>The longest the timer goes, in minutes.</summary>
	public const int MostMinutes = 240;

	/// <summary>What a game has with no file: nothing the original does not do.</summary>
	public static readonly CEnhancedSettings Default = new();

	/// <summary>These settings with each figure held to what it may be.</summary>
	public CEnhancedSettings Held()
	{
		return this with
		{
			PreMonthDays = Math.Clamp(PreMonthDays, 0, MostDays),
			TimedMinutes = Math.Clamp(TimedMinutes, 0, MostMinutes),
		};
	}
}
