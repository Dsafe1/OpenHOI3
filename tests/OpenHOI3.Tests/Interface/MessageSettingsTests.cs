using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Messages;
using OpenHOI3.Interface.Messages;

namespace OpenHOI3.Tests.Interface;

/// <summary>The in-game menu's message settings, against <c>docs/mechanics/ingamemenu.md</c>, section 4.</summary>
public class MessageSettingsTests
{
	private static readonly Dictionary<string, string> Table = new()
	{
		["ATTACKUS_SETUP"] = "When one of our land units is attacked by the enemy",
		["MESSAGE_IS_LOG"] = "This message type $TRUTH$ currently displayed in the log.",
		["MESSAGE_IS_PAUSE"] = "This message type $TRUTH$ currently displayed as a popup and will pause the game.",
		["MESSAGE_IS_POPUP"] = "This message type $TRUTH$ currently displayed as a popup.",
		["MESSAGE_SETTING_TRUE"] = "is",
		["MESSAGE_SETTING_FALSE"] = "is not",
		["MESSAGECAT_OTHERS"] = "Other Messages",
		["MESSAGECAT_PROVS"] = "Province Messages",
	};

	private static string Localise(string key) => Table.GetValueOrDefault(key, key);

	private static CMessageTypes Types()
	{
		return CMessageTypes.Read(ClausewitzParser.Parse(
			"ATTACKUS = { log = yes popup = yes categories = \"cat_combat\" }\n"
			+ "DIPLOEXPIRE = { log = no categories = \"cat_diplomacy\" }\n"
			+ "WEATTACK = { log = yes pausepopup = yes categories = \"cat_combat\" }\n"), _ => { });
	}

	[Fact]
	public void A_tab_lists_its_category_in_order_and_all_lists_every_type()
	{
		CMessageTypes types = Types();

		Assert.Equal(new[] { "ATTACKUS", "WEATTACK" },
			CMessageSettingsText.Listed(types, EMessageCategory.Combat).Select(type => type.Name));
		Assert.Equal(3, CMessageSettingsText.Listed(types, null).Count());
		Assert.Equal("When one of our land units is attacked by the enemy",
			CMessageSettingsText.RowText(Localise, types.Find("ATTACKUS")));
	}

	[Fact]
	public void A_box_is_ticked_where_its_setting_is_off_and_a_click_writes_all_four()
	{
		CMessageType attack = Types().Find("ATTACKUS");

		Assert.False(CMessageSettingsText.Ticked(attack, CMessageSettingsText.LogBox));
		Assert.False(CMessageSettingsText.Ticked(attack, CMessageSettingsText.DialogBox));
		Assert.True(CMessageSettingsText.Ticked(attack, CMessageSettingsText.PauseBox));
		Assert.True(CMessageSettingsText.Ticked(attack, CMessageSettingsText.MapBox));

		CMessageSettingsText.Apply(attack, pauseTicked: false, logTicked: true, dialogTicked: true, mapTicked: false);

		Assert.True(attack.PausePopup);
		Assert.False(attack.Log);
		Assert.False(attack.Popup);
		Assert.True(attack.OnMap);
	}

	[Fact]
	public void The_pause_boxs_tooltip_tests_the_log_as_the_originals_does()
	{
		CMessageTypes types = Types();

		Assert.Equal("This message type is not currently displayed as a popup and will pause the game.",
			CMessageSettingsText.BoxTooltip(Localise, types.Find("DIPLOEXPIRE"), CMessageSettingsText.PauseBox));
		// Paused and logged: "is"; a popup only through pausepopup: "is not".
		Assert.Equal("This message type is currently displayed as a popup and will pause the game.",
			CMessageSettingsText.BoxTooltip(Localise, types.Find("WEATTACK"), CMessageSettingsText.PauseBox));
		Assert.Equal("This message type is not currently displayed as a popup.",
			CMessageSettingsText.BoxTooltip(Localise, types.Find("WEATTACK"), CMessageSettingsText.DialogBox));
	}

	[Fact]
	public void The_all_tab_has_no_tooltip_and_the_provinces_tab_says_provs()
	{
		Assert.Null(CMessageSettingsText.TabTooltip(Localise, CMessageSettingsText.AllTab));
		Assert.Equal("Province Messages", CMessageSettingsText.TabTooltip(Localise, "messagecat_provinces"));
		Assert.Equal("Other Messages", CMessageSettingsText.TabTooltip(Localise, "messagecat_others"));
		Assert.Equal(7, CMessageSettingsText.Tabs.Count);
	}
}
