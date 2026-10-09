using System;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the deploy menu writes: its title, and a line for a unit waiting to be placed
/// or for a unit in the field it may join. See <c>docs/mechanics/mapunits.md</c>,
/// section 6.
/// </summary>
public static class CDeployMenuText
{
	/// <summary>The alert that says units are waiting, and the two keys of its tooltip.</summary>
	public const string Alert = "alert_deployable_units";

	/// <summary>Where that alert is among the original's sixteen, from 0. Its icon shows the frame after.</summary>
	public const int AlertNumber = 1;

	private const string TitleKey = "DEPLOY_UNITS";
	private const string TargetTitleKey = "DEPLOY_SEL_TARGET";

	/// <summary>The menu's heading: one while a unit is being picked, another while it is being given a place.</summary>
	public static string Title(Func<string, string> localise, bool choosingTarget)
	{
		return localise(choosingTarget ? TargetTitleKey : TitleKey);
	}

	/// <summary>The sprite of the picture of a unit's kind in a list, or an empty string for a unit of nothing.</summary>
	public static string CounterSprite(CUnit unit)
	{
		return CCounterText.Sprite(CCounterText.ListSprite, unit);
	}

	/// <summary>The frame of the size mark beside it.</summary>
	public static int SizeFrame(CUnit unit)
	{
		return CCounterText.SizeFrame(unit);
	}

	/// <summary>Where a unit in the field is: its province's name.</summary>
	public static string Location(CGameState state, CUnit unit)
	{
		return unit.Location > 0 ? state.Province(unit.Location).Name : "";
	}
}
