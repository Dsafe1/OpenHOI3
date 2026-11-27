using System;
using System.Collections.Generic;
using OpenHOI3.Content.Music;

namespace OpenHOI3.Interface.Music;

/// <summary>
/// Which song plays next, as the original picks it (<c>docs/mechanics/music.md</c>, section 2;
/// <c>reversing/findings/FINDINGS-music.md</c>, 2.2 and 2.3) - not a weighted draw but the highest weight: each song not
/// among the six chosen last weighs its chance times a roll of 5 to 9, and the first of the heaviest wins, a weight of
/// nought never. The six chosen last are this object's, and outlive a game as the original's outlive one.
/// </summary>
public sealed class CSongPicker
{
	/// <summary>How many of the songs chosen last may not be chosen next.</summary>
	public const int RecentCount = 6;

	/// <summary>The roll's least and its span: 5 to 9.</summary>
	public const int RollLeast = 5;
	public const int RollSpan = 5;

	/// <summary>The songs chosen last, oldest first, by their place in the list they were picked from.</summary>
	private readonly List<int> _recent = new();

	public IReadOnlyList<int> Recent => _recent;

	/// <summary>
	/// The index of the song to play, or -1 where none weighs anything. <paramref name="chance"/> answers a song's chance in
	/// thousandths, as the original's <c>GetDays</c>; <paramref name="draw"/> is the roll's source, a number never below
	/// nought - the original's is a generator nothing seeds, and never the game's (section 2.2).
	/// </summary>
	/// <param name="byWeight">
	/// An enhanced game's fix <c>weighted_songs</c>: one draw picks among the songs that may play, each as likely as its
	/// chance, where the original takes the heaviest of chance times a roll of five to nine - so that a factor of 2
	/// beats every factor of 1 outright.
	/// </param>
	public int Pick(IReadOnlyList<CSong> songs, Func<CSong, long> chance, Func<int> draw, bool byWeight = false)
	{
		int chosen = byWeight ? ByWeight(songs, chance, draw) : Heaviest(songs, chance, draw);
		// Whether or not a song was found, the oldest goes where the list is full; then the one chosen joins. A pass that
		// finds nothing so frees a place only with six held.
		if (_recent.Count > RecentCount - 1)
		{
			_recent.RemoveAt(0);
		}
		if (chosen >= 0)
		{
			_recent.Add(chosen);
		}
		return chosen;
	}

	/// <summary>The original's pick: a roll a song, the heaviest of chance times roll, the first of equals.</summary>
	private int Heaviest(IReadOnlyList<CSong> songs, Func<CSong, long> chance, Func<int> draw)
	{
		int chosen = -1;
		long best = 0;
		for (int index = 0; index < songs.Count; index++)
		{
			if (_recent.Contains(index))
			{
				continue;
			}
			long roll = draw() % RollSpan + RollLeast;
			long weight = roll * 1000L * chance(songs[index]) / 1000L;
			if (weight > best)
			{
				best = weight;
				chosen = index;
			}
		}
		return chosen;
	}

	/// <summary>
	/// The fix's pick: the songs not among the six chosen last, each weighing its chance where that is above nought, and
	/// one draw over their sum - the song whose running total first passes the draw's remainder. None where nothing weighs.
	/// </summary>
	private int ByWeight(IReadOnlyList<CSong> songs, Func<CSong, long> chance, Func<int> draw)
	{
		var weights = new long[songs.Count];
		long sum = 0;
		for (int index = 0; index < songs.Count; index++)
		{
			if (_recent.Contains(index))
			{
				continue;
			}
			weights[index] = Math.Max(0, chance(songs[index]));
			sum += weights[index];
		}
		if (sum <= 0)
		{
			return -1;
		}
		long point = draw() % sum;
		for (int index = 0; index < songs.Count; index++)
		{
			point -= weights[index];
			if (weights[index] > 0 && point < 0)
			{
				return index;
			}
		}
		return -1;
	}
}
