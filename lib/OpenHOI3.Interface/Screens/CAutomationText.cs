using System;
using OpenHOI3.Sim.Settings;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the country screens' automation line says, <c>automation_setting</c> between <c>mode_arrow_left</c> and
/// <c>mode_arrow_right</c>, and the production screen's slider automation, <c>slider_automation_setting</c>; each arrow's
/// step. See <c>docs/mechanics/frontend.md</c>, section 4.4; <c>reversing/findings/FINDINGS-frontend.md</c>, C.4.
/// </summary>
public static class CAutomationText
{
	/// <summary>An area's two values on the screens, where the lobby says "Player Control" for the first [exe].</summary>
	private static readonly string[] TwoWays = { "OPT_NORMAL", "OPT_AICONTROL" };

	/// <summary>The technology screen's four, the Technology setting's.</summary>
	private static readonly string[] Technology = { "OPT_NORMAL", "OPT_AICONTROL", "TECH_AUTOMATED", "LEADERSHIP_AUTOMATED" };

	/// <summary>The slider automation's four levels.</summary>
	public static readonly string[] SliderLevels =
		{ "AUTOMATE_MANUAL", "AUTOMATE_PRODUCTION", "AUTOMATE_UPGRADES", "AUTOMATE_REINFORCEMENT" };

	/// <summary>An area's values on its screen: the technology screen's four, every other's two.</summary>
	public static string[] Values(EAutomation area) => area == EAutomation.Technology ? Technology : TwoWays;

	/// <summary>The line for an area: its value's text, the Technology setting's for technology.</summary>
	public static string Setting(Func<string, string> localise, CPlayerAutomation automation, EAutomation area)
	{
		return CLobbyOptionsText.Text(localise, Values(area), CLobbyOptionsText.Value(automation, area));
	}

	/// <summary>The slider automation's line; nothing for a level out of range [exe].</summary>
	public static string Sliders(Func<string, string> localise, CPlayerAutomation automation)
	{
		return CLobbyOptionsText.Text(localise, SliderLevels, automation.Sliders);
	}

	/// <summary>
	/// The value an arrow sets: the left one up and the right one down, each wrapping [exe] - an area's flag between its
	/// two, the Technology setting and the slider automation round their four.
	/// </summary>
	public static int Step(int value, bool left, int count)
	{
		return CLobbyOptionsText.Step(value, left ? 1 : -1, count);
	}
}
