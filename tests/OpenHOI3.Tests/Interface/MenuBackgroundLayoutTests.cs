using OpenHOI3.Interface.Screens;
using Xunit;

namespace OpenHOI3.Tests.Interface;

/// <summary>The picture behind a country screen, against <c>docs/mechanics/countryviews.md</c>, section 6.</summary>
public class MenuBackgroundLayoutTests
{
	[Fact]
	public void It_takes_the_larger_of_the_two_ratios_so_that_it_covers_the_screen()
	{
		Assert.Equal(2f, CMenuBackgroundLayout.Scale(1920f, 1080f, 960f, 1024f));
		Assert.Equal(1.5f, CMenuBackgroundLayout.Scale(1280f, 1536f, 1024f, 1024f));
	}

	[Fact]
	public void A_picture_larger_than_the_screen_keeps_its_own_size()
	{
		Assert.Equal(1f, CMenuBackgroundLayout.Scale(800f, 600f, 2048f, 2048f));
	}
}
