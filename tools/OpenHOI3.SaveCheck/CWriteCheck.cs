using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;


/// <summary>
/// Loads a save, writes it straight back and sets the two beside each other:
///
///   OpenHOI3.SaveCheck write &lt;install folder&gt; &lt;savegame&gt; [written file] [keys=text,text]
///   OpenHOI3.SaveCheck write &lt;install folder&gt; new &lt;start-date savegame&gt; [written file] [keys=text,text]
///
/// every province block byte for byte, and every block key by key - what the save has that was not written, what was
/// written that the save has not, the values that differ, and keys out of the save's order - tallied by the kind of
/// block, with the first few of each, or every key whose path holds one of the texts given. With <c>new</c>, a new game
/// is written instead and set beside the start save. Nothing of the install or the save is changed. See
/// <c>docs/mechanics/savegame.md</c>, sections 8 and 9.
/// </summary>
public static class CWriteCheck
{
	private sealed class CTally
	{
		public readonly SortedDictionary<string, int> Missing = new(StringComparer.Ordinal);
		public readonly SortedDictionary<string, int> Extra = new(StringComparer.Ordinal);
		public readonly SortedDictionary<string, int> Differ = new(StringComparer.Ordinal);
		public readonly Dictionary<string, List<string>> Examples = new(StringComparer.Ordinal);
		public int OutOfOrder;
		public int Blocks;
		public int Same;

		public void Add(SortedDictionary<string, int> into, string key, string example)
		{
			into[key] = into.GetValueOrDefault(key) + 1;
			List<string> list = Examples.TryGetValue(key, out List<string>? had) ? had : Examples[key] = new List<string>();
			if (list.Count < 2)
			{
				list.Add(example);
			}
		}
	}

	public static int Run(string[] args)
	{
		if (args.Length < 3 || args[2] == "new" && args.Length < 4)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck write <install folder> <savegame | new start-date savegame> [written file] [keys=..]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		// With "new", a new game as a player gets it, the start save's country played, written beside that save: meant
		// to differ where the original's AI has moved by then, and there to show the layout and the numbering.
		bool fresh = args[2] == "new";
		string savePath = fresh ? args[3] : args[2];
		string[] rest = args.Skip(fresh ? 4 : 3).ToArray();
		CGameState state;
		if (fresh)
		{
			using var scripts = new OpenHOI3.Lua.CAiScripts(directories, warnings.Add);
			ClausewitzNode start = ClausewitzParser.ParseFile(savePath);
			state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts,
				played: new[] { new OpenHOI3.Sim.Countries.CCountryTag(start.GetString("player")) },
				date: CStartSave.Date(savePath));
		}
		else
		{
			(state, CSaveReport _) = CSaveLoader.Load(directories, map, localisation, countries, savePath, warn: warnings.Add);
		}
		var clock = Stopwatch.StartNew();
		string written = CSaveWriter.Write(state, localisation.Localise);
		Console.WriteLine($"written in {clock.ElapsedMilliseconds} ms, {written.Length:N0} characters");
		// keys=<text>,<text> lists every key whose path holds one of the texts, where otherwise only the commonest are.
		s_keys = rest.FirstOrDefault(arg => arg.StartsWith("keys=", StringComparison.Ordinal))?[5..].Split(',');
		string path = rest.FirstOrDefault(arg => !arg.StartsWith("keys=", StringComparison.Ordinal))
			?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openhoi3_written.hoi3");
		System.IO.File.WriteAllBytes(path, Encoding.Latin1.GetBytes(written));
		Console.WriteLine($"to {path}");

		string original = Encoding.Latin1.GetString(System.IO.File.ReadAllBytes(savePath));
		CompareProvinceBytes(original, written, state.Provinces.Count);

		ClausewitzNode saved = ClausewitzParser.Parse(original);
		ClausewitzNode ours = ClausewitzParser.Parse(written);
		var tallies = new SortedDictionary<string, CTally>(StringComparer.Ordinal);
		CTally Tally(string kind) => tallies.TryGetValue(kind, out CTally? had) ? had : tallies[kind] = new CTally();

		// The top level: each key of ours set beside the save's of the same key and occurrence.
		CTally top = Tally("top");
		CompareChildren(saved, ours, "", top, Kind);
		foreach ((string kind, CTally tally) in tallies)
		{
			Console.WriteLine($"{kind}: {tally.Same} of {tally.Blocks} blocks the same, {tally.OutOfOrder} out of the save's order");
			Lines("  in the save, not written", tally.Missing, tally);
			Lines("  written, not in the save", tally.Extra, tally);
			Lines("  written otherwise", tally.Differ, tally);
		}
		return 0;

		string Kind(ClausewitzNode node)
		{
			if (int.TryParse(node.Key, out _))
			{
				return "province";
			}
			return node.Key is { Length: 3 } key && key.All(char.IsUpper) ? "country" : "top";
		}

