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
/// <param name="WindowMode">
/// How the game's window stands on the screen, in any game, a faithful one too; null until the player has chosen,
/// and the window is then left as the system opened it.
/// </param>
public sealed record CEnhancedSettings(int PreMonthDays = 0, int TimedMinutes = 0, bool EdgeScrolling = true,
	EEventWindowPlace EventWindow = EEventWindowPlace.AsFiled, string WindowKey = CEnhancedSettings.DefaultWindowKey,
	string WindowTheme = CEnhancedSettings.DefaultWindowTheme, string MapPalette = CEnhancedSettings.DefaultMapPalette,
	EWindowMode? WindowMode = null)
{
	/// <summary>The most days before the month's end: the 27 that keep the day inside February.</summary>
	public const int MostDays = 27;

	/// <summary>The longest the timer goes, in minutes.</summary>
	public const int MostMinutes = 240;

	/// <summary>What a game has with no file: nothing the original does not do.</summary>
	/// <summary>The key that shows and hides a mod's windows, by the name the engine gives it.</summary>
	public const string DefaultWindowKey = "Insert";

	/// <summary>The palette of a custom map mode: one of <see cref="MapPalettes"/> (custommapmodes.md, section 5).</summary>
	public const string DefaultMapPalette = "green";

	public static readonly string[] MapPalettes = { "green", "heat" };

	/// <summary>The look of a mod's windows: one of <see cref="WindowThemes"/>.</summary>
	public const string DefaultWindowTheme = "dark";

	/// <summary>
	/// The looks offered: the three the drawing library has of its own, and <c>army_green</c> - olive panels and
	/// amber headings, asked for by the user on 2026-11-22 after the look of that name BlackICE's overlay has on the
	/// original. Its colours are this engine's own, made from that description.
	/// </summary>
	public static readonly string[] WindowThemes = { "dark", "light", "classic", "army_green" };

	public static readonly CEnhancedSettings Default = new();

	/// <summary>These settings with each figure held to what it may be.</summary>
	public CEnhancedSettings Held()
	{
		return this with
		{
			PreMonthDays = Math.Clamp(PreMonthDays, 0, MostDays),
			TimedMinutes = Math.Clamp(TimedMinutes, 0, MostMinutes),
			WindowKey = string.IsNullOrWhiteSpace(WindowKey) ? DefaultWindowKey : WindowKey.Trim(),
			WindowTheme = Array.IndexOf(WindowThemes, WindowTheme) >= 0 ? WindowTheme : DefaultWindowTheme,
		};
	}
}
