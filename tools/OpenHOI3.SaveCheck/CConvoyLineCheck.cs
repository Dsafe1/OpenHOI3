using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;

/// <summary>
/// The convoy lines of the build queue beside the save's: <c>docs/mechanics/convoylines.md</c>, sections 2 and 3.
///
/// Every line of the earlier save is priced as an order would price it under the technology the save shows, and set
/// beside what the save keeps. Then each line paid in full in both saves is worked for the days between them, paid in
/// full every day, and what is left of it - or that nothing is - set beside the later save, with the ships its finished
/// items gave. A line's second <c>status</c> says escorts (<c>yes</c>) or transports (<c>no</c>); its first is the day's
/// share.
/// </summary>
internal static class CConvoyLineCheck
{
	private const string Block = "convoy_construction";

	public static void Compare(CGameState state, IReadOnlyList<(CCountry Country, ClausewitzNode Earlier, ClausewitzNode Later)> pairs,
		int days, Action<string, bool, Func<string>> compare)
	{
		// A line keeps its number only within one session of the game: two saves with a load between them share none,
		// and their lines are priced and not worked forward.
		bool oneSession = pairs.Any(pair => Lines(pair.Earlier).Select(Id).Intersect(Lines(pair.Later).Select(Id)).Any());
		if (!oneSession)
		{
			Console.WriteLine("convoy lines: the two saves share no line's number - a game loaded between them - so none is worked forward");
		}
		foreach ((CCountry country, ClausewitzNode before, ClausewitzNode after) in pairs)
		{
			Dictionary<int, ClausewitzNode> later = Lines(after).ToDictionary(line => Id(line));
			foreach (ClausewitzNode node in Lines(before))
			{
				List<string> statuses = node.All("status").Select(status => status.Value ?? "").ToList();
				bool escorts = statuses.Count > 1 && statuses[^1].Equals("yes", StringComparison.OrdinalIgnoreCase);
				string who = $"{country.Tag.Value} {(escorts ? "escorts" : "transports")} {Id(node)}";

				CConstruction ours = CConvoyLines.NewLine(state, country, escorts, node.GetInt("size"));
				CFixedPoint cost = Number(node.GetString("cost"));
				CFixedPoint duration = Number(node.GetString("duration"));
				compare("convoy lines: what an item costs a day", cost == ours.Cost, () => $"{who}: save {cost}, ours {ours.Cost}");
				compare("convoy lines: the days an item takes", duration == ours.Duration,
					() => $"{who}: save {duration}, ours {ours.Duration}");

				// Worked forward only where the line was paid in full in both saves.
				later.TryGetValue(Id(node), out ClausewitzNode? then);
				if (!oneSession || Number(statuses.FirstOrDefault()) != CFixedPoint.One
					|| (then is not null && Number(then.GetString("status")) != CFixedPoint.One))
				{
					continue;
				}
				var line = new CConstruction(country.Tag)
				{
					IsConvoy = true, IsEscorts = escorts, Size = node.GetInt("size"), Cost = cost, Duration = duration,
					Progress = Number(node.GetString("progress")),
				};
				int ships = 0;
				bool done = false;
				for (int day = 0; day < days && !done; day++)
				{
					CBuildQueue.AddProgress(country, line, CFixedPoint.Scale);
					if (line.Progress >= line.Duration)
					{
						(int transports, int escortsHeld) = (country.Transports, country.Escorts);
						done = CBuildQueue.Deliver(state, country, line);
						ships += country.Transports - transports + country.Escorts - escortsHeld;
						(country.Transports, country.Escorts) = (transports, escortsHeld);
					}
				}
				compare("convoy lines: a line worked between the saves is finished, or as far on, as the later save has it",
					done ? then is null : then is not null && then.GetInt("size") == line.Size && Number(then.GetString("progress")) == line.Progress,
					() => $"{who}: ours {(done ? "finished" : $"size {line.Size} at {line.Progress}")}, the later save "
						+ (then is null ? "has no such line" : $"size {then.GetString("size")} at {then.GetString("progress")}")
						+ $"  (gave {ships} ships)");
			}
		}
	}

	private static IEnumerable<ClausewitzNode> Lines(ClausewitzNode country)
	{
		return country.Children.Where(child => child.IsBlock && Block.Equals(child.Key, StringComparison.OrdinalIgnoreCase));
	}

	private static int Id(ClausewitzNode line)
	{
		return line.Get("id")?.GetInt("id") ?? 0;
	}

	private static CFixedPoint Number(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}
}
