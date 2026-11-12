using System.Linq;
using OpenHOI3.Interface;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// Sliders that hold the whole of what they divide between them, against
/// <c>docs/mechanics/sliders.md</c>, section 3.
/// </summary>
public class SliderGroupTests
{
	private static CFixed15 Share(string value)
	{
		return CFixed15.FromThousandths(CFixedPoint.Parse(value));
	}

	private static CSliderGroup Group(string total, params string[] shares)
	{
		var group = new CSliderGroup(shares.Length);
		group.Load(CFixedPoint.Parse(total), shares.Select(Share).ToArray());
		return group;
	}

	private static long Sum(CSliderGroup group)
	{
		long sum = 0;
		for (int index = 0; index < group.Count; index++)
		{
			sum += group[index].Raw;
		}
		return sum;
	}

	[Fact]
	public void ASliderOverItsLimitIsPutDownAndTheFreeOnesShareWhatItHeld()
	{
		// Six shares of 100 IC, lend-lease the last with 15.5 of them and a limit of nought: it gives nobody anything
		// (FINDINGS-lendleaseslider.md, section 3).
		CSliderGroup group = Group("100", "0.169", "0.169", "0.169", "0.169", "0.169", "0.155");
		group.SetLimit(5, CFixedPoint.Zero);

		Assert.True(group.ClampToLimits());
		Assert.Equal(0, group[5].Raw);
		Assert.Equal(100_000, Sum(group));
		// 15.5 shared equally among the five others, 3.1 each - to a thousandth or two, the shares being loaded through
		// 15-bit fractions.
		Assert.InRange(group[0].Raw, 19_998, 20_002);
		Assert.False(group.ClampToLimits());
	}

	[Fact]
	public void ASliderCannotBeDraggedPastItsLimitNorTakeMoreThanItInTheSharing()
	{
		CSliderGroup group = Group("100", "0.5", "0.5", "0");
		group.SetLimit(2, CFixedPoint.Parse("10"));

		group.Move(2, CFixedPoint.Parse("40"));
		Assert.Equal(10_000, group[2].Raw);
		Assert.Equal(100_000, Sum(group));

		// Lowering another frees IC: lend-lease, at its limit, takes none of it.
		group.Move(0, CFixedPoint.Parse("20"));
		Assert.Equal(10_000, group[2].Raw);
		Assert.Equal(100_000, Sum(group));
	}

	[Fact]
	public void ALockedSliderOverItsLimitIsLeftAsTheOriginalLeavesIt()
	{
		CSliderGroup group = Group("100", "0.8", "0.2");
		group.SetHeld(1, true);
		group.SetLimit(1, CFixedPoint.Zero);

		Assert.False(group.ClampToLimits());
		Assert.Equal(20_000, group[1].Raw);
	}

	[Fact]
	public void LessThanHalfAHundredthOverTheLimitIsLeftAsTheOriginalLeavesIt()
	{
		CSliderGroup group = Group("100", "1", "0");
		group.Move(1, new CFixedPoint(4));
		group.SetLimit(1, CFixedPoint.Zero);
		Assert.False(group.ClampToLimits());
		Assert.Equal(4, group[1].Raw);

		group.SetLimit(1, null);
		group.Move(1, new CFixedPoint(5));
		group.SetLimit(1, CFixedPoint.Zero);
		Assert.True(group.ClampToLimits());
		Assert.Equal(0, group[1].Raw);
	}

	[Fact]
	public void ASliderHoldsAnAmountAndNotAShare()
	{
		CSliderGroup group = Group("100", "0.25", "0.75");

		Assert.Equal(CFixedPoint.FromInt(25), group[0]);
		Assert.Equal(CFixedPoint.FromInt(75), group[1]);
	}

	[Fact]
	public void TheSlidersAlwaysHoldTheWholeBetweenThem()
	{
		// A third each does not come to the whole in a number cut off; the rest is handed round.
		CSliderGroup group = Group("100", "0.333", "0.333", "0.333");

		Assert.Equal(100000, Sum(group));
	}

	[Fact]
	public void MovingOneSharesTheDifferenceEquallyAmongTheOthers()
	{
		// Halves and quarters are shares fifteen binary places hold exactly; tenths are not.
		CSliderGroup group = Group("100", "0.5", "0.25", "0.25");

		group.Move(0, CFixedPoint.FromInt(60));

		Assert.Equal(CFixedPoint.FromInt(60), group[0]);
		Assert.Equal(CFixedPoint.FromInt(20), group[1]);
		Assert.Equal(CFixedPoint.FromInt(20), group[2]);
	}

	[Fact]
	public void ASliderThatCannotGiveItsPartLeavesTheRestToTheOthers()
	{
		CSliderGroup group = Group("100", "0.5", "0.125", "0.375");

		group.Move(0, CFixedPoint.FromInt(80));

		// Fifteen each was wanted; the second had only twelve and a half, so the third gives the rest.
		Assert.Equal(CFixedPoint.Zero, group[1]);
		Assert.Equal(CFixedPoint.FromInt(20), group[2]);
		Assert.Equal(100000, Sum(group));
	}

	[Fact]
	public void AHeldSliderStaysWhereItIs()
	{
		CSliderGroup group = Group("100", "0.5", "0.25", "0.25");
		group.SetHeld(1, true);

		group.Move(0, CFixedPoint.FromInt(60));

		Assert.Equal(CFixedPoint.FromInt(25), group[1]);
		Assert.Equal(CFixedPoint.FromInt(15), group[2]);
	}

	[Fact]
	public void WithNothingFreeToMakeRoomTheMovedSliderGivesWay()
	{
		CSliderGroup group = Group("100", "0.5", "0.25", "0.25");
		group.SetHeld(1, true);
		group.SetHeld(2, true);

		group.Move(0, CFixedPoint.FromInt(90));

		Assert.Equal(CFixedPoint.FromInt(50), group[0]);
	}

	[Fact]
	public void ASliderCannotGoBelowNothingOrAboveTheWhole()
	{
		CSliderGroup group = Group("100", "0.5", "0.5");

		group.Move(0, CFixedPoint.FromInt(250));
		Assert.Equal(CFixedPoint.FromInt(100), group[0]);
		Assert.Equal(CFixedPoint.Zero, group[1]);

		group.Move(0, CFixedPoint.FromInt(-5));
		Assert.Equal(CFixedPoint.Zero, group[0]);
		Assert.Equal(CFixedPoint.FromInt(100), group[1]);
	}

	[Fact]
	public void TheSharesNeverComeToMoreThanTheWhole()
	{
		CSliderGroup group = Group("129", "0.17", "0.15", "0.17", "0.17", "0.17", "0.17");
		group.Move(2, CFixedPoint.Parse("77.777"));

		CFixed15[] shares = group.Shares();

		Assert.True(CProduction.AreValidShares(shares));
		Assert.True(shares.Sum(share => share.Raw) > CFixed15.Scale - 10);
	}

	[Fact]
	public void WithNothingToDivideEveryShareIsNothing()
	{
		CSliderGroup group = Group("0", "0.5", "0.5");

		Assert.All(group.Shares(), share => Assert.Equal(CFixed15.Zero, share));
	}
}
