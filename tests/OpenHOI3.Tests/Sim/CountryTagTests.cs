
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Tests.Sim;

public class CountryTagTests
{
	[Theory]
	[InlineData("GER")]
	[InlineData("SOV")]
	[InlineData("U01")]
	public void AcceptsRealTags(string text)
	{
		Assert.True(CCountryTag.TryParse(text, out CCountryTag tag));
		Assert.Equal(text, tag.ToString());
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	[InlineData("GE")]
	[InlineData("GERM")]
	[InlineData("ger")]            // the files are consistently upper case
	[InlineData("G R")]
	public void RejectsAnythingElse(string? text)
	{
		Assert.False(CCountryTag.TryParse(text, out _));
	}
}
