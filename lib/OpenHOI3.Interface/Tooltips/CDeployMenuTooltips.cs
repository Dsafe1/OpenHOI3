using System;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What a line of the deploy menu says in a tooltip, anywhere on it: its unit's brigades, each
/// by its counter and its name. See <c>docs/mechanics/mapunits.md</c>, section 10.
/// </summary>
public static class CDeployMenuTooltips
{
	/// <summary>A finished unit's line: a land unit's brigades, and nothing for a fleet or an air unit - the original's.</summary>
	public static CTooltip Entry(Func<string, string> localise, CUnit unit)
	{
		return CMovementSpeed.IsLand(unit) ? Target(localise, unit) : CTooltip.Empty;
	}

	/// <summary>A line of a unit the new one may join: its brigades, ships or wings, whatever it is.</summary>
	public static CTooltip Target(Func<string, string> localise, CUnit unit)
	{
		string text = CUnitHeaderTooltips.Brigades(localise, unit);
		return text.Length == 0 ? CTooltip.Empty : new CTooltip { Text = text };
	}
}
