using OpenHOI3.Content.Settings;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// An enhanced game's two extra autosaves, beside the original's own and never in its place: one a set number of days
/// before each month ends, and one on a timer of real time. Each keeps three rotating files of its own, so no set
/// pushes another out. Both are decided where the original's is - once, as a day begins - so a paused game takes none
/// and a timed save lands on the first day to begin after its interval is up. What it remembers is the session's and
/// no part of the game. <c>docs/mechanics/savegame.md</c>, section 11.1.
/// </summary>
public sealed class CExtraAutosaves
{
	/// <summary>The pre-month save's three files, the newest first.</summary>
	public static readonly string[] PreMonthFiles = Files("autosave_premonth");

	/// <summary>The timed save's three files, the newest first.</summary>
	public static readonly string[] TimedFiles = Files("autosave_timed");

	private const long MillisecondsPerMinute = 60_000;

	private static readonly int[] MonthDays = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

	/// <summary>The day a pre-month save was last asked for: one a calendar day.</summary>
	private CDate? _preMonthAsked;

	/// <summary>When the timer was last started, on the caller's clock of milliseconds; null while it is not running.</summary>
	private long? _timerStarted;

	/// <summary>A base name's three files as the original's rotation names its own: the name, <c>old</c> and <c>older</c> before it.</summary>
	public static string[] Files(string baseName)
	{
		return new[] { baseName + CSaveFiles.Extension, "old" + baseName + CSaveFiles.Extension, "older" + baseName + CSaveFiles.Extension };
	}

	/// <summary>The files of a kind of save.</summary>
	public static string[] FilesOf(EExtraAutosave kind) => kind == EExtraAutosave.PreMonth ? PreMonthFiles : TimedFiles;

	/// <summary>
	/// Whether a day is the pre-month save's: the day of the month that is its length less the days plus one, on the
	/// original's calendar of no leap years - with 2, the 30th of a month of 31 days and the 27th of February; with 1, a
	/// month's last day.
	/// </summary>
	public static bool IsPreMonthDay(CDate date, int days)
	{
		return days >= 1 && date.Month is >= 1 and <= 12 && date.Day == MonthDays[date.Month - 1] - days + 1;
	}

	/// <summary>
	/// What the day that has just begun asks for. The pre-month save comes first, having a day of its own to land on:
	/// asked once a calendar day. Otherwise the timed one: a timer not running is started and nothing asked, so the
	/// first interval is counted from a day of play and not from the menu; then nothing until the interval is up. A save
	/// of either kind starts the timer afresh, which promises that no more than its interval goes unsaved. With the
	/// timed save off the timer stops.
	/// </summary>
	/// <param name="now">Real time in milliseconds, from any start: only its differences are read.</param>
	public EExtraAutosave Decide(CEnhancedSettings settings, CDate today, long now)
	{
		if (IsPreMonthDay(today, settings.PreMonthDays) && _preMonthAsked != today)
		{
			_preMonthAsked = today;
			_timerStarted = settings.TimedMinutes > 0 ? now : null;
			return EExtraAutosave.PreMonth;
		}
		if (settings.TimedMinutes <= 0)
		{
			_timerStarted = null;
			return EExtraAutosave.None;
		}
		if (_timerStarted is not { } started)
		{
			_timerStarted = now;
			return EExtraAutosave.None;
		}
		if (now - started < settings.TimedMinutes * MillisecondsPerMinute)
		{
			return EExtraAutosave.None;
		}
		_timerStarted = now;
		return EExtraAutosave.Timed;
	}
}
