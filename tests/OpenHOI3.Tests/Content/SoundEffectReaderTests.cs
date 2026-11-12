using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Sound;

namespace OpenHOI3.Tests.Content;

/// <summary>The sounds of <c>interface/sound.sfx</c>: <c>docs/mechanics/sound.md</c>, section 2.</summary>
public class SoundEffectReaderTests
{
	private static (Dictionary<string, CSoundEffect> Sounds, List<string> Warnings) Read(string text)
	{
		var warnings = new List<string>();
		return (CSoundEffectReader.Read(ClausewitzParser.Parse(text), warnings.Add), warnings);
	}

	[Fact]
	public void ASoundWithOnlyItsFileHasTheDefaults()
	{
		(Dictionary<string, CSoundEffect> sounds, List<string> warnings) = Read("click = { file = \"click.wav\" }");

		CSoundEffect click = Assert.Single(sounds.Values);
		Assert.Equal("sound/click.wav", click.Path);
		Assert.Equal(100, click.Volume);
		Assert.Equal(-1, click.Pitch);
		Assert.Equal(0, click.Pan);
		Assert.False(click.Single);
		Assert.Empty(warnings);
	}

	[Fact]
	public void ANameIsLookedUpWithoutCaseAndTheFirstOfANameWins()
	{
		(Dictionary<string, CSoundEffect> sounds, List<string> warnings) =
			Read("Click = { file = \"first.wav\" } click = { file = \"second.wav\" }");

		Assert.Equal("sound/first.wav", sounds["CLICK"].Path);
		Assert.Single(warnings);
	}

	[Fact]
	public void PitchAndPanAreHeldInTheirRangesAndOnlyAnExactSingleIsOneVoice()
	{
		(Dictionary<string, CSoundEffect> sounds, _) = Read(
			"a = { file = a.wav pitch = 50 pan = 20000 voicetype = single volume = 40 }"
			+ " b = { file = b.wav pitch = 200000 pan = -20000 voicetype = Single }");

		Assert.Equal(100, sounds["a"].Pitch);
		Assert.Equal(10_000, sounds["a"].Pan);
		Assert.True(sounds["a"].Single);
		Assert.Equal(40, sounds["a"].Volume);
		Assert.Equal(100_000, sounds["b"].Pitch);
		Assert.Equal(-10_000, sounds["b"].Pan);
		Assert.False(sounds["b"].Single);
	}

	[Fact]
	public void AnUnknownKeyIsReportedAndTheSoundKept()
	{
		(Dictionary<string, CSoundEffect> sounds, List<string> warnings) = Read("a = { file = a.wav loop = yes }");

		Assert.True(sounds.ContainsKey("a"));
		Assert.Contains("loop", Assert.Single(warnings));
	}
}
