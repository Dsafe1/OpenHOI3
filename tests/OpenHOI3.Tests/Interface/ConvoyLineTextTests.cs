using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the production screen shows of transports and escorts - the confirm-build dialog, the counts and the queue's
/// line - against <c>docs/mechanics/convoylines.md</c>, sections 1 and 4.
/// </summary>
public class ConvoyLineTextTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static string Localise(string key) => key switch
	{
		"DAYS" => "Days",
		"MONTHS" => "Months",
		"CC_CONVOY" => "Convoy",
		"CC_ESCORT" => "Escort",
		_ => key,
	};

	[Fact]
	public void The_dialogs_time_is_serial_times_the_items_time_in_days_to_a_year_and_months_beyond()
	{
		Assert.Equal("300 Days", CConfirmBuildText.ConvoyTime(Localise, 3, CFixedPoint.FromInt(100)));
		Assert.Equal("13 Months", CConfirmBuildText.ConvoyTime(Localise, 4, CFixedPoint.FromInt(100)));
		Assert.Equal("16 Months", CConfirmBuildText.ConvoyTime(Localise, 2, CFixedPoint.FromInt(240)));
		// The thousandths are kept until the whole is cut: three items of 99.5 days are 298, not 297.
		Assert.Equal("298 Days", CConfirmBuildText.ConvoyTime(Localise, 3, CFixedPoint.Parse("99.5")));
		Assert.Equal("6.00", CConfirmBuildText.Cost(3, CFixedPoint.FromInt(2)));
		Assert.Equal(("CONVOYS", "picture_convoy"), (CConfirmBuildText.ConvoyLabel(false), CConfirmBuildText.ConvoyPicture(false)));
		Assert.Equal(("ESCORTS", "picture_escort"), (CConfirmBuildText.ConvoyLabel(true), CConfirmBuildText.ConvoyPicture(true)));
	}

	[Fact]
	public void A_convoy_line_is_called_convoy_or_escort_with_no_count_and_says_nothing_of_brigades()
	{
		var transports = new CConstruction(Ger) { IsConvoy = true, Size = 5 };
		var escorts = new CConstruction(Ger) { IsConvoy = true, IsEscorts = true };
		Assert.Equal("Convoy", CBuildQueueText.ConvoyName(Localise, transports));
		Assert.Equal("Escort", CBuildQueueText.ConvoyName(Localise, escorts));
		Assert.Equal("icon_build_convoy", CBuildQueueText.CounterSprite(transports));
		Assert.Equal("icon_build_escort", CBuildQueueText.CounterSprite(escorts));
		Assert.Equal("production_queue_buildings", CBuildQueueText.KindElement(transports));
		Assert.Equal("+4", CBuildQueueText.Series(transports));
		Assert.Equal("", CProductionTooltips.QueueLine(Localise, transports, "counter_type").Text);
	}

	[Fact]
	public void The_counts_carry_the_ships_still_to_come()
	{
		Assert.Equal("12 (+50)", CProductionScreenText.Convoys(12, 50));
		Assert.Equal("12", CProductionScreenText.Convoys(12, 0));
	}
}
