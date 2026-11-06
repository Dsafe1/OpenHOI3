using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A country's spies, both ways: its free spies, and for every country its presence there - the priority, the level,
/// the hour its missions last changed, the nine missions' weights and the covert-ops points - each a list with an entry
/// for the original's null country, <c>---</c>, first and then one a country in <c>common/countries.txt</c>'s order. See
/// <c>docs/mechanics/savegame.md</c>, section 13.
/// </summary>
public static class CSaveEspionage
{
	/// <summary>The keys of a country block this reads.</summary>
	public static readonly string[] Keys =
	{
		"spies", "spy_priority", "spy_allocation", "spy_date", "spy_mission", "covert_ops_points",
	};

	public static void Read(CGameState state, CCountry country, ClausewitzNode node)
	{
		country.FreeSpies = CSaveValues.Fixed(node, "spies");
		List<string?> priorities = Values(node, "spy_priority");
		List<string?> levels = Values(node, "spy_allocation");
		List<string?> dates = Values(node, "spy_date");
		List<string?> missions = Values(node, "spy_mission");
		List<string?> points = Values(node, "covert_ops_points");
		foreach (CCountry other in state.Countries)
		{
			// The placeholder's entry first, so a country's place is its index plus one; what a list leaves out is nought.
			int at = other.Index + 1;
			CSpyPresence presence = CGameState.Presence(country, other);
			presence.Priority = (int)CSaveValues.Whole(At(priorities, at));
			presence.Level = CSaveValues.Fixed(At(levels, at)).Raw;
			presence.LastMissionChange = CSaveValues.HoursOrNull(At(dates, at));
			for (int mission = 0; mission < CSpyPresence.MissionCount; mission++)
			{
				presence.SetMission(mission, (int)CSaveValues.Whole(At(missions, at * CSpyPresence.MissionCount + mission)));
			}
			presence.CovertOpsPoints = CSaveValues.Fixed(At(points, at)).Raw;
		}
	}

	private static List<string?> Values(ClausewitzNode node, string key)
	{
		return (node.Get(key)?.Children ?? new List<ClausewitzNode>()).Select(value => value.Value).ToList();
	}

	private static string? At(List<string?> values, int index)
	{
		return index < values.Count ? values[index] : null;
	}

	/// <summary>The free spies and the five lists, after the convoys; the placeholder's entry nought, its date the null date.</summary>
	public static void Write(CSaveTextWriter writer, CGameState state, CCountry country)
	{
		writer.Fixed("spies", country.FreeSpies);
		IReadOnlyList<CCountry> countries = state.Countries;
		CSpyPresence Of(CCountry other) => CGameState.Presence(country, other);
		writer.Numbers("spy_priority", countries.Select(other => (long)Of(other).Priority).Prepend(0));
		writer.Figures("spy_allocation", countries.Select(other => new CFixedPoint(Of(other).Level)).Prepend(CFixedPoint.Zero));
		writer.Words("spy_date", countries.Select(other => Of(other).LastMissionChange is { } hours
			? CSaveValues.HoursText(hours) : CSaveValues.NullDate).Prepend(CSaveValues.NullDate));
		writer.Numbers("spy_mission", Enumerable.Repeat(0L, CSpyPresence.MissionCount)
			.Concat(countries.SelectMany(other => Of(other).Missions.Select(weight => (long)weight))));
		writer.Figures("covert_ops_points", countries.Select(other => new CFixedPoint(Of(other).CovertOpsPoints))
			.Prepend(CFixedPoint.Zero));
	}
}
