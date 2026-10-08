using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

public class FixedPointTests
{
	[Theory]
	[InlineData("237964.731", 237964731)]
	[InlineData("22", 22000)]
	[InlineData("0.5", 500)]
	[InlineData(".5", 500)]
	[InlineData("5.", 5000)]
	[InlineData("1.25", 1250)]
	[InlineData(" 3.100 ", 3100)]
	[InlineData("+2.5", 2500)]
	[InlineData("0", 0)]
	public void ReadsDecimalsAsThousandths(string text, long raw)
	{
		Assert.True(CFixedPoint.TryParse(text, out CFixedPoint value));
		Assert.Equal(raw, value.Raw);
	}

	[Fact]
	public void ANegativeNumberIsNegativeWhateverItsWholePart()
	{
		// "Whole part times a thousand plus the fraction" gets this one wrong if done naively.
		Assert.Equal(-500, CFixedPoint.Parse("-0.5").Raw);
		Assert.Equal(-8000, CFixedPoint.Parse("-8").Raw);
		Assert.Equal(-1250, CFixedPoint.Parse("-1.25").Raw);
	}

	[Fact]
	public void DigitsPastTheThirdAreDroppedNotRounded()
	{
		Assert.Equal(83, CFixedPoint.Parse("0.0839").Raw);
		Assert.Equal(-83, CFixedPoint.Parse("-0.0839").Raw);
		Assert.Equal(1999, CFixedPoint.Parse("1.9999999").Raw);
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	[InlineData("-")]
	[InlineData(".")]
	[InlineData("yes")]
	[InlineData("1.2.3")]
	[InlineData("1,5")]
	[InlineData("12a")]
	[InlineData("99999999999999999999999")]
	public void RejectsWhatIsNotANumber(string? text)
	{
		Assert.False(CFixedPoint.TryParse(text, out _));
	}

	[Theory]
	[InlineData(237964731, "237964.731")]
	[InlineData(5000, "5.000")]
	[InlineData(7, "0.007")]
	[InlineData(-500, "-0.500")]
	[InlineData(-1250, "-1.250")]
	[InlineData(0, "0.000")]
	public void WritesThreeDecimalsAsTheSavegameDoes(long raw, string text)
	{
		Assert.Equal(text, new CFixedPoint(raw).ToString());
	}

	[Fact]
	public void AddsAndSubtractsExactly()
	{
		CFixedPoint a = CFixedPoint.Parse("0.1");
		CFixedPoint b = CFixedPoint.Parse("0.2");

		Assert.Equal(CFixedPoint.Parse("0.3"), a + b);       // the sum a double gets wrong
		Assert.Equal(CFixedPoint.Parse("-0.1"), a - b);
		Assert.Equal(CFixedPoint.Parse("-0.1"), -a);
	}

	[Fact]
	public void MultiplyingAndDividingTruncateTowardZero()
	{
		CFixedPoint third = CFixedPoint.One / CFixedPoint.FromInt(3);
		Assert.Equal(333, third.Raw);
		Assert.Equal(-333, (-CFixedPoint.One / CFixedPoint.FromInt(3)).Raw);

		// 0.333 * 0.333 is 0.110889, which becomes 0.110 and not 0.111.
		Assert.Equal(110, (third * third).Raw);
		Assert.Equal(-110, (third * -third).Raw);

		Assert.Equal(CFixedPoint.Parse("7.5"), CFixedPoint.Parse("2.5") * 3);
		Assert.Equal(833, (CFixedPoint.Parse("2.5") / 3).Raw);
	}

	[Fact]
	public void HasRoomForProductsTheOriginalsThirtyTwoBitsWouldNot()
	{
		CFixedPoint large = CFixedPoint.FromInt(2_000_000);
		Assert.Equal(CFixedPoint.FromInt(4_000_000_000_000), large * large);
	}

	[Fact]
	public void ComparesAndClamps()
	{
		CFixedPoint low = CFixedPoint.Parse("0.5");
		CFixedPoint high = CFixedPoint.FromInt(2);

		Assert.True(low < high);
		Assert.True(high >= CFixedPoint.FromInt(2));
		Assert.Equal(low, CFixedPoint.Min(low, high));
		Assert.Equal(high, CFixedPoint.Clamp(CFixedPoint.FromInt(9), low, high));
		Assert.Equal(CFixedPoint.Zero, CFixedPoint.Max(CFixedPoint.Zero, CFixedPoint.Parse("-0.083")));
		Assert.Equal(2, CFixedPoint.Parse("2.9").ToInt());
		Assert.Equal(-2, CFixedPoint.Parse("-2.9").ToInt());
	}
}
