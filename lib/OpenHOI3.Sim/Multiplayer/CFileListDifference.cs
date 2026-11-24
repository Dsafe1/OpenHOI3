using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHOI3.Sim.Multiplayer;

/// <summary>
/// Where two machines' lists of the files their play checksums are made of differ (<c>docs/multiplayer.md</c>, section
/// 15; the list is <c>mechanics/checksum.md</c>, section 8's: a line a file, its path, its length and the head of its
/// hash, tabs between, under three lines for the format, the engine's version and the rules).
/// </summary>
public sealed class CFileListDifference
{
	/// <summary>The lines before the files, by their first word.</summary>
	private static readonly string[] Heads = { "format", "engine", "rules" };

	private const string Last = "checksum";

	/// <summary>Files this machine has and the host has not, by path.</summary>
	public IReadOnlyList<string> OnlyHere { get; }

	/// <summary>Files the host has and this machine has not.</summary>
	public IReadOnlyList<string> OnlyHost { get; }

	/// <summary>Files both have, of another length or content.</summary>
	public IReadOnlyList<string> Changed { get; }

	/// <summary>The format, the engine's version or the rules, where they differ: <c>engine: here 1.2, host 1.3</c>.</summary>
	public IReadOnlyList<string> Settings { get; }

	public int Count => OnlyHere.Count + OnlyHost.Count + Changed.Count + Settings.Count;

	private CFileListDifference(List<string> onlyHere, List<string> onlyHost, List<string> changed, List<string> settings)
	{
		OnlyHere = onlyHere;
		OnlyHost = onlyHost;
		Changed = changed;
		Settings = settings;
	}

	/// <summary>This machine's list set beside the host's.</summary>
	public static CFileListDifference Compare(IEnumerable<string> here, IEnumerable<string> host)
	{
		Dictionary<string, string> own = Read(here);
		Dictionary<string, string> theirs = Read(host);
		var onlyHere = new List<string>();
		var onlyHost = new List<string>();
		var changed = new List<string>();
		var settings = new List<string>();
		foreach ((string path, string figures) in own)
		{
			if (path == Last)
			{
				continue;
			}
			bool head = Array.IndexOf(Heads, path) >= 0;
			if (!theirs.TryGetValue(path, out string? other))
			{
				(head ? settings : onlyHere).Add(head ? $"{path}: here {Shown(figures)}, host nothing" : path);
			}
			else if (other != figures)
			{
				(head ? settings : changed).Add(head ? $"{path}: here {Shown(figures)}, host {Shown(other)}" : path);
			}
		}
		foreach ((string path, string figures) in theirs)
		{
			if (path != Last && !own.ContainsKey(path))
			{
				bool head = Array.IndexOf(Heads, path) >= 0;
				(head ? settings : onlyHost).Add(head ? $"{path}: here nothing, host {Shown(figures)}" : path);
			}
		}
		onlyHere.Sort(StringComparer.Ordinal);
		onlyHost.Sort(StringComparer.Ordinal);
		changed.Sort(StringComparer.Ordinal);
		return new CFileListDifference(onlyHere, onlyHost, changed, settings);
	}

	private static string Shown(string figures) => figures.Length == 0 ? "none" : figures;

	private static Dictionary<string, string> Read(IEnumerable<string> lines)
	{
		var read = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (string line in lines)
		{
			int tab = line.IndexOf('\t');
			if (line.Length > 0)
			{
				read[tab < 0 ? line : line[..tab]] = tab < 0 ? "" : line[(tab + 1)..];
			}
		}
		return read;
	}

	/// <summary>
	/// The whole of it as lines: what differs of the settings, then the three lists under a heading each - what is
	/// written beside the logs.
	/// </summary>
	public IReadOnlyList<string> Report()
	{
		var lines = new List<string>(Settings);
		Add(lines, "differing", Changed);
		Add(lines, "only on this machine", OnlyHere);
		Add(lines, "only on the host", OnlyHost);
		return lines;

		static void Add(List<string> lines, string heading, IReadOnlyList<string> paths)
		{
			if (paths.Count > 0)
			{
				lines.Add($"{heading} ({paths.Count}):");
				lines.AddRange(paths.Select(path => "  " + path));
			}
		}
	}

	/// <summary>
	/// It in a line for a player: how many files differ, are here alone and are the host's alone, what differs of the
	/// settings, and the first <paramref name="named"/> paths.
	/// </summary>
	public string Summary(int named)
	{
		var parts = new List<string>(Settings);
		if (Changed.Count > 0)
		{
			parts.Add($"{Changed.Count} differing");
		}
		if (OnlyHere.Count > 0)
		{
			parts.Add($"{OnlyHere.Count} only here");
		}
		if (OnlyHost.Count > 0)
		{
			parts.Add($"{OnlyHost.Count} only on the host");
		}
		if (parts.Count == 0)
		{
			return "no file differs";
		}
		List<string> paths = Changed.Concat(OnlyHere).Concat(OnlyHost).ToList();
		string first = string.Join(", ", paths.Take(named));
		string more = paths.Count > named ? $" and {paths.Count - named} more" : "";
		return string.Join("; ", parts) + (first.Length > 0 ? ": " + first + more : "");
	}
}
