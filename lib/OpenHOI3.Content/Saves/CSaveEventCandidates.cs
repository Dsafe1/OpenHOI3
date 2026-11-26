using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Rules;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A country's candidate events in an enhanced game's save, both ways (<c>docs/mechanics/savegame.md</c>, section
/// 21): one key of ours in the country's block, the events' numbers in the list's order, which is the order the day's
/// pass asks them in. The original's save keeps none and its load makes none, so a loaded game fires no event with a
/// mean time to happen until its month turns (<c>docs/mechanics/events.md</c>, section 1); with the list in the save
/// a loaded game asks what the saved one would have, from its first day. Written while the game has the fix
/// (<see cref="EFix.SavedEventCandidates"/>) and for a country that has any; read wherever a save has the key.
/// </summary>
public static class CSaveEventCandidates
{
	public const string Key = "openhoi_event_candidates";

	public static void Write(CSaveTextWriter writer, CGameState state, CCountry country)
	{
		if (!state.Rules.Has(EFix.SavedEventCandidates) || country.EventCandidates.Count == 0)
		{
			return;
		}
		writer.Numbers(Key, country.EventCandidates.Select(candidate => (long)candidate.Id));
	}

	/// <summary>
	/// A country's list as its block has it; a block without the key leaves the list as it is - empty, in a game just
	/// loaded. A number is the first country event of that number in the order the files were read, as a fired event's
	/// is found; one that is no event's is reported and left out.
	/// </summary>
	public static void Read(CGameState state, CCountry country, ClausewitzNode node, CSaveReport report)
	{
		if (node.Get(Key) is not { } list)
		{
			return;
		}
		var byNumber = new Dictionary<long, CEvent>();
		foreach (CEvent candidate in state.Events)
		{
			if (!candidate.IsProvinceEvent)
			{
				byNumber.TryAdd(candidate.Id, candidate);
			}
		}
		country.EventCandidates.Clear();
		foreach (ClausewitzNode value in list.Children)
		{
			long number = CSaveValues.Whole(value.Value);
			if (byNumber.TryGetValue(number, out CEvent? found))
			{
				country.EventCandidates.Add(found);
			}
			else
			{
				report.Unread(Key, $"an event of no number known, {number}");
			}
		}
	}
}
