using OpenHOI3.Interface;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

/// <summary>A number as the interface writes one, against <c>docs/mechanics/interface.md</c>, section 6.</summary>
public class NumberTextTests
{
	[Theory]
	[InlineData("115.999", 0, "115")]                                // cut off, not rounded
	[InlineData("0.999", 0, "0")]
	[InlineData("0.129", 2, "0.12")]
	[InlineData("1.250", 1, "1.2")]
	[InlineData("3.000", 2, "3.00")]
	[InlineData("3.005", 3, "3.005")]
	[InlineData("237964.731", 0, "237964")]                          // nothing between the thousands
	public void CutsToTheDecimalsAskedFor(string value, int decimals, string expected)
	{
		Assert.Equal(expected, CNumberText.Format(CFixedPoint.Parse(value), decimals));
	}

	[Fact]
	public void ANegativeFigureKeepsItsSignUnlessItCutsDownToNothing()
	{
		Assert.Equal("-2", CNumberText.Format(CFixedPoint.Parse("-2.900"), 0));
		Assert.Equal("-0.5", CNumberText.Format(CFixedPoint.Parse("-0.500"), 1));
		Assert.Equal("0", CNumberText.Format(CFixedPoint.Parse("-0.500"), 0));
	}
}
