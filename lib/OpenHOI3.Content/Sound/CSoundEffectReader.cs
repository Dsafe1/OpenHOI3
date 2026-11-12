using System;
using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Sound;

/// <summary>
/// The original's sound effects: <c>interface/sound.sfx</c> through the install's layers, one file, every top-level key a
/// sound named by it - looked up without case, the first of a name winning - with <c>file</c>, <c>pitch</c>, <c>pan</c>,
/// <c>voicetype</c> and <c>volume</c> (<c>reversing/findings/FINDINGS-soundeffects.md</c>, section 2). What it does not
/// know is reported.
/// </summary>
public static class CSoundEffectReader
{
	public const string FilePath = "interface/sound.sfx";

	/// <summary>The folder a sound's <c>file</c> is in.</summary>
	public const string Folder = "sound/";

	private static readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase)
	{
		"file", "pitch", "pan", "voicetype", "volume",
	};

	public static Dictionary<string, CSoundEffect> Load(CDirectorySettings directories, Action<string> warn)
	{
		string path = directories.Resolve(FilePath);
		if (!System.IO.File.Exists(path))
		{
			warn($"sound: {FilePath} is missing; no sound effect plays");
			return new Dictionary<string, CSoundEffect>(StringComparer.OrdinalIgnoreCase);
		}
		return Read(ClausewitzParser.ParseFile(path), warn);
	}

	public static Dictionary<string, CSoundEffect> Read(ClausewitzNode root, Action<string> warn)
	{
		var sounds = new Dictionary<string, CSoundEffect>(StringComparer.OrdinalIgnoreCase);
		foreach (ClausewitzNode node in root.Children)
		{
			if (node.Key is not { Length: > 0 } name || !node.IsBlock)
			{
				continue;
			}
			foreach (ClausewitzNode key in node.Children)
			{
				if (key.Key is null || !Keys.Contains(key.Key))
				{
					warn($"sound: {FilePath}, {name}: unknown key '{key.Key}'");
				}
			}
			int pitch = node.GetInt("pitch", -1);
			int pan = node.GetInt("pan");
			var sound = new CSoundEffect(name, Folder + node.GetString("file"),
				pitch == -1 ? -1 : Math.Clamp(pitch, 100, 100_000),
				Math.Clamp(pan, -10_000, 10_000),
				// Exactly "single", case and all [exe].
				string.Equals(node.GetString("voicetype"), "single", StringComparison.Ordinal),
				node.GetInt("volume", 100));
			if (!sounds.TryAdd(name, sound))
			{
				warn($"sound: {FilePath}: duplicate sound '{name}', the first kept");
			}
		}
		return sounds;
	}
}
