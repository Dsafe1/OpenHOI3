using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Reads <c>common/on_actions.txt</c>: for each of the thirteen on_actions, its events' ids in file
/// order. The weight written before each is never read, by the original either. An on_action no
/// place in the game fires and an entry that is no number are reported, and so - once for each
/// on_action, with how many - are ids no event has, which the original passes over in silence. See
/// <c>docs/mechanics/events.md</c>, section 8.
/// </summary>
public static class COnActionReader
{
	public const string File = "common/on_actions.txt";

	/// <summary>The thirteen the original's code asks for by name.</summary>
	public static readonly string[] Known =
	{
		"on_surrender", "on_nuke_dropped", "on_battle_won", "on_battle_lost", "on_naval_battle_won",
		"on_naval_battle_lost", "on_air_battle_won", "on_air_battle_lost", "on_encirclement",
		"on_enemy_pride_sunk", "on_our_pride_sunk", "on_coup_success", "on_coup_fail",
	};

	/// <summary>The on_actions of the install, checked against the events already read.</summary>
	public static Dictionary<string, IReadOnlyList<int>> Read(CDirectorySettings directories, CScriptContext context,
		IReadOnlyList<CEvent> events)
	{
		string path = directories.Resolve(File);
		if (!System.IO.File.Exists(path))
		{
			return new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
		}
		context.Problems.File = Path.GetRelativePath(directories.GameRoot, path).Replace('\\', '/');
		return Read(CScriptParser.ParseFile(path, context.Problems), context, events);
	}

	/// <summary>The on_actions of a file already parsed.</summary>
	public static Dictionary<string, IReadOnlyList<int>> Read(CScriptNode root, CScriptContext context, IReadOnlyList<CEvent> events)
	{
		var known = new HashSet<int>();
		foreach (CEvent read in events)
		{
			known.Add(read.Id);
		}
		var onActions = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
		foreach (CScriptNode block in root.Children)
		{
			if (!block.IsBlock || Array.IndexOf(Known, block.Key) < 0)
			{
				context.Problems.Add(EScriptProblem.UnknownKey, block.Line,
					$"'{block.Key}' is no on_action; the original asks for {Known.Length} by name and never for this one");
				continue;
			}
			var ids = new List<int>();
			var missing = new List<int>();
			foreach (CScriptNode entry in block.Children)
			{
				if (entry.Value is null || !int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
				{
					context.Problems.Add(EScriptProblem.WrongShape, entry.Line,
						$"an entry of {block.Key} is '{entry}', where a weight and an event's id are written");
					continue;
				}
				ids.Add(id);
				if (!known.Contains(id))
				{
					missing.Add(id);
				}
			}
			if (missing.Count > 0)
			{
				string some = string.Join(", ", missing.GetRange(0, Math.Min(5, missing.Count)));
				context.Problems.Add(EScriptProblem.UnknownName, block.Line,
					$"{missing.Count} of {block.Key}'s {ids.Count} ids are no event ({some}{(missing.Count > 5 ? ", ..." : "")}); "
					+ "the original passes them over");
			}
			// Ours: a second block of one name is passed over; which one the original finds was not read. Said.
			if (!onActions.TryAdd(block.Key, ids))
			{
				context.Problems.Add(EScriptProblem.Structure, block.Line,
					$"{block.Key} is given again; the first stands and this one is passed over");
			}
		}
		return onActions;
	}
}
