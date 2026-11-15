using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.History;

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
	/// The original's last date of its first pass over the history: everything undated and everything dated up to it is
	/// applied at once, before the rest (<c>reversing/findings/FINDINGS-bookmarks.md</c>, section 2.1).
	/// </summary>
	public static readonly CDate FirstPassEnd = new(2, 1, 1);

	/// <summary>
	/// The entries in force on a date, in the order they take effect: the undated ones
	/// as written, then the contents of every block dated on or before
	/// <paramref name="date"/>, earliest first. A block dated exactly on the date
	/// counts. Blocks sharing a date keep their order in the file.
	/// </summary>
	public static List<ClausewitzNode> Fold(ClausewitzNode root, CDate date) => FoldBetween(root, null, date);

	/// <summary>
	/// The entries in force on a date, as <see cref="Fold"/> gives them, each marked whether the bookmark's own pass applies
	/// it - dated after the defines' start date - where the passes to that date come first
	/// (<c>reversing/findings/FINDINGS-bookmarks.md</c>, section 2.1; <c>docs/mechanics/bookmarks.md</c>, section 3).
	/// </summary>
	public static IEnumerable<(ClausewitzNode Entry, bool Late)> FoldInPasses(ClausewitzNode root, CDate definesStart, CDate date)
	{
		CDate early = definesStart.CompareTo(date) < 0 ? definesStart : date;
		foreach (ClausewitzNode entry in FoldBetween(root, null, early))
		{
			yield return (entry, false);
		}
		if (definesStart.CompareTo(date) < 0)
		{
			foreach (ClausewitzNode entry in FoldBetween(root, definesStart, date))
			{
				yield return (entry, true);
			}
		}
	}

	/// <summary>
	/// The entries one pass of the original's replay applies: those of every block dated after <paramref name="after"/> and
	/// on or before <paramref name="until"/>, earliest first, blocks sharing a date in the file's order - and, with no
	/// <paramref name="after"/>, the undated ones as written before them. The original replays the history from nothing to
	/// <see cref="FirstPassEnd"/>, from there to the defines' start date, and as a bookmark is chosen on to its date
	/// (<c>reversing/findings/FINDINGS-bookmarks.md</c>, sections 1.3 and 2.1).
	/// </summary>
	public static List<ClausewitzNode> FoldBetween(ClausewitzNode root, CDate? after, CDate until)
	{
		var entries = new List<ClausewitzNode>();
		var dated = new List<(CDate When, int Order, ClausewitzNode Block)>();

		foreach (ClausewitzNode child in root.Children)
		{
			if (child.IsBlock && CDate.TryParse(child.Key, out CDate when))
			{
				if (when.CompareTo(until) <= 0 && (after is not { } from || when.CompareTo(from) > 0))
				{
					dated.Add((when, dated.Count, child));
				}
				continue;
			}
			if (after is null)
			{
				entries.Add(child);
			}
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
