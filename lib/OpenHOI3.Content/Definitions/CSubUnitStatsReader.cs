using System;
using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads a block of a brigade's figures - a unit type's own, or what a technology adds
/// to one: <c>soft_attack = 2.33</c> lines, and blocks such as
/// <c>hills = { attack = -0.10 movement = -0.10 }</c>. The two are written the same
/// way, which is why this is shared.
/// </summary>
public static class CSubUnitStatsReader
{
	/// <summary>What an adjuster may hold.</summary>
	private static readonly HashSet<string> AdjusterKinds = new(StringComparer.OrdinalIgnoreCase)
	{
		"attack", "defence", "movement", "attrition",
	};

	/// <summary>
	/// Fills <paramref name="stats"/> from a block. A line is taken when
	/// <paramref name="isStatistic"/> says its key is one, a block when
	/// <paramref name="isAdjuster"/> says so; the rest is left for the caller, who is
	/// told of each through <paramref name="other"/>.
	/// </summary>
	public static void Read(ClausewitzNode block, CSubUnitStats stats, Func<string, bool> isStatistic,
		Func<string, bool> isAdjuster, Action<ClausewitzNode> other, SortedSet<string> notNumbers)
	{
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is null)
			{
				continue;                                        // a stray word; nothing to hold it
			}
			if (!entry.IsBlock && isStatistic(entry.Key))
			{
				// Every digit after the point, as the original's unit loader reads a figure (units.md, section 2).
				if (CFixedPoint.TryParseAsUnitFigure(entry.Value, out CFixedPoint value))
				{
					// Two lines for one statistic: the later one stands.
					stats.Values.Set(entry.Key, value);
				}
				else
				{
					notNumbers.Add($"{block.Key}: {entry.Key} = {entry.Value}");
				}
			}
			else if (entry.IsBlock && isAdjuster(entry.Key))
			{
				if (!stats.Adjusters.TryGetValue(entry.Key, out CModifier? adjuster))
				{
					stats.Adjusters[entry.Key] = adjuster = new CModifier(entry.Key);
				}
				foreach (ClausewitzNode line in entry.Children)
				{
					if (line.Key is null || line.IsBlock)
					{
						continue;
					}
					if (!AdjusterKinds.Contains(line.Key) || !CFixedPoint.TryParse(line.Value, out CFixedPoint value))
					{
						notNumbers.Add($"{block.Key}: {entry.Key} {{ {line.Key} = {line.Value} }}");
						continue;
					}
					adjuster.Set(line.Key, value);
				}
			}
			else
			{
				other(entry);
			}
		}
	}
}
