using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads <c>common/governments.txt</c> and <c>common/government_positions.txt</c>: each government's accepted
/// ideologies, elected posts, elections, term and modifier, and the cabinet posts in their order. See
/// <c>docs/mechanics/politics.md</c>, section 1.
/// </summary>
public static class CGovernmentReader
{
	private const string GovernmentsFile = "common/governments.txt";
	private const string PositionsFile = "common/government_positions.txt";

	/// <summary>A post's own key besides the kinds of modifier.</summary>
	private static readonly HashSet<string> PositionKeys = new(StringComparer.OrdinalIgnoreCase) { "changeable" };

	/// <summary>The cabinet posts, numbered from 1 in the file's order; <c>changeable = no</c> is a fixed one.</summary>
	public static List<CGovernmentPosition> ReadPositions(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var positions = new List<CGovernmentPosition>();
		if (Parse(directories, PositionsFile, warn) is not { } root)
		{
			return positions;
		}
		// A post is a modifier besides: what of it is a kind is read and not kept, and a key that is none is reported.
		Sim.Modifiers.CModifierKeys kinds = CModifierReader.Kinds(directories);
		var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			bool changeable = !block.GetString("changeable").Equals("no", StringComparison.OrdinalIgnoreCase);
			positions.Add(new CGovernmentPosition(block.Key, positions.Count + 1, changeable));
			CModifierReader.ReadBlock(block, PositionKeys, kinds, unknown, out _);
		}
		CModifierReader.ReportUnknown(warn, PositionsFile, "government positions", unknown);
		return positions;
	}

	/// <summary>
	/// The governments, by name: an ideology or a post said <c>yes</c> to is accepted or elected, <c>election</c>,
	/// <c>duration</c> and <c>valid_for_new_country</c> are its own, and every other key with a number is its modifier.
	/// </summary>
	public static Dictionary<string, CGovernment> Read(CDirectorySettings directories, IEnumerable<string> ideologies,
		IEnumerable<CGovernmentPosition> positions, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var governments = new Dictionary<string, CGovernment>(StringComparer.OrdinalIgnoreCase);
		if (Parse(directories, GovernmentsFile, warn) is not { } root)
		{
			return governments;
		}
		var ideologyNames = new HashSet<string>(ideologies, StringComparer.OrdinalIgnoreCase);
		var positionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CGovernmentPosition position in positions)
		{
			positionNames.Add(position.Name);
		}
		var ownKeys = new HashSet<string>(ideologyNames, StringComparer.OrdinalIgnoreCase) { "election", "duration", "valid_for_new_country" };
		ownKeys.UnionWith(positionNames);

		Sim.Modifiers.CModifierKeys kinds = CModifierReader.Kinds(directories);
		var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		int unreadable = 0;
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			var government = new CGovernment(block.Key, CModifierReader.ReadBlock(block, ownKeys, kinds, unknown, out int bad));
			unreadable += bad;
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is null || entry.Value is null)
				{
					continue;
				}
				bool yes = entry.Value.Equals("yes", StringComparison.OrdinalIgnoreCase);
				if (ideologyNames.Contains(entry.Key))
				{
					if (yes)
					{
						government.Accepted.Add(entry.Key);
					}
				}
				else if (positionNames.Contains(entry.Key))
				{
					if (yes)
					{
						government.Elected.Add(entry.Key);
					}
				}
				else if (entry.Key.Equals("election", StringComparison.OrdinalIgnoreCase))
				{
					government.HasElections = yes;
				}
				else if (entry.Key.Equals("valid_for_new_country", StringComparison.OrdinalIgnoreCase))
				{
					government.ValidForNewCountry = yes;
				}
				else if (entry.Key.Equals("duration", StringComparison.OrdinalIgnoreCase))
				{
					// Read whole, as the original reads it.
					government.Duration = int.TryParse(entry.Value, out int months) ? months : 0;
				}
			}
			governments[block.Key] = government;
		}
		if (unreadable > 0)
		{
			warn($"'{GovernmentsFile}': {unreadable} entries are neither an ideology, a post nor a number");
		}
		CModifierReader.ReportUnknown(warn, GovernmentsFile, "governments", unknown);
		return governments;
	}

	private static ClausewitzNode? Parse(CDirectorySettings directories, string gameRelative, Action<string> warn)
	{
		string path = directories.Resolve(gameRelative);
		try
		{
			return ClausewitzParser.ParseFile(path);
		}
		catch (IOException e)
		{
			warn($"Could not read '{path}': {e.Message}");
			return null;
		}
	}
}
