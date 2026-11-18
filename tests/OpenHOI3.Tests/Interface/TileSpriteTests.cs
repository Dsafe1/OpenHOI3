using OpenHOI3.Interface.Text;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// A <c>tileSpriteType</c>'s repeats: the declared size over the file's, in integer division, against
/// <c>docs/mechanics/interface.md</c>, section 5.
/// </summary>
public class TileSpriteTests
{
	[Fact]
	public void TheBaseGamesStripRepeatsAcrossAtItsOwnScale()
	{
		// GFX_topcolor_*: a 64 by 512 file declared 2048 by 512.
		Assert.Equal((32, 1), CTileSprite.Repeats(2048, 512, 64, 512));
	}

	[Fact]
	public void BlackIcesStripRepeatsOnceDownAndIsStretched()
	{
		// BlackICE declares 2048 by 1000: 1000 / 512 is 1, so one copy fills the 1000.
		Assert.Equal((32, 1), CTileSprite.Repeats(2048, 1000, 64, 512));
	}

	[Fact]
	public void TheDivisionIsCutNotRounded()
	{
		Assert.Equal((2, 1), CTileSprite.Repeats(191, 1023, 64, 512));
		Assert.Equal((0, 0), CTileSprite.Repeats(63, 511, 64, 512));
	}
}
