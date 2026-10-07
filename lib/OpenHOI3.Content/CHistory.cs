using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content;

/// <summary>
/// The grammar every history file shares: plain entries that hold before any date, and
/// blocks named for a date that hold from that date on.
///
/// The original keeps the dated entries as a log it can replay in either direction.
/// There is one start here, so the log is folded once and not kept. See
/// <c>docs/mechanics/map.md</c>, section 7.
/// </summary>
public static class CHistory
{
	/// <summary>
	/// The entries in force on a date, in the order they take effect: the undated ones
	/// as written, then the contents of every block dated on or before
	/// <paramref name="date"/>, earliest first. A block dated exactly on the date
	/// counts. Blocks sharing a date keep their order in the file.
	/// </summary>
	public static List<ClausewitzNode> Fold(ClausewitzNode root, CDate date)
	{
		var entries = new List<ClausewitzNode>();
		var dated = new List<(CDate When, int Order, ClausewitzNode Block)>();

		foreach (ClausewitzNode child in root.Children)
		{
			if (child.IsBlock && CDate.TryParse(child.Key, out CDate when))
			{
				if (when.CompareTo(date) <= 0)
				{
					dated.Add((when, dated.Count, child));
				}
				continue;
			}
			entries.Add(child);
		}

		dated.Sort((a, b) =>
		{
			int byDate = a.When.CompareTo(b.When);
			return byDate != 0 ? byDate : a.Order.CompareTo(b.Order);
		});
		foreach ((CDate _, int _, ClausewitzNode block) in dated)
		{
			entries.AddRange(block.Children);
		}
		return entries;
	}
}
