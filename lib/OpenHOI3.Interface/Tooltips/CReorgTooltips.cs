using System;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the reorganise window's elements say, and its answer is final: a fleet's ratio line its description, any element
/// of a row the brigade's summary, everything else nothing. See <c>docs/mechanics/reorganise.md</c>, section 6.
/// </summary>
public static class CReorgTooltips
{
	/// <summary>An element of the window itself.</summary>
	public static CTooltip Window(Func<string, string> localise, CUnit left, CUnit right, string element)
	{
		if ((element == CReorgText.RatioLeft || element == CReorgText.RatioRight) && CReorgText.ShowsFleetLines(left, right))
		{
			return new CTooltip { Text = localise("REORG_WND_SHIPS_RATIO_DESC") };
		}
		return CTooltip.Empty;
	}

	/// <summary>Any element of a row, its arrow too: the brigade's summary, as the unit panel's row gives it.</summary>
	public static CTooltip Row(Func<string, string> localise, Func<string, string?> find, CGameState state, CCountry owner,
		CSubUnit subUnit)
	{
		return new CTooltip { Text = CBrigadeTooltips.Summary(localise, find, state, owner, subUnit) };
	}
}
