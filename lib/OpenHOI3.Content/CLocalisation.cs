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

	private readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);

	public int Count => _entries.Count;

	/// <summary>Where each key's text came from, <c>file:line</c>, for the report of a later line of it.</summary>
	private readonly Dictionary<string, string> _firstAt = new(StringComparer.Ordinal);

	/// <summary>How many lines gave a key that already had text, and were dropped as the original drops them.</summary>
	public int Duplicates { get; private set; }

	/// <summary>How many lines gave a key no text in the language read, which the original logs as an empty key.</summary>
	public int Empties { get; private set; }

	public static CLocalisation Load(CDirectorySettings directories,
		Language language = Language.English, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var localisation = new CLocalisation();
		// The original reads its tables in the order PhysFS lists them: every name over all the layers once, each its
		// frontmost copy, sorted by byte with case - not the base layer first (reversing/findings/FINDINGS-localisation.md,
		// section 3).
		string[] files = directories.ListFiles("localisation", "*.csv");
		Array.Sort(files, (a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
		foreach (string file in files)
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

		string language = ((Language)column).ToString();
		int number = 0;
		foreach (string rawLine in text.Split('\n'))
		{
			number++;
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
				// The original logs it as an empty key; the key shows as itself unless a later line gives it text.
				Empties++;
				warn(CDiagnostics.Located(absolutePath, number, $"'{key}' has no {language} text"));
				continue;
			}
			// A table writes a line break as a backslash and an n, and the original's text
			// breaks there: RES_CHANGE ends in one, and its lines stack. The original turns it
			// into one as it reads the line.
			value = value.Replace("\\n", "\n");

			// The first text a key is given stands: the original reads every table as one text, the files in the order
			// they are listed, and drops a later line of a key that already has text, logging it as a duplicate
			// (reversing/findings/FINDINGS-localisation.md, section 2). A line with no text in the language gives none and
			// leaves the key to a later one.
			if (_entries.TryAdd(key, value))
			{
				_firstAt[key] = $"{absolutePath}:{number}";
			}
			else
			{
				Duplicates++;
				warn(CDiagnostics.Located(absolutePath, number,
					$"'{key}' is given again and dropped; its first text, at {_firstAt[key]}, stands"));
			}
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

	/// <summary>The text of a key, or null when no table has it - for a caller with something else to try.</summary>
	public string? Find(string key)
	{
		return _entries.TryGetValue(key, out string? value) ? value : null;
	}
}
