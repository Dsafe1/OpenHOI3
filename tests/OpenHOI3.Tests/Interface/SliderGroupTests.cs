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
