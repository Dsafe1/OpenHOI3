using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Settings;

/// <summary>
/// Reads the player's <c>settings.txt</c> from <c>Documents\Paradox Interactive\Hearts of Iron III</c> - the one the
/// original writes, and not the one beside its executable. <see cref="CUserSettingsWriter"/> writes it back. See
/// <c>docs/mechanics/settings.md</c>.
/// </summary>
public static class CUserSettingsReader
{
	public const string FileName = "settings.txt";

	/// <summary>Where the original keeps the player's settings: its folder under the Documents folder.</summary>
	public static string DefaultPath()
	{
		return System.IO.Path.Combine(UserFolder(), FileName);
	}

	/// <summary>
	/// The player's folder of the base game, where the original keeps its settings, the message types the player chose
	/// and the saves (<c>docs/mechanics/ingamemenu.md</c>, section 5). A mod's is its own; no mod is played yet.
	/// </summary>
	public static string UserFolder()
	{
		string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
		return System.IO.Path.Combine(documents, "Paradox Interactive", "Hearts of Iron III");
	}

	/// <summary>The settings in a file, or the defaults where there is none; anything not supported is reported.</summary>
	public static CUserSettings Read(string path, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		if (!File.Exists(path))
		{
			return CUserSettings.Default;
		}
		ClausewitzNode root;
		try
		{
			root = ClausewitzParser.ParseFile(path);
		}
		catch (Exception e)
		{
			warn($"Could not parse '{path}': {e.Message}; the default settings are used");
			return CUserSettings.Default;
		}
		CUserSettings d = CUserSettings.Default;
		ClausewitzNode empty = new();
		ClausewitzNode gui = root.Get("gui") ?? empty;
		ClausewitzNode graphics = root.Get("graphics") ?? empty;
		ClausewitzNode size = graphics.Get("size") ?? empty;
		ClausewitzNode map = root.Get("mapRenderingOptions") ?? empty;
		bool alwaysCounters = map.GetBool("alwaysCounters", d.AlwaysCounters);
		if (!alwaysCounters)
		{
			warn($"'{path}': alwaysCounters = no is not supported; the map shows counters (docs/compatibility.md, entry 12)");
		}
		string autosave = root.GetString("autosave", d.Autosave);
		if (Array.IndexOf(AutosaveNames, autosave) < 0)
		{
			warn($"'{path}': autosave = \"{autosave}\" is none of the original's frequencies; {d.Autosave} is used");
			autosave = d.Autosave;
		}
		int shadowSize = graphics.GetInt("shadowSize", d.ShadowSize);
		return new CUserSettings
		{
			Path = path,
			Language = gui.GetString("language", d.Language),
			HasSetLanguage = gui.GetBool("has_set_language", d.HasSetLanguage),
			// The original's least size, and its shadows held between 256 and 4096.
			Width = Math.Max(size.GetInt("x", d.Width), 1024),
			Height = Math.Max(size.GetInt("y", d.Height), 768),
			RefreshRate = graphics.GetInt("refreshRate", d.RefreshRate),
			FullScreen = graphics.GetBool("fullScreen", d.FullScreen),
			Shadows = graphics.GetBool("shadows", d.Shadows),
			ShadowSize = shadowSize < 1 ? 256 : Math.Min(shadowSize, 4096),
			MultiSampling = graphics.GetInt("multi_sampling", d.MultiSampling),
			AnisotropicFiltering = graphics.GetInt("anisotropic_filtering", d.AnisotropicFiltering),
			Gamma = graphics.GetFloat("gamma", d.Gamma),
			Adapter = graphics.GetInt("adapter", d.Adapter),
			Vsync = graphics.GetBool("vsync", d.Vsync),
			DebugMonitor = graphics.GetBool("debugmonitor", d.DebugMonitor),
			SoundFxVolume = root.GetFloat("sound_fx_volume", d.SoundFxVolume),
			MusicVolume = root.GetFloat("music_volume", d.MusicVolume),
			ScrollSpeed = root.GetFloat("scroll_speed", d.ScrollSpeed),
			CameraRotationSpeed = root.GetFloat("camera_rotation_speed", d.CameraRotationSpeed),
			ZoomSpeed = root.GetFloat("zoom_speed", d.ZoomSpeed),
			MouseSpeed = root.GetFloat("mouse_speed", d.MouseSpeed),
			MasterVolume = root.GetFloat("master_volume", d.MasterVolume),
			AmbientVolume = root.GetFloat("ambient_volume", d.AmbientVolume),
			PauseOnEvent = root.GetBool("pause_on_event", d.PauseOnEvent),
			RenderTrees = map.GetBool("renderTrees", d.RenderTrees),
			OnMap = map.GetBool("onmap", d.OnMap),
			SimpleWater = map.GetBool("simpleWater", d.SimpleWater),
			AlwaysCounters = alwaysCounters,
			CounterDistance = map.GetFloat("counter_distance", d.CounterDistance),
			Waves = map.GetBool("waves", d.Waves),
			CounterScale = map.GetFloat("counter_scale", d.CounterScale),
			RenderHierarchy = map.GetBool("render_hierarchy", d.RenderHierarchy),
			ShowUnitNames = map.GetBool("show_unit_names", d.ShowUnitNames),
			LastPlayer = root.GetString("lastplayer", d.LastPlayer),
			LastHost = root.GetString("lasthost", d.LastHost),
			ServerAddress = root.GetString("serveradress", d.ServerAddress),
			DebugSaves = root.GetInt("debug_saves", d.DebugSaves),
			Autosave = autosave,
			Simple = root.GetBool("simple", d.Simple),
			UseLegacyCamera = root.GetBool("use_legacy_camera", d.UseLegacyCamera),
			Categories = Numbers(root.Get("categories")) ?? CUserSettings.DefaultCategories,
			Outliner = Numbers(root.Get("outliner")) ?? CUserSettings.DefaultOutliner,
		};
	}

	/// <summary>The autosave frequencies in the original's order, nought to five: the game options' Game tab steps through them.</summary>
	public static readonly string[] AutosaveNames = { "NEVER", "WEEKLY", "MONTHLY", "HALFYEAR", "YEARLY", "FIVE_YEAR" };

	/// <summary>A block of bare whole numbers, the key's whole list replacing the default; null with no such block.</summary>
	private static List<int>? Numbers(ClausewitzNode? node)
	{
		if (node is not { IsBlock: true })
		{
			return null;
		}
		var numbers = new List<int>();
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is null && int.TryParse(child.Value, System.Globalization.NumberStyles.Integer,
				System.Globalization.CultureInfo.InvariantCulture, out int number))
			{
				numbers.Add(number);
			}
		}
		return numbers;
	}
}
