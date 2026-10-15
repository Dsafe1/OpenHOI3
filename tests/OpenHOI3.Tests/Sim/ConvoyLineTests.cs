using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A convoy line ordered, priced, worked and finished, against <c>docs/mechanics/convoylines.md</c>, on
/// <see cref="RebelWorld"/> with Their Finest Hour's defines: Germany builds, Slovakia owns no land.
/// </summary>
public class ConvoyLineTests
{
	private static CGameState World()
	{
		CGameState state = RebelWorld.State();
		state.Defines.Set(CConvoyLines.Section, CConvoyLines.ConvoyCostDefine, CFixedPoint.FromInt(2));
		state.Defines.Set(CConvoyLines.Section, CConvoyLines.ConvoyTimeDefine, CFixedPoint.FromInt(100));
		state.Defines.Set(CConvoyLines.Section, CConvoyLines.EscortCostDefine, CFixedPoint.FromInt(4));
		state.Defines.Set(CConvoyLines.Section, CConvoyLines.EscortTimeDefine, CFixedPoint.FromInt(240));
		state.Defines.Set(CConvoyLines.Section, CConvoyLines.SizeDefine, CFixedPoint.FromInt(10));
		return state;
	}

	[Fact]
	public void An_item_costs_the_define_a_day_for_the_defines_days_and_the_technology_changes_both()
	{
		CGameState state = World();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		Assert.Equal(CFixedPoint.FromInt(2), CConvoyLines.Cost(state, germany, escorts: false));
		Assert.Equal(CFixedPoint.FromInt(100), CConvoyLines.Time(state, germany, escorts: false));
		Assert.Equal(CFixedPoint.FromInt(4), CConvoyLines.Cost(state, germany, escorts: true));
		Assert.Equal(CFixedPoint.FromInt(240), CConvoyLines.Time(state, germany, escorts: true));

		// A tenth off the cost and a little off the time: the time is cut to whole days only on the line.
		germany.Technology.Set(CConvoyLines.ConvoyCostEffect, CFixedPoint.Parse("-0.1"));
		germany.Technology.Set(CConvoyLines.ConvoyTimeEffect, CFixedPoint.Parse("-0.005"));
		Assert.Equal(CFixedPoint.Parse("1.8"), CConvoyLines.Cost(state, germany, escorts: false));
		Assert.Equal(CFixedPoint.Parse("99.5"), CConvoyLines.Time(state, germany, escorts: false));
		CConstruction line = CConvoyLines.NewLine(state, germany, escorts: false, amount: 3);
		Assert.Equal(CFixedPoint.FromInt(99), line.Duration);

		// Never below a hundredth.
		germany.Technology.Set(CConvoyLines.EscortCostEffect, CFixedPoint.FromInt(-2));
		Assert.Equal(CFixedPoint.Parse("0.01"), CConvoyLines.Cost(state, germany, escorts: true));
	}

	[Fact]
	public void An_order_is_never_refused_and_makes_a_new_line_at_the_end_each_time()
	{
		CGameState state = World();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		// Escorts with no technology allowing them, and an amount of nought, are both taken.
		var escorts = new CConstructConvoyCommand(germany.Tag, escorts: true, amount: 0);
		Assert.True(escorts.MayExecute(state));
		escorts.Execute(state);
		new CConstructConvoyCommand(germany.Tag, escorts: false, amount: 5).Execute(state);
		new CConstructConvoyCommand(germany.Tag, escorts: false, amount: 5).Execute(state);

		Assert.Equal(3, germany.Constructions.Count);
		Assert.True(germany.Constructions[0].IsEscorts);
		Assert.Equal(0, germany.Constructions[0].Size);
		Assert.All(germany.Constructions.GetRange(1, 2), line => Assert.Equal((5, false), (line.Size, line.IsEscorts)));
		Assert.Equal(CFixedPoint.FromInt(4 + 2 + 2), germany.ProductionCost);
		Assert.Equal(100, CConvoyLines.BeingBuilt(state, germany, escorts: false));
		Assert.Equal(0, CConvoyLines.BeingBuilt(state, germany, escorts: true));
	}

	[Fact]
	public void A_line_is_priced_once_and_passed_over_when_the_queue_is_priced_again()
	{
		CGameState state = World();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		new CConstructConvoyCommand(germany.Tag, escorts: false, amount: 2).Execute(state);
		germany.Technology.Set(CConvoyLines.ConvoyCostEffect, CFixedPoint.Parse("-0.5"));
		CBuildQueue.RecalculateAll(germany);
		Assert.Equal(CFixedPoint.FromInt(2), germany.Constructions[0].Cost);
		Assert.Equal(CFixedPoint.FromInt(100), germany.Constructions[0].Duration);
	}

	[Fact]
	public void Each_item_finished_gives_ten_ships_and_what_was_paid_past_it_goes_to_the_next()
	{
		CGameState state = World();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		int transports = germany.Transports;
		new CConstructConvoyCommand(germany.Tag, escorts: false, amount: 2).Execute(state);
		CConstruction line = germany.Constructions[0];
		line.Progress = CFixedPoint.FromInt(99);

		CBuildQueue.RunDaily(state, germany, CFixed15.FromInt(10));
		Assert.Equal(transports + 10, germany.Transports);
		Assert.Equal(1, line.Size);
		Assert.Equal(CFixedPoint.Zero, line.Progress);
		Assert.Equal(CFixedPoint.Zero, line.AccumulatedExperience);
		Assert.Same(line, germany.Constructions[0]);

		line.Progress = CFixedPoint.FromInt(107);
		Assert.True(CBuildQueue.Deliver(state, germany, line));
		Assert.Equal(transports + 20, germany.Transports);
		Assert.Equal(CFixedPoint.FromInt(7), line.Progress);
	}

	[Fact]
	public void An_item_is_given_to_a_country_that_holds_no_land_and_escorts_go_to_the_escorts()
	{
		CGameState state = World();
		CCountry slovakia = state.Country(RebelWorld.Slo)!;
		CConstruction line = CConvoyLines.NewLine(state, slovakia, escorts: true, amount: 1);
		line.Progress = line.Duration;
		Assert.True(CBuildQueue.Deliver(state, slovakia, line));
		Assert.Equal(10, slovakia.Escorts);
		Assert.Equal(0, slovakia.Transports);
	}
}
