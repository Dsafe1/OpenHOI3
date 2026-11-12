using System.Collections.Generic;
using OpenHOI3.Content.Music;
using OpenHOI3.Content.Script;

namespace OpenHOI3.Tests.Content;

/// <summary>The songs of <c>music/*.txt</c>: <c>docs/mechanics/music.md</c>, section 2.1.</summary>
public class SongReaderTests
{
	private static (List<CSong> Songs, CScriptContext Context) Read(string text)
	{
		CScriptContext context = ScriptFixture.Context();
		var songs = new List<CSong>();
		CSongReader.ReadFile(ScriptFixture.Parse(text, context), songs, context);
		return (songs, context);
	}

	[Fact]
	public void ASongWithNoChanceHasADayOfOne()
	{
		(List<CSong> songs, _) = Read("song = { name = \"plain.mp3\" }");

		CSong song = Assert.Single(songs);
		Assert.Equal("plain.mp3", song.Name);
		Assert.Equal("music/plain.mp3", song.Path);
		Assert.Equal(1, song.Chance.Days);
		Assert.Empty(song.Chance.Modifiers);
	}

	[Fact]
	public void TheChanceIsAMeanTimeToHappenWithItsModifiers()
	{
		(List<CSong> songs, CScriptContext context) = Read(
			"song = { name = \"a.mp3\" chance = { modifier = { factor = 2 GER = { faction = axis } } modifier = { factor = 0 ENG = { faction = axis } } } }");

		CSong song = Assert.Single(songs);
		Assert.Equal(1, song.Chance.Days);
		Assert.Equal(2, song.Chance.Modifiers.Count);
		Assert.Equal(2000, song.Chance.Modifiers[0].Factor.Raw);
		Assert.Equal(0, song.Chance.Modifiers[1].Factor.Raw);
		Assert.Empty(context.Problems.All);
	}

	[Fact]
	public void SongsKeepTheirOrderAndOtherKeysAreReported()
	{
		(List<CSong> songs, CScriptContext context) = Read(
			"song = { name = \"first.mp3\" } playlist = { } song = { name = \"second.mp3\" tempo = 3 }");

		Assert.Equal(new[] { "first.mp3", "second.mp3" }, songs.ConvertAll(song => song.Name));
		Assert.Equal(2, ScriptFixture.Of(context, EScriptProblem.UnknownKey).Length);
	}
}
