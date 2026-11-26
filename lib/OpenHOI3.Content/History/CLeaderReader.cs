using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;

namespace OpenHOI3.Content.History;

/// <summary>
/// Reads <c>history/leaders</c>: a block for each leader, named by his number, giving
/// his name, his country, his kind, his skill, the skill he learns towards, his
/// loyalty, his portrait and his traits, and a <c>history</c> of dated changes - of
/// rank, loyalty and traits; a dated <c>skill</c> is read and does nothing, as in the
/// original. His history is folded to the start date. See
/// <c>docs/mechanics/leaders.md</c>, section 1.
///
/// The files are read in the order <see cref="CDirectorySettings.ListFiles"/> gives,
/// and each country keeps its leaders in the order they were read - which is the order
/// it puts them in service and so how a tie between two is broken.
/// </summary>
public static class CLeaderReader
{
	private const string LeaderDirectory = "history/leaders";

	public static void Read(CDirectorySettings directories, CGameState state, CDate start, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var traits = new Dictionary<string, CTrait>(StringComparer.OrdinalIgnoreCase);
		foreach (CTrait trait in state.Traits)
		{
			traits.TryAdd(trait.Key, trait);
		}
		var problems = new Problems();
		var firstRead = new Dictionary<int, string>();
		foreach (string path in directories.ListFiles(LeaderDirectory, "*.txt"))
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(path);
			}
			catch (Exception e) when (e is IOException or InvalidDataException)
			{
				warn(CDiagnostics.Located(path, 0, $"could not read the file: {e.Message}"));
				continue;
			}
			problems.File = path;
			foreach (ClausewitzNode block in root.Children)
			{
				if (block.Key is null || !block.IsBlock || !int.TryParse(block.Key, out int id))
				{
					// Said, where it was passed over: a leader is a number and a block (clausewitz.md, section 3).
					string what = block.Key ?? block.Value ?? "";
					problems.UnknownKeys.Add($"{what} (no leader: a number and a block)");
					problems.Add(block.Line, $"'{what}' is no leader: a number and a block");
					continue;
				}
				CLeader leader = Read(block, id, traits, start, problems);
				if (!state.Leaders.TryAdd(id, leader))
				{
					problems.Duplicates.Add(id);
					problems.Add(block.Line, $"the leader {id} is numbered twice; the first read, at {firstRead[id]}, stands");
					continue;
				}
				firstRead[id] = block.Line > 0 ? $"{path}:{block.Line}" : path;
				if (leader.Country is { } tag && state.Country(tag) is { } country)
				{
					country.Leaders.Add(leader);
				}
				else
				{
					string named = leader.Country?.Value ?? "(none)";
					problems.UnknownCountries.Add(named);
					ClausewitzNode? given = block.Get("country");
					problems.Add(given?.Line ?? block.Line, $"the leader {id} is given to '{named}', a country that is not defined");
				}
			}
		}
		problems.Report(warn);
	}

	/// <summary>One leader's block, his history folded to <paramref name="start"/>.</summary>
	public static CLeader Read(ClausewitzNode block, int id, IReadOnlyDictionary<string, CTrait> traits, CDate start,
		Problems? problems = null)
	{
		problems ??= new Problems();
		var leader = new CLeader(id);
		ClausewitzNode? history = null;
		foreach (ClausewitzNode entry in block.Children)
		{
			string key = entry.Key?.ToLowerInvariant() ?? "";
			switch (key)
			{
				case "name":
					leader.Name = entry.Value ?? "";
					break;
				case "country":
					if (CCountryTag.TryParse(entry.Value, out CCountryTag tag))
					{
						leader.Country = tag;
					}
					break;
				case "type":
					leader.Type = (entry.Value ?? "").ToLowerInvariant() switch
					{
						"sea" => ELeaderType.Sea,
						"air" => ELeaderType.Air,
						"land" => ELeaderType.Land,
						_ => Unknown(problems, entry, $"type {entry.Value}", $"type '{entry.Value}' is not understood; read as land",
							ELeaderType.Land),
					};
					break;
				case "skill":
					leader.Skill = Whole(entry.Value);
					if (leader.StartingSkill < 0)
					{
						leader.StartingSkill = leader.Skill;
					}
					break;
				case "max_skill":
					leader.MaxSkill = Whole(entry.Value);
					break;
				case "loyalty":
					leader.Loyalty = Number(entry.Value);
					break;
				case "picture":
					leader.Picture = entry.Value ?? "";
					break;
				case "rank":
					leader.Rank = Whole(entry.Value);
					break;
				case "experience":
					leader.StartingExperience = (int)Number(entry.Value).Raw;
					break;
				case "add_trait":
				case "trait":
					AddTrait(leader, entry, traits, problems);
					break;
				case "history":
					history = entry;
					break;
				case "id":
				case "current_experience":
				case "gainable_trait":
					// What a savegame holds of a leader; nothing in a history file has them.
					break;
				default:
					problems.UnknownKeys.Add(key);
					problems.Add(entry.Line, $"'{entry.Key ?? entry.Value}' is a key nothing reads");
					break;
			}
		}
		if (history is not null)
		{
			ApplyHistory(leader, history, traits, start, problems);
		}
		CLeaders.SettleSkill(leader);
		return leader;
	}

	/// <summary>
	/// What a leader's history makes of him by the start, in date order; and the day it
	/// first gives him a rank, which is when he is put in service if the start has not.
	/// </summary>
	private static void ApplyHistory(CLeader leader, ClausewitzNode history, IReadOnlyDictionary<string, CTrait> traits,
		CDate start, Problems problems)
	{
		foreach (ClausewitzNode block in history.Children)
		{
			if (block.IsBlock && CDate.TryParse(block.Key, out CDate when) && block.Get("rank") is { } rank
				&& (leader.FirstRankDate is not { } first || when.CompareTo(first) < 0))
			{
				leader.FirstRankDate = when;
				leader.FirstRank = Whole(rank.Value);
			}
		}
		foreach (ClausewitzNode entry in CHistory.Fold(history, start))
		{
			switch (entry.Key?.ToLowerInvariant())
			{
				case "rank":
					leader.Rank = Whole(entry.Value);
					break;
				case "loyalty":
					leader.Loyalty = Number(entry.Value);
					break;
				case "add_trait":
					AddTrait(leader, entry, traits, problems);
					break;
				case "remove_trait":
					if (entry.Value is not null && traits.TryGetValue(entry.Value, out CTrait? gone))
					{
						leader.Traits.Remove(gone);
					}
					break;
				case "skill":
					// Read by the original and applied to nothing.
					break;
				default:
					problems.UnknownKeys.Add($"history {entry.Key}");
					problems.Add(entry.Line, $"history: '{entry.Key ?? entry.Value}' is a key nothing reads");
					break;
			}
		}
	}

	private static void AddTrait(CLeader leader, ClausewitzNode entry, IReadOnlyDictionary<string, CTrait> traits,
		Problems problems)
	{
		string? name = entry.Value;
		if (name is not null && traits.TryGetValue(name, out CTrait? trait))
		{
			// Whether the trait is meant for his kind is not asked here, as in the original.
			leader.Traits.Add(trait);
		}
		else
		{
			problems.UnknownTraits.Add(name ?? "");
			problems.Add(entry.Line, $"'{name ?? ""}' is a trait that is not defined");
		}
	}

	private static T Unknown<T>(Problems problems, ClausewitzNode entry, string what, string message, T fallback)
	{
		problems.UnknownKeys.Add(what);
		problems.Add(entry.Line, message);
		return fallback;
	}

	private static int Whole(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? (int)(value.Raw / CFixedPoint.Scale) : 0;
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}

	/// <summary>
	/// What the files got wrong: each kind gathered by name, and each fault a line of its own at its file and line
	/// (<c>docs/mechanics/logs.md</c>, section 4), said in the order found.
	/// </summary>
	public sealed class Problems
	{
		private readonly HashSet<string> _said = new(StringComparer.Ordinal);

		public SortedSet<string> UnknownKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
		public SortedSet<string> UnknownTraits { get; } = new(StringComparer.OrdinalIgnoreCase);
		public SortedSet<string> UnknownCountries { get; } = new(StringComparer.OrdinalIgnoreCase);
		public SortedSet<int> Duplicates { get; } = new();

		/// <summary>The file being read, which each fault found from now on is placed in.</summary>
		public string File { get; set; } = "";

		/// <summary>Each fault as its line of the validation log, once.</summary>
		public List<string> Lines { get; } = new();

		public void Add(int line, string message)
		{
			string located = CDiagnostics.Located(File, line, message);
			if (_said.Add(located))
			{
				Lines.Add(located);
			}
		}

		public void Report(Action<string> warn)
		{
			foreach (string line in Lines)
			{
				warn(line);
			}
		}
	}
}
