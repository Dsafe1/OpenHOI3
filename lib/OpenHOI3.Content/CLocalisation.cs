using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenHOI3.Content;

/// <summary>
/// The game's string table. Every file in <c>localisation/</c> is a semicolon
/// separated table whose first column is the key and whose remaining columns are one
/// language each.
/// </summary>
public sealed class CLocalisation
{
	/// <summary>Column index per language, in the order the game files store them.</summary>
	public enum Language
	{
		English = 1,
		French = 2,
		German = 3,
		Polish = 4,
		Spanish = 5,
		Italian = 6,
	}

	private readonly Dictionary<string, string> _entries = new(StringComparer.OrdinalIgnoreCase);

	public int Count => _entries.Count;

	public static CLocalisation Load(CDirectorySettings directories,
		Language language = Language.English, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var localisation = new CLocalisation();
		foreach (string file in directories.ListFiles("localisation", "*.csv"))
		{
			localisation.LoadFile(file, (int)language, warn);
		}
		return localisation;
	}

	private void LoadFile(string absolutePath, int column, Action<string> warn)
	{
		string text;
		try
		{
			text = Encoding.Latin1.GetString(File.ReadAllBytes(absolutePath));
		}
		catch (IOException e)
		{
			warn($"Could not read localisation '{absolutePath}': {e.Message}");
			return;
		}

		foreach (string rawLine in text.Split('\n'))
		{
			string line = rawLine.TrimEnd('\r');
			if (line.Length == 0 || line[0] == '#')
			{
				continue;
			}
			string[] columns = line.Split(';');
			if (columns.Length <= column)
			{
				continue;
			}
			string key = columns[0].Trim();
			if (key.Length == 0)
			{
				continue;
			}
			string value = columns[column];
			if (value.Length == 0)
			{
				continue;
			}
			// Later files patch earlier ones, matching the game's load order.
			_entries[key] = value;
		}
	}

	/// <summary>
	/// Resolves a key, returning it unchanged when absent so unlocalised UI still
	/// renders something identifiable rather than going blank.
	/// </summary>
	public string Localise(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return string.Empty;
		}
		return _entries.TryGetValue(key, out string? value) ? value : key;
	}
}
