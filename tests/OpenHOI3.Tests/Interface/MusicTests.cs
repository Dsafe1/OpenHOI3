using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Music;
using OpenHOI3.Interface.Music;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Tests.Interface;

/// <summary>Which song plays next and how loud: <c>docs/mechanics/music.md</c>, sections 2 and 5.</summary>
public class MusicTests
{
	private static List<CSong> Songs(int count)
	{
		return Enumerable.Range(0, count).Select(i => new CSong($"s{i}.mp3", new CMeanTimeToHappen(), "music/test.txt", i)).ToList();
	}

	/// <summary>A source of draws that answers the given numbers in turn, again and again.</summary>
	private static Func<int> Draws(params int[] draws)
	{
		int next = 0;
		return () => draws[next++ % draws.Length];
	}

	[Fact]
	public void TheHeaviestWinsAndNotADraw()
	{
		List<CSong> songs = Songs(3);
		// Rolls 5, 9 and 6, every chance 1: the second weighs most.
		Assert.Equal(1, new CSongPicker().Pick(songs, _ => 1000, Draws(0, 4, 1)));
	}

	[Fact]
	public void EqualWeightsGoToTheEarlierSong()
	{
		Assert.Equal(0, new CSongPicker().Pick(Songs(3), _ => 1000, Draws(2)));
	}

	[Fact]
	public void AFactorOfTwoBeatsEveryFactorOneWhateverTheRolls()
	{
		List<CSong> songs = Songs(3);
		// The third has twice the chance and the least roll, 10 against the others' 9.
		Assert.Equal(2, new CSongPicker().Pick(songs, song => song == songs[2] ? 2000 : 1000, Draws(4, 4, 0)));
	}

	[Fact]
	public void ASongWeighingNothingIsNeverChosen()
	{
		Assert.Equal(-1, new CSongPicker().Pick(Songs(2), _ => 0, Draws(4)));
	}

	[Fact]
	public void TheSixChosenLastAreLeftOut()
	{
		List<CSong> songs = Songs(8);
		var picker = new CSongPicker();
		var chosen = new List<int>();
		for (int pick = 0; pick < 8; pick++)
		{
			chosen.Add(picker.Pick(songs, _ => 1000, Draws(0)));
		}

		// Equal weights take the first not left out: 0 to 5, then 6 - and the seventh pick lets 0 go, so the eighth takes it.
		Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 0 }, chosen);
		Assert.Equal(new[] { 2, 3, 4, 5, 6, 0 }, picker.Recent);
		Assert.Equal(1, picker.Pick(songs, _ => 1000, Draws(0)));
	}

	[Fact]
	public void FiveOrFewerSongsFallSilentOnceEachHasPlayed()
	{
		List<CSong> songs = Songs(5);
		var picker = new CSongPicker();
		for (int pick = 0; pick < 5; pick++)
		{
			Assert.Equal(pick, picker.Pick(songs, _ => 1000, Draws(0)));
		}

		// Five held, none free: nothing found, and nothing leaves the list, so it stays so.
		Assert.Equal(-1, picker.Pick(songs, _ => 1000, Draws(0)));
		Assert.Equal(-1, picker.Pick(songs, _ => 1000, Draws(0)));
		Assert.Equal(5, picker.Recent.Count);
	}

	[Fact]
	public void TheLoudnessIsTheOriginalsCurve()
	{
		Assert.Equal(0f, CMusicTracks.Decibels(100f, 100f));
		Assert.Equal(-15.91f, CMusicTracks.Decibels(100f, 50f), 2);
		Assert.Equal(-15.91f, CMusicTracks.Decibels(50f, 100f), 2);
		Assert.Equal(-100f, CMusicTracks.Decibels(0f, 100f));
	}

	[Fact]
	public void TheFallbackLeavesOutTheThreeNamedTracks()
	{
		Assert.False(CMusicTracks.IsFallbackTrack(CMusicTracks.Title));
		Assert.False(CMusicTracks.IsFallbackTrack(CMusicTracks.Victory));
		Assert.False(CMusicTracks.IsFallbackTrack(CMusicTracks.Defeat));
		Assert.True(CMusicTracks.IsFallbackTrack("music/bigarmy.mp3"));
	}
}
