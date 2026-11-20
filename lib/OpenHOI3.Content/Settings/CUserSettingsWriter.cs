using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenHOI3.Content.Settings;

/// <summary>
/// Writes the player's <c>settings.txt</c> as the original's writer does: its 21 keys in its order, figures of nought to
/// a hundred with six places, a block's inner keys indented a tab only below the first level, the keys written only off
/// their defaults left out where on them, and every line ended CR LF. See <c>docs/mechanics/settings.md</c>, section 2.
/// </summary>
public static class CUserSettingsWriter
{
	/// <summary>The thirteen languages the original writes; any other is not written at all.</summary>
	private static readonly HashSet<string> Languages = new(StringComparer.Ordinal)
	{
		"l_english", "l_french", "l_german", "l_polish", "l_spanish", "l_italian", "l_swedish", "l_czech",
		"l_hungarian", "l_dutch", "l_portugese", "l_russian", "l_finnish",
	};

	/// <summary>The file written; a failure is answered, as the original's dialog tells it, and not thrown.</summary>
	public static bool WriteFile(CUserSettings settings, string path, Action<string>? warn = null)
	{
		try
		{
			System.IO.File.WriteAllBytes(path, Encoding.Latin1.GetBytes(Write(settings)));
			return true;
		}
		catch (Exception e)
		{
			(warn ?? CDiagnostics.Warn)($"Could not write '{path}': {e.Message}");
			return false;
		}
	}

	public static string Write(CUserSettings s)
	{
		var text = new StringBuilder();
		Open(text, "gui");
		if (Languages.Contains(s.Language))
		{
			Line(text, "language", s.Language);
		}
		if (s.HasSetLanguage)
		{
			Line(text, "has_set_language", "yes");
		}
		Close(text);

		Open(text, "graphics");
		Open(text, "size");
		Line(text, "\tx", Int(s.Width));
		Line(text, "\ty", Int(s.Height));
		Close(text);
		// The blank line after size's block is in every file the original writes; how it comes to be was not read.
		text.Append("\r\n");
		Line(text, "refreshRate", Int(s.RefreshRate));
		Line(text, "fullScreen", Bool(s.FullScreen));
		Line(text, "shadows", Bool(s.Shadows));
		Line(text, "shadowSize", Int(s.ShadowSize));
		Line(text, "multi_sampling", Int(s.MultiSampling));
		Line(text, "anisotropic_filtering", Int(s.AnisotropicFiltering));
		Line(text, "gamma", Float(s.Gamma));
		if (s.Adapter != 0)
		{
			Line(text, "adapter", Int(s.Adapter));
		}
		if (!s.Vsync)
		{
			Line(text, "vsync", "no");
		}
		if (s.DebugMonitor && s.Adapter == 0)
		{
			Line(text, "debugmonitor", "yes");
		}
		Close(text);

		Line(text, "sound_fx_volume", Float(s.SoundFxVolume));
		Line(text, "music_volume", Float(s.MusicVolume));
		Line(text, "scroll_speed", Float(s.ScrollSpeed));
		Line(text, "camera_rotation_speed", Float(s.CameraRotationSpeed));
		Line(text, "zoom_speed", Float(s.ZoomSpeed));
		Line(text, "mouse_speed", Float(s.MouseSpeed));
		Line(text, "master_volume", Float(s.MasterVolume));
		Line(text, "ambient_volume", Float(s.AmbientVolume));
		if (!s.PauseOnEvent)
		{
			Line(text, "pause_on_event", "no");
		}

		Open(text, "mapRenderingOptions");
		Line(text, "renderTrees", Bool(s.RenderTrees));
		Line(text, "onmap", Bool(s.OnMap));
		Line(text, "simpleWater", Bool(s.SimpleWater));
		Line(text, "alwaysCounters", Bool(s.AlwaysCounters));
		Line(text, "counter_distance", Float(s.CounterDistance));
		Line(text, "waves", Bool(s.Waves));
		Line(text, "counter_scale", Float(s.CounterScale));
		Line(text, "render_hierarchy", Bool(s.RenderHierarchy));
		Line(text, "show_unit_names", Bool(s.ShowUnitNames));
		Close(text);

		Line(text, "lastplayer", Quoted(s.LastPlayer));
		Line(text, "lasthost", Quoted(s.LastHost));
		// A key of ours, written only off its default: a file that never set it comes out as the original writes it.
		if (s.Port != CUserSettings.Default.Port)
		{
			Line(text, "openhoi3_port", Int(s.Port));
		}
		Line(text, "serveradress", Quoted(s.ServerAddress));
		Line(text, "debug_saves", Int(s.DebugSaves));
		Line(text, "autosave", Quoted(s.Autosave));
		Line(text, "simple", Bool(s.Simple));
		Line(text, "use_legacy_camera", Bool(s.UseLegacyCamera));
		if (s.Categories.Count > 0)
		{
			Numbers(text, "categories", s.Categories);
		}
		Numbers(text, "outliner", s.Outliner);
		return text.ToString();
	}

	private static void Open(StringBuilder text, string key)
	{
		text.Append(key).Append("=\r\n{\r\n");
	}

	private static void Close(StringBuilder text)
	{
		text.Append("}\r\n");
	}

	private static void Line(StringBuilder text, string key, string value)
	{
		text.Append(key).Append('=').Append(value).Append("\r\n");
	}

	/// <summary>A list of whole numbers: on the line after the brace, each followed by a space, then the closing brace.</summary>
	private static void Numbers(StringBuilder text, string key, IReadOnlyList<int> numbers)
	{
		text.Append(key).Append("=\r\n{\r\n");
		foreach (int number in numbers)
		{
			text.Append(Int(number)).Append(' ');
		}
		text.Append("}\r\n");
	}

	private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static string Float(float value) => value.ToString("F6", CultureInfo.InvariantCulture);

	private static string Bool(bool value) => value ? "yes" : "no";

	private static string Quoted(string value) => "\"" + value + "\"";
}
