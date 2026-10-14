using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Politics;

/// <summary>
/// The cabinet's seats: shared out between the parties by popularity, and whether a party of another group
/// than the ruling party's holds one. See <c>docs/mechanics/politics.md</c>, section 4.
/// </summary>
public static class CCabinet
{
	public const string Section = "country";

	/// <summary>The modifier a fractured government gets, step 18 of a country's modifier.</summary>
	public const string Fractured = "fractured_government";

	/// <summary>
	/// The seats allotted afresh: on a copy of popularity normalised to one, every party under
	/// <c>PARTY_POPULARITY_CUTOFF</c> set to nought, normalised again, each party's share of the changeable posts
	/// rounded to the nearest, ties to even, and handed out greatest first - equals in their order - while seats
	/// are left. What the rounding leaves over goes to nobody.
	/// </summary>
	public static void Allot(CGameState state, CCountry country)
	{
		int count = country.Popularity.Count;
		if (country.Cabinet.Length != count)
		{
			country.Cabinet = new int[count];
		}
		Array.Clear(country.Cabinet);
		long[] shares = (long[])country.Popularity.Values.Clone();
		CPartyFigures.NormaliseToOne(shares);
		long cutoff = state.Defines[Section, "PARTY_POPULARITY_CUTOFF"].Raw;
		for (int i = 1; i < count; i++)
		{
			if (shares[i] < cutoff)
			{
				shares[i] = 0;
			}
		}
		CPartyFigures.NormaliseToOne(shares);
		int seats = state.GovernmentPositions.Count(position => position.Changeable);

		var wanted = new List<(int Ideology, int Seats)>();
		for (int i = 1; i < count; i++)
		{
			long q = shares[i] * (seats * 1000L) / 1000;
			wanted.Add((i, (int)MathF.Round(q / 1000f, MidpointRounding.ToEven)));
		}
		int left = seats;
		// Greatest first, equals in their order: the runtime's insertion sort for so short a list.
		foreach ((int ideology, int n) in wanted.OrderByDescending(pair => pair.Seats))
		{
			int given = Math.Min(n, left);
			country.Cabinet[ideology] = given;
			left -= given;
		}
	}

	/// <summary>Each day, a country whose ruling party holds no seat has its seats allotted again.</summary>
	public static void RunDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (state.Ideology(country.RulingIdeology) is { } ruling && Seats(country, ruling.Index) == 0)
			{
				Allot(state, country);
			}
		}
	}

	/// <summary>The seats a party holds.</summary>
	public static int Seats(CCountry country, int ideology)
	{
		return ideology >= 0 && ideology < country.Cabinet.Length ? country.Cabinet[ideology] : 0;
	}

	/// <summary>Whether a party of another ideology group than the ruling party's holds a seat.</summary>
	public static bool IsFractured(CGameState state, CCountry country)
	{
		if (state.Ideology(country.RulingIdeology) is not { } ruling)
		{
			return false;
		}
		for (int i = 1; i < country.Cabinet.Length && i < state.Ideologies.Count; i++)
		{
			if (country.Cabinet[i] > 0 && state.Ideologies[i].Group != ruling.Group)
			{
				return true;
			}
		}
		return false;
	}
}
