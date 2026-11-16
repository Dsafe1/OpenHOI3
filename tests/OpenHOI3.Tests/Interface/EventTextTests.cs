using OpenHOI3.Content.Gui;
using OpenHOI3.Interface.Events;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// An event's window and its text, against <c>docs/mechanics/events.md</c>, sections 10 and 11, on
/// <see cref="ScriptWorld"/>'s strip of ground.
/// </summary>
public class EventTextTests
{
	private static string Localise(string key) => key switch
	{
		"EVT_TEXT" => "$COUNTRY$ ($COUNTRY_ADJ$) and $FROMCOUNTRY$ in $PROVINCENAME$, $YEAR$; $NOTHING$",
		"POL_ADJ" => "Polish",
		"GER_ADJ" => "German",
		_ => key,
	};

	[Fact]
	public void Variables_are_filled_from_the_scope_and_one_nothing_gives_is_left()
	{
		CGameState state = ScriptWorld.State();
		CEventScope scope = CEventScope.Start(ScriptWorld.Pol, 4, fromCountry: ScriptWorld.Ger);

		Assert.Equal("POL (Polish) and GER in p4, 1936; $NOTHING$", CEventText.Localise(Localise, state, "EVT_TEXT", scope));
	}

	[Fact]
	public void The_last_battles_losses_are_always_given_as_whole_numbers()
	{
		CGameState state = ScriptWorld.State();
		state.LastBattleLosses.LandLoser = 4512;
		state.LastBattleLosses.NavalWinner = 3;

		var values = CEventText.Variables(Localise, state, CEventScope.Start(null));

		Assert.Equal("4512", values["LAND_LOSER_LOSSES"]);
		Assert.Equal("0", values["LAND_WINNER_LOSSES"]);
		Assert.Equal("0", values["AIR_LOSER_LOSSES"]);
		Assert.Equal("0", values["AIR_WINNER_LOSSES"]);
		Assert.Equal("0", values["NAVAL_LOSER_LOSSES"]);
		Assert.Equal("3", values["NAVAL_WINNER_LOSSES"]);
	}

	[Fact]
	public void A_scope_without_a_country_takes_its_provinces_controller()
	{
		CGameState state = ScriptWorld.State();
		state.Province(3).Controller = ScriptWorld.Pol;

		var values = CEventText.Variables(Localise, state, CEventScope.Start(null, 3));

		Assert.Equal("POL", values["COUNTRY"]);
		Assert.Equal("GER", values["OWNERNAME"]);
		Assert.False(values.ContainsKey("THISCOUNTRY"));
	}

	[Fact]
	public void Options_grow_upwards_from_two_steps_above_the_bottom()
	{
		var offset = new CGuiPoint(6, 25);

		Assert.Equal(new CGuiPoint(6, 360), CEventWindowLayout.OptionPosition(410, 1, 0, offset));
		Assert.Equal(new CGuiPoint(6, 310), CEventWindowLayout.OptionPosition(410, 3, 0, offset));
		Assert.Equal(new CGuiPoint(6, 360), CEventWindowLayout.OptionPosition(410, 3, 2, offset));
	}

	[Fact]
	public void The_provinces_window_is_for_a_scope_with_a_province_whatever_the_event()
	{
		Assert.Equal("Event_Province_Window", CEventWindowLayout.WindowName(CEventScope.Start(ScriptWorld.Ger, 2)));
		Assert.Equal("EventWindow", CEventWindowLayout.WindowName(CEventScope.Start(ScriptWorld.Ger)));
		Assert.Equal("EventPOptionButton", CEventWindowLayout.ButtonType(CEventScope.Start(null, 2)));
	}
}
