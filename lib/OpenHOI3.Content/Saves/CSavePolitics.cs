using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A country's politics and its research queue, both ways: the technologies it researches in order, its last election
/// and its own term, its last surrender, the spies it has caught, each party's organisation and popularity, its
/// government, the seats of its cabinet and the pool of ministers it may appoint. See <c>docs/mechanics/savegame.md</c>,
/// section 12.
/// </summary>
public static class CSavePolitics
{
	/// <summary>The keys of a country block this reads.</summary>
	public static readonly string[] Keys =
	{
		"research", "last_election", "last_surrender", "election", "duration", "spiescaught", "organization", "popularity",
		"government", "cabinet", "ministers",
	};

	public static void Read(CGameState state, CCountry country, ClausewitzNode node, CSaveReport report)
	{
		// The queue in its order; a technology of no name known is left out.
		country.ResearchQueue.Clear();
		foreach (ClausewitzNode entry in node.All("research"))
		{
			if (entry.Value is { } name && state.Technologies.ContainsKey(name))
			{
				country.ResearchQueue.Add(name);
			}
			else
			{
				report.Unread("research", $"a technology of no name known, {entry.Value}");
			}
		}

		// The null date, or none, is never.
		country.LastElection = CSaveValues.HoursOrNull(node.Get("last_election")?.Value) is { } elected
			? CDate.FromDayNumber((int)(elected / CGameState.HoursPerDay))
			: CDate.Zero;
		country.LastSurrender = CSaveValues.HoursOrNull(node.Get("last_surrender")?.Value) ?? long.MinValue / 2;
		// election is read by nothing in the original, and is always yes.
		country.ElectionDuration = (int)CSaveValues.Whole(node, "duration");
		country.SpiesCaught = (int)CSaveValues.Whole(node, "spiescaught");

		// The new game's setting-up is not run on a load, so the figures are made here at the ideologies' count, each
		// the save's by its number and a number past the save's nought.
		int count = state.Ideologies.Count;
		country.Organisation = ReadParties(node.Get("organization"), count);
		country.Popularity = ReadParties(node.Get("popularity"), count);
		// The seats are allotted again from popularity once the save is read, as the original's AfterLoad does; the
		// save's own list, every figure of it, is kept behind them, as the original appends it (inferred).
		country.CabinetLoaded = (node.Get("cabinet")?.Children ?? new List<ClausewitzNode>())
			.Select(seats => (int)CSaveValues.Whole(seats.Value)).ToArray();
		country.GovernmentType = node.Get("government")?.Value ?? "";

		// The pool, in its order: each a minister of the country's file by the number it gives him.
		country.PossibleMinisters.Clear();
		foreach (ClausewitzNode id in node.Get("ministers")?.Children ?? new List<ClausewitzNode>())
		{
			if (country.Ministers.TryGetValue((int)CSaveValues.Whole(id.Value), out CMinister? minister))
			{
				country.PossibleMinisters.Add(minister);
			}
			else
			{
				report.Unread("ministers", $"a minister of no number the country's file gives, {id.Value}");
			}
		}
	}

	/// <summary><c>organization = { data = { figures } }</c>, one figure an ideology by its number.</summary>
	private static CPartyFigures ReadParties(ClausewitzNode? node, int count)
	{
		var figures = new CPartyFigures(count);
		List<ClausewitzNode> values = node?.Get("data")?.Children ?? new List<ClausewitzNode>();
		for (int i = 0; i < count && i < values.Count; i++)
		{
			figures.Values[i] = CSaveValues.Fixed(values[i].Value).Raw;
		}
		return figures;
	}

	/// <summary>One <c>research</c> a technology in the queue, in its order, after the categories.</summary>
	public static void WriteResearch(CSaveTextWriter writer, CCountry country)
	{
		foreach (string technology in country.ResearchQueue)
		{
			writer.Word("research", technology);
		}
	}

	/// <summary>
	/// The last election, the last surrender, <c>election</c> - always yes, as nothing in the original sets it - and
	/// the country's own term.
	/// </summary>
	public static void WriteDates(CSaveTextWriter writer, CCountry country)
	{
		writer.DateOrNull("last_election", country.LastElection == CDate.Zero ? null
			: (long)country.LastElection.DayNumber * CGameState.HoursPerDay);
		writer.DateOrNull("last_surrender", country.LastSurrender < 0 ? null : country.LastSurrender);
		writer.Bool("election", true);
		writer.Int("duration", country.ElectionDuration);
	}

	/// <summary>The two party figures, each key at column 0 as the original writes them, its list of figures below.</summary>
	public static void WriteParties(CSaveTextWriter writer, CCountry country)
	{
		WriteParties(writer, "organization", country.Organisation);
		WriteParties(writer, "popularity", country.Popularity);
	}

	private static void WriteParties(CSaveTextWriter writer, string key, CPartyFigures figures)
	{
		writer.KeyAtColumnZero(key);
		writer.OpenBrace();
		writer.Figures("data", figures.Values.Select(value => new CFixedPoint(value)));
		writer.End();
	}

	/// <summary>The government, where the country has one.</summary>
	public static void WriteGovernment(CSaveTextWriter writer, CCountry country)
	{
		if (country.GovernmentType.Length > 0)
		{
			writer.Word("government", country.GovernmentType);
		}
	}

	/// <summary><c>ministers={1 2 3 }</c> on one line, each number followed by a space: the pool in its order.</summary>
	public static void WriteMinisters(CSaveTextWriter writer, CCountry country)
	{
		writer.Key("ministers");
		writer.Line("{" + string.Concat(country.PossibleMinisters.Select(minister =>
			minister.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ")) + "}");
	}

	/// <summary>Each party's seats, by its ideology's number, and after them the cabinet a loaded save gave.</summary>
	public static void WriteCabinet(CSaveTextWriter writer, CCountry country)
	{
		writer.Numbers("cabinet", country.Cabinet.Concat(country.CabinetLoaded).Select(seats => (long)seats));
	}
}
