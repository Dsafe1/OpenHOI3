using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Settings;

/// <summary>
/// Reads the player's <c>settings.txt</c> from <c>Documents\Paradox Interactive\Hearts of Iron III</c> - the one the
/// original writes, and not the one beside its executable - and never writes it. See <c>docs/mechanics/settings.md</c>.
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
		ClausewitzNode? map = root.Get("mapRenderingOptions");
		bool alwaysCounters = map?.GetBool("alwaysCounters", true) ?? true;
		if (!alwaysCounters)
		{
			warn($"'{path}': alwaysCounters = no is not supported; the map shows counters (docs/compatibility.md, entry 12)");
		}
		return new CUserSettings
		{
			Path = path,
			Language = root.Get("gui")?.GetString("language", CUserSettings.Default.Language) ?? CUserSettings.Default.Language,
			AlwaysCounters = alwaysCounters,
			CounterScale = map?.GetFloat("counter_scale", CUserSettings.Default.CounterScale) ?? CUserSettings.Default.CounterScale,
			CounterDistance = map?.GetFloat("counter_distance", CUserSettings.Default.CounterDistance)
				?? CUserSettings.Default.CounterDistance,
			Outliner = Numbers(root.Get("outliner")) ?? CUserSettings.DefaultOutliner,
		};
	}

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
