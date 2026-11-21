using System;
using System.Globalization;
using System.IO;
using System.Text;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Settings;

/// <summary>
/// <c>openhoi_settings.txt</c> in the player's folder, beside the original's <c>settings.txt</c>: the settings an
/// enhanced game adds (<see cref="CEnhancedSettings"/>). Ours - the original never opens it - so UTF-8, and named with
/// the prefix everything of this engine's carries where it lies among the original's files. Read where a game is an
/// enhanced one, written by the game options' Apply. <c>docs/mechanics/gameoptions.md</c>, section 5.
/// <code>
/// premonth_autosave_days = 2
/// timed_autosave_minutes = 15
/// edge_scrolling = yes
/// event_window = centre
/// </code>
/// </summary>
public static class CEnhancedSettingsFile
{
	public const string FileName = "openhoi_settings.txt";

	private const string PreMonthKey = "premonth_autosave_days";
	private const string TimedKey = "timed_autosave_minutes";
	private const string EdgeKey = "edge_scrolling";
	private const string EventKey = "event_window";

	/// <summary>The file for what is played: in the player's folder, a mod's own where it has one.</summary>
	public static string DefaultPath(CDirectorySettings directories)
	{
		return Path.Combine(directories.UserFolder, FileName);
	}

	/// <summary>The settings in a file, or the defaults where there is none; what is not understood is reported and left at its default.</summary>
	public static CEnhancedSettings Read(string path, Action<string>? warn = null)
	{
		return File.Exists(path) ? Parse(File.ReadAllText(path, Encoding.UTF8), path, warn ?? CDiagnostics.Warn) : CEnhancedSettings.Default;
	}

	public static CEnhancedSettings Parse(string text, string source, Action<string> warn)
	{
		CEnhancedSettings settings = CEnhancedSettings.Default;
		foreach (ClausewitzNode entry in ClausewitzParser.Parse(text, source).Children)
		{
			string value = entry.Value ?? "";
			switch (entry.Key)
			{
				case PreMonthKey when Whole(value) is { } days:
					settings = settings with { PreMonthDays = days };
					break;
				case TimedKey when Whole(value) is { } minutes:
					settings = settings with { TimedMinutes = minutes };
					break;
				case EdgeKey when value is "yes" or "no":
					settings = settings with { EdgeScrolling = value == "yes" };
					break;
				case EventKey when Place(value) is { } place:
					settings = settings with { EventWindow = place };
					break;
				default:
					warn(CDiagnostics.Located(source, entry.Line, $"'{entry.Key ?? entry.Value}' is not a setting of {FileName}, or its value is not one it takes; ignored"));
					break;
			}
		}
		return settings.Held();
	}

	/// <summary>The file's text for the settings: every key, whatever it holds.</summary>
	public static string Write(CEnhancedSettings settings)
	{
		CEnhancedSettings held = settings.Held();
		var text = new StringBuilder();
		text.Append("# OpenHOI3's own settings for an enhanced game; the original's are in settings.txt beside this file.\n");
		text.Append(CultureInfo.InvariantCulture, $"{PreMonthKey} = {held.PreMonthDays}\n");
		text.Append(CultureInfo.InvariantCulture, $"{TimedKey} = {held.TimedMinutes}\n");
		text.Append(CultureInfo.InvariantCulture, $"{EdgeKey} = {(held.EdgeScrolling ? "yes" : "no")}\n");
		text.Append(CultureInfo.InvariantCulture, $"{EventKey} = {PlaceName(held.EventWindow)}\n");
		return text.ToString();
	}

	/// <summary>The file written; a failure is answered, as the settings' own writer answers one, and not thrown.</summary>
	public static bool WriteFile(CEnhancedSettings settings, string path, Action<string>? warn = null)
	{
		try
		{
			if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
			{
				Directory.CreateDirectory(folder);
			}
			File.WriteAllText(path, Write(settings), new UTF8Encoding(false));
			return true;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			(warn ?? CDiagnostics.Warn)($"Could not write '{path}': {e.Message}");
			return false;
		}
	}

	private static int? Whole(string value)
	{
		return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : null;
	}

	private static EEventWindowPlace? Place(string value) => value switch
	{
		"default" => EEventWindowPlace.AsFiled,
		"left" => EEventWindowPlace.Left,
		"centre" => EEventWindowPlace.Centre,
		_ => null,
	};

	private static string PlaceName(EEventWindowPlace place) => place switch
	{
		EEventWindowPlace.Left => "left",
		EEventWindowPlace.Centre => "centre",
		_ => "default",
	};
}
