using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What a line of the production screen's build queue shows, against
/// <c>docs/mechanics/productionscreen.md</c>, section 6.
/// </summary>
public class BuildQueueTextTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static string Localise(string key) => key == "IC" ? " IK" : key;

	private static CConstruction Line(EUnitBranch branch = EUnitBranch.Naval, string unitName = "")
	{
		var construction = new CConstruction(Ger)
		{
			Name = unitName,
			Cost = CFixedPoint.Parse("7.029"),
			Duration = CFixedPoint.FromInt(383),
			Progress = CFixedPoint.FromInt(309),
		};
		construction.Brigades.Add(new CBrigadeOrder(new CSubUnitType("heavy_cruiser", branch)) { Name = "Graf Spee" });
		return construction;
	}

	[Fact]
	public void ALineIsGreenPaidInFullOrangePaidInPartAndRedGivenNothing()
	{
		CConstruction construction = Line();
		Assert.Equal(EQueueBackground.Green, CBuildQueueText.Background(construction));

		construction.Status = CFixedPoint.Parse("0.991");
		Assert.Equal(EQueueBackground.Green, CBuildQueueText.Background(construction));
		construction.Status = CFixedPoint.Parse("0.990");
		Assert.Equal(EQueueBackground.Orange, CBuildQueueText.Background(construction));
		construction.Status = CFixedPoint.Parse("0.001");
		Assert.Equal(EQueueBackground.Orange, CBuildQueueText.Background(construction));
		construction.Status = CFixedPoint.Zero;
		Assert.Equal(EQueueBackground.Red, CBuildQueueText.Background(construction));
	}

	[Fact]
	public void TheSpeedIsWhatTheLineWasLastPaidAsAWholePercentage()
	{
		CConstruction construction = Line();
		Assert.Equal("100%", CBuildQueueText.Speed(construction));

		construction.Status = CFixedPoint.Parse("0.386");
		Assert.Equal("38%", CBuildQueueText.Speed(construction));
	}

	[Fact]
	public void TheCostIsCutToTwoDecimalsWithTheWordForIcStraightAfter()
	{
		// The German word is given a space of its own, which is how it comes to have one.
		Assert.Equal("7.02 IK", CBuildQueueText.Cost(Localise, Line()));
		Assert.Equal("7.02IC", CBuildQueueText.Cost(key => key, Line()));
	}

	[Fact]
	public void TheDateIsTheDayTheLineWillBeDoneAtThePaceItWasLastPaid()
	{
		CConstruction construction = Line();
		var today = new CDate(1936, 1, 1);

		// 74 days of work to go.
		Assert.Equal("1936 March 16", CBuildQueueText.Date(Localise, today, construction));

		construction.Status = CFixedPoint.Parse("0.5");
		Assert.Equal("1936 May 29", CBuildQueueText.Date(Localise, today, construction));

		construction.Status = CFixedPoint.Zero;
		Assert.Equal("---", CBuildQueueText.Date(Localise, today, construction));
	}

	[Fact]
	public void ASeriesSaysHowManyMoreFollow()
	{
		CConstruction construction = Line();
		Assert.Equal("", CBuildQueueText.Series(construction));

		construction.Size = 4;
		Assert.Equal("+3", CBuildQueueText.Series(construction));
	}

	[Fact]
	public void TheBarIsHowFarTheOneBeingBuiltHasGotInHundredths()
	{
		CConstruction construction = Line();
		Assert.Equal(80, CBuildQueueText.Progress(construction));           // 309 of 383, cut

		construction.Progress = CFixedPoint.FromInt(400);
		Assert.Equal(100, CBuildQueueText.Progress(construction));
		construction.Duration = CFixedPoint.Zero;
		Assert.Equal(0, CBuildQueueText.Progress(construction));
	}

	[Fact]
	public void ALineIsCalledAfterItsUnitOrItsFirstBrigade()
	{
		Assert.Equal("Graf Spee", CBuildQueueText.Name(Line()));
		Assert.Equal("1. Panzer-Division", CBuildQueueText.Name(Line(EUnitBranch.Land, "1. Panzer-Division")));
	}

	[Fact]
	public void TheCounterAndThePictureGoByWhatTheFirstBrigadeIs()
	{
		Assert.Equal("GFX_counter_heavy_cruiser", CBuildQueueText.CounterSprite(Line()));
		Assert.Equal("production_queue_naval", CBuildQueueText.KindElement(Line()));
		Assert.Equal("production_queue_air", CBuildQueueText.KindElement(Line(EUnitBranch.Air)));
		Assert.Equal("production_queue_division", CBuildQueueText.KindElement(Line(EUnitBranch.Land)));
	}
}
