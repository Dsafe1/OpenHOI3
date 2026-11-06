using System.Collections.Generic;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim.Settings;

namespace OpenHOI3.Tests.Interface;

/// <summary>The lobby's Options panel, as <c>docs/mechanics/frontend.md</c>, section 4, has it.</summary>
public class LobbyOptionsTextTests
{
	private static readonly Dictionary<string, string> Table = new()
	{
		["DIFF_HARD"] = "Hard",
		["FE_DIFFICULTY"] = "Difficulty:",
		["OPTION_CHANGED"] = "$FROM$ has changed to: $TO$",
		["LEADERSHIP_AUTOMATED"] = "Slider AI",
	};

	private static string Localise(string key) => Table.TryGetValue(key, out string? text) ? text : key;

	[Fact]
	public void An_arrow_wraps_past_either_end()
	{
		Assert.Equal(0, CLobbyOptionsText.Step(4, 1, 5));
		Assert.Equal(4, CLobbyOptionsText.Step(0, -1, 5));
		Assert.Equal(3, CLobbyOptionsText.Step(0, -1, 4));
		Assert.Equal(1, CLobbyOptionsText.Step(0, 1, 2));
	}

	[Fact]
	public void The_Technology_row_writes_the_setting_and_leaves_its_flag_alone()
	{
		var automation = new CPlayerAutomation();

		CLobbyOptionsText.Set(automation, EAutomation.Technology, 3);
		CLobbyOptionsText.Set(automation, EAutomation.Politics, 1);

		Assert.Equal(3, automation.Technology);
		Assert.Equal(0, automation.Flags[(int)EAutomation.Technology]);
		Assert.Equal(1, automation.Flags[(int)EAutomation.Politics]);
		Assert.Equal(3, CLobbyOptionsText.Value(automation, EAutomation.Technology));
		Assert.Equal(4, CLobbyOptionsText.Values(EAutomation.Technology).Length);
		Assert.Equal(2, CLobbyOptionsText.Values(EAutomation.Diplomacy).Length);
	}

	[Fact]
	public void The_screens_say_Normal_where_the_lobby_says_Player_Control_and_their_arrows_wrap()
	{
		var automation = new CPlayerAutomation { Technology = 3, Sliders = 2 };
		automation.Flags[(int)EAutomation.Diplomacy] = 1;

		Assert.Equal("OPT_NORMAL", CAutomationText.Setting(Localise, automation, EAutomation.Politics));
		Assert.Equal("OPT_AICONTROL", CAutomationText.Setting(Localise, automation, EAutomation.Diplomacy));
		Assert.Equal("Slider AI", CAutomationText.Setting(Localise, automation, EAutomation.Technology));
		Assert.Equal("AUTOMATE_UPGRADES", CAutomationText.Sliders(Localise, automation));
		// The left arrow up, the right one down.
		Assert.Equal(0, CAutomationText.Step(3, left: true, 4));
		Assert.Equal(3, CAutomationText.Step(0, left: false, 4));
		Assert.Equal(0, CAutomationText.Step(1, left: true, 2));
	}

	[Fact]
	public void A_change_keeps_the_labels_colon_and_a_value_out_of_range_has_no_text()
	{
		Assert.Equal("Difficulty: has changed to: Hard", CLobbyOptionsText.Changed(Localise, "FE_DIFFICULTY",
			CLobbyOptionsText.Text(Localise, CLobbyOptionsText.Difficulties, 3)));
		Assert.Equal("Slider AI", CLobbyOptionsText.Text(Localise, CLobbyOptionsText.Values(EAutomation.Technology), 3));
		Assert.Equal("", CLobbyOptionsText.Text(Localise, CLobbyOptionsText.Difficulties, 5));
	}
}