		void CompareChildren(ClausewitzNode save, ClausewitzNode mine, string path, CTally tally, Func<ClausewitzNode, string>? kindOf)
		{
			var savedByKey = new Dictionary<string, List<ClausewitzNode>>(StringComparer.Ordinal);
			var savedOrder = new List<string>();
			foreach (ClausewitzNode child in save.Children)
			{
				string key = child.Key ?? "{}";
				(savedByKey.TryGetValue(key, out List<ClausewitzNode>? list) ? list : savedByKey[key] = new List<ClausewitzNode>()).Add(child);
				savedOrder.Add(key);
			}
			var seen = new Dictionary<string, int>(StringComparer.Ordinal);
			int lastIndex = -1;
			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (ClausewitzNode child in mine.Children)
			{
				string key = child.Key ?? "{}";
				int occurrence = seen.GetValueOrDefault(key);
				seen[key] = occurrence + 1;
				used.Add(key);
				CTally into = kindOf is null ? tally : Tally(kindOf(child));
				string where = kindOf is null ? path + "/" + key : key;
				if (!savedByKey.TryGetValue(key, out List<ClausewitzNode>? matches) || occurrence >= matches.Count)
				{
					into.Add(into.Extra, NameOf(path, key, kindOf is not null), where);
					continue;
				}
				ClausewitzNode match = matches[occurrence];
				int index = save.Children.IndexOf(match);
				if (index < lastIndex)
				{
					into.OutOfOrder++;
				}
				lastIndex = Math.Max(lastIndex, index);
				if (kindOf is not null)
				{
					into.Blocks++;
					int before = into.Missing.Values.Sum() + into.Extra.Values.Sum() + into.Differ.Values.Sum();
					CompareNode(match, child, key, into);
					if (into.Missing.Values.Sum() + into.Extra.Values.Sum() + into.Differ.Values.Sum() == before)
					{
						into.Same++;
					}
				}
				else
				{
					CompareNode(match, child, where, into);
				}
			}
			foreach ((string key, List<ClausewitzNode> matches) in savedByKey)
			{
				int written = seen.GetValueOrDefault(key);
				for (int i = written; i < matches.Count; i++)
				{
					CTally into = kindOf is null ? tally : Tally(kindOf(matches[i]));
					into.Add(into.Missing, NameOf(path, key, kindOf is not null), kindOf is null ? path + "/" + key : key);
				}
			}
		}

		void CompareNode(ClausewitzNode save, ClausewitzNode mine, string path, CTally tally)
		{
			if (save.IsBlock != mine.IsBlock)
			{
				tally.Add(tally.Differ, Generic(path), $"{path}: a block on one side only");
				return;
			}
			if (!save.IsBlock)
			{
				if (!string.Equals(save.Value, mine.Value, StringComparison.Ordinal))
				{
					tally.Add(tally.Differ, Generic(path), $"{path}: save {save.Value}, ours {mine.Value}");
				}
				return;
			}
			// A list of bare values is set beside as a whole.
			if (save.Children.All(c => c.Key is null && !c.IsBlock) && mine.Children.All(c => c.Key is null && !c.IsBlock))
			{
				string a = string.Join(' ', save.Children.Select(c => c.Value));
				string b = string.Join(' ', mine.Children.Select(c => c.Value));
				if (a != b)
				{
					tally.Add(tally.Differ, Generic(path), $"{path}: save {Cut(a)}, ours {Cut(b)}");
				}
				return;
			}
			CompareChildren(save, mine, path, tally, null);
		}
	}

	/// <summary>The kind of a key for the tally: a block's own key under its kind, a province's number and a tag folded.</summary>
	private static string NameOf(string path, string key, bool topLevel)
	{
		return topLevel ? Generic(key) : Generic(path + "/" + key);
	}

	private static string Generic(string path)
	{
		var parts = path.Split('/').Select(part =>
			int.TryParse(part, out _) ? "#" : part.Length == 3 && part.All(char.IsUpper) ? "TAG" : part);
		return string.Join('/', parts.Skip(1).DefaultIfEmpty(parts.FirstOrDefault() ?? ""));
	}

	private static string Cut(string text) => text.Length > 60 ? text[..60] + "..." : text;

	/// <summary>With <c>keys=</c>, the texts one of which a key's path must hold to be listed, and then every such key is.</summary>
	private static string[]? s_keys;

	private static void Lines(string what, SortedDictionary<string, int> counts, CTally tally)
	{
		var shown = counts.Where(pair => s_keys is null || s_keys.Any(text => pair.Key.Contains(text, StringComparison.Ordinal)))
			.OrderByDescending(pair => pair.Value).ToList();
		int limit = s_keys is null ? 25 : int.MaxValue;
		foreach ((string key, int count) in shown.Take(limit))
		{
			string example = tally.Examples.TryGetValue(key, out List<string>? list) ? list[0] : "";
			Console.WriteLine($"{what}: {key} x{count}   {Cut(example)}");
		}
		if (shown.Count > limit)
		{
			Console.WriteLine($"{what}: and {shown.Count - limit} more keys");
		}
	}

	/// <summary>Each province's block as text in both files, set beside byte for byte.</summary>
	private static void CompareProvinceBytes(string original, string written, int provinces)
	{
		int same = 0, compared = 0;
		string? firstDiffering = null;
		int fromA = 0, fromB = 0;
		for (int id = 1; id < provinces; id++)
		{
			string head = $"\r\n{id}=\r\n{{";
			string next = $"\r\n{id + 1}=\r\n{{";
			int a = original.IndexOf(head, fromA, StringComparison.Ordinal);
			int b = written.IndexOf(head, fromB, StringComparison.Ordinal);
			if (a < 0 || b < 0)
			{
				continue;
			}
			int aEnd = id + 1 < provinces ? original.IndexOf(next, a, StringComparison.Ordinal) : -1;
			int bEnd = id + 1 < provinces ? written.IndexOf(next, b, StringComparison.Ordinal) : -1;
			if (aEnd < 0 || bEnd < 0)
			{
				continue;
			}
			compared++;
			if (string.CompareOrdinal(original, a, written, b, Math.Max(aEnd - a, bEnd - b)) == 0 && aEnd - a == bEnd - b)
			{
				same++;
			}
			else
			{
				firstDiffering ??= id.ToString(System.Globalization.CultureInfo.InvariantCulture);
			}
			fromA = aEnd;
			fromB = bEnd;
		}
		Console.WriteLine($"province blocks byte for byte: {same} of {compared} the same" +
			(firstDiffering is null ? "" : $"; the first that differs is {firstDiffering}"));
	}
}
