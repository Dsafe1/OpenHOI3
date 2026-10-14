using System;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Politics;

/// <summary>
/// Elections: one is held each day it is due for a country whose government holds them - its last election and
/// its term past - and the most popular party wins it. See <c>docs/mechanics/politics.md</c>, sections 2 and 5.
/// </summary>
public static class CElections
{
	public const string ResultsMessage = "ELECTIONRESULTS";

	/// <summary>The term in months: the country's own, else its government's.</summary>
	public static int Term(CGameState state, CCountry country)
	{
		return country.ElectionDuration != 0 ? country.ElectionDuration : state.GovernmentOf(country)?.Duration ?? 0;
	}

	/// <summary>When the next election is due; today for a country that never held one.</summary>
	public static CDate Due(CGameState state, CCountry country)
	{
		return country.LastElection == CDate.Zero ? state.Date : country.LastElection.AddMonths(Term(state, country));
	}

	/// <summary>Each day, late in a country's day: an election where one is due.</summary>
	public static void RunDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (state.GovernmentOf(country) is { HasElections: true } && Due(state, country) <= state.Date)
			{
				Hold(state, country);
			}
		}
	}

	/// <summary>
	/// An election: each party scored by its popularity times a factor of one draw on the game's generator - with
	/// the shipped <c>ELECTION_RANDOMNESS</c> always 1000 - the highest winning, the first of equals, number 0
	/// skipped. The winner rules, the government's elected posts are filled again, the date is today and the seats
	/// are allotted; the player is told. Popularity is left as it was.
	/// </summary>
	public static void Hold(CGameState state, CCountry country)
	{
		CPartyFigures popularity = country.Popularity;
		if (popularity.Count == 0)
		{
			return;
		}
		long m = 100000 * state.Defines[CCabinet.Section, "ELECTION_RANDOMNESS"].Raw / 1000 / 1000;
		var scores = new long[popularity.Count];
		for (int i = 0; i < popularity.Count; i++)
		{
			int t = state.Random.Next();
			long factor = m != 0 ? Math.Max(1000 + 1000 * (t % m / 100), 0) : 1000;
			long score = popularity[i] * factor / 1000;
			if (score >= 0)
			{
				scores[i] = score;
			}
		}
		int winner = CPartyFigures.Leading(scores);
		if (winner == 0 || winner >= state.Ideologies.Count)
		{
			return;
		}
		string previous = country.RulingIdeology;
		country.RulingIdeology = state.Ideologies[winner].Name;
		CMinisterPosts.Fill(state, country, refillAll: true, electedOnly: true);
		country.LastElection = state.Date;
		CCabinet.Allot(state, country);
		if (state.Played.Contains(country.Tag))
		{
			state.RaisePactMessage(new Agreements.CPactMessage(ResultsMessage, "", "", country.Tag, country.Tag)
			{
				Told = country.Tag,
				Values = new[]
				{
					("PREVIOUS_PARTY", previous),
					("WINNING_PARTY", country.RulingIdeology),
					("ELECTION_YEAR", state.Date.Year.ToString(System.Globalization.CultureInfo.InvariantCulture)),
				},
			});
		}
	}

	/// <summary>
	/// A new game's dates: a last election before today moved forward by whole terms to the last one on or before
	/// today, and a country with none given today.
	/// </summary>
	public static void SetUpAtStart(CGameState state, CCountry country)
	{
		int term = Term(state, country);
		if (country.LastElection != CDate.Zero && country.LastElection < state.Date && term > 0)
		{
			while (country.LastElection.AddMonths(term) <= state.Date)
			{
				country.LastElection = country.LastElection.AddMonths(term);
			}
		}
		if (country.LastElection == CDate.Zero)
		{
			country.LastElection = state.Date;
		}
	}
}
