using OpenHOI3.Interface;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

/// <summary>
/// What a slider writes beside itself, against <c>docs/mechanics/sliders.md</c>,
/// section 2. The words of <c>SLIDER_NEED</c> are the original's own English.
/// </summary>
public class SliderTextTests
{
	private static string Localise(string key)
	{
		return key == CSliderText.NeedKey ? "Need: " : key;
	}

	[Fact]
	public void TheAmountIsWrittenToTwoDecimalsInWhiteWhenItCoversTheNeed()
	{
		Assert.Equal("§W21.92", CSliderText.Value(CFixedPoint.Parse("21.924"), CFixedPoint.Zero));
		Assert.Equal("§W19.35", CSliderText.Value(CFixedPoint.Parse("19.35"), CFixedPoint.Parse("19.35")));
	}

	[Fact]
	public void AShareSetAtItsNeedIsNotRedForTheHairItLostBeingStored()
	{
		// 7.5% of 92 IC is 6.9; the share as it is stored gives 6.898.
		Assert.Equal("§W6.90", CSliderText.Value(CFixedPoint.Parse("6.898"), CFixedPoint.Parse("6.9")));
	}

	[Fact]
	public void TheAmountIsRedWhenTheNeedIsGreater()
	{
		Assert.Equal("§R19.34", CSliderText.Value(CFixedPoint.Parse("19.34"), CFixedPoint.Parse("19.35")));
	}

	[Fact]
	public void TheyAreComparedInHundredthsAsTheyAreWritten()
	{
		// A need of 19.359 is written 19.35, and 19.351 is written 19.35: not red.
		Assert.Equal("§W19.35", CSliderText.Value(CFixedPoint.Parse("19.351"), CFixedPoint.Parse("19.359")));
	}

	[Fact]
	public void ASliderWithNoNeedOfItsOwnHasNoColour()
	{
		Assert.Equal("6.83", CSliderText.Value(CFixedPoint.Parse("6.827")));
	}

	[Fact]
	public void TheNeedLineIsTheWordsAndTheNeedToTwoDecimals()
	{
		Assert.Equal("Need: 19.35", CSliderText.Need(Localise, CFixedPoint.Parse("19.35")));
		Assert.Equal("Need: 7.00", CSliderText.Need(Localise, CFixedPoint.FromInt(7)));
	}

	[Theory]
	[InlineData(EProductionCategory.LendLease, "DISTRIBUTE_LENDLEASE")]
	[InlineData(EProductionCategory.ConsumerGoods, "DISTRIBUTE_CONSUMER_GOODS")]
	[InlineData(EProductionCategory.Production, "DISTRIBUTE_PRODUCTION")]
	[InlineData(EProductionCategory.Supplies, "DISTRIBUTE_SUPPLY")]
	[InlineData(EProductionCategory.Reinforcement, "DISTRIBUTE_REINFORCE")]
	[InlineData(EProductionCategory.Upgrades, "DISTRIBUTE_UPGRADE")]
	public void EachIcSliderIsTitledByItsOwnKey(EProductionCategory category, string key)
	{
		Assert.Equal(key, CSliderText.TitleKey(category));
	}

	[Theory]
	[InlineData(ELeadershipCategory.Officers, "DISTRIBUTE_NCO")]
	[InlineData(ELeadershipCategory.Diplomacy, "DISTRIBUTE_DIPLOMACY")]
	[InlineData(ELeadershipCategory.Espionage, "DISTRIBUTE_ESPIONAGE")]
	[InlineData(ELeadershipCategory.Research, "DISTRIBUTE_RESEARCH")]
	public void EachLeadershipSliderIsTitledByItsOwnKey(ELeadershipCategory category, string key)
	{
		Assert.Equal(key, CSliderText.TitleKey(category));
	}
}
