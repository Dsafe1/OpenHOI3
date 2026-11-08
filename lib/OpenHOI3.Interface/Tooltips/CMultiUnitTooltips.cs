using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the multi-unit panel's elements say, by name, as the in-game screen answers for them. See
/// <c>docs/mechanics/selection.md</c>, section 5.2, and <c>transport.md</c>, section 12, for the carrying three.
/// </summary>
public static class CMultiUnitTooltips
{
	public static CTooltip For(Func<string, string> localise, CGameState state, IReadOnlyList<CUnit> units, string element)
	{
		switch (element)
		{
			case "prio_checkbox":
				return Flags(localise, "MULTI_SEL_PRIO_DESC", units, EUnitSupport.Priority);
			case "reinf_checkbox":
				return Flags(localise, "MULTI_SEL_PRIO_REINF_DESC", units, EUnitSupport.Reinforcement);
			case "upgrade_checkbox":
				return Flags(localise, "MULTI_SEL_PRIO_UPG_DESC", units, EUnitSupport.Upgrade);
			case "load_multiunit":
				return Load(localise, units, CMultiUnitText.FleetFor(state, units), "NO_MULTILOAD");
			case "load_on_air_multiunit":
				return Load(localise, units, CMultiUnitText.PlaneFor(state, units), "NO_MULTILOAD_AIR");
			case "unmount_multiunit":
				int aboard = CMultiUnitText.Aboard(units);
				return new CTooltip
				{
					Text = aboard == 0
						? localise("UNMOUNT_MULTILOAD_NONE")
						: CTextTemplate.Fill(localise("UNMOUNT_MULTILOAD"), ("NUM", aboard.ToString(CultureInfo.InvariantCulture))),
				};
			case "detach_multiunit":
				return new CTooltip { Text = localise("DETACH_ALL") };
			case "highercomm_multiunit":
				return new CTooltip { Text = localise("HIGHER_COMMAND_MULTI_UNITS") };
			case "reorganizebutton":
				return new CTooltip
				{
					Text = localise(units.Count < 3 ? "REORGANISE_UNITS" : "NO_REORGANISE_UNITS"),
					Delayed = localise("REORGANISE_UNITS_DRO"),
				};
			case "mergebutton":
				return new CTooltip { Text = localise("MERGE_UNITS"), Delayed = localise("MERGE_UNITS_DRO") };
			case "upgrade_all":
				return state.Country(units.Count == 0 ? null : units[0].Owner) is { } country
					? new CTooltip
					{
						Text = CTextTemplate.Fill(localise("MULTI_SEL_UPGRADE_DESC"),
							("NUM", CBrigadeUpgrades.CountUpgradeable(state, country, units).ToString(CultureInfo.InvariantCulture))),
					}
					: CTooltip.Empty;
			case "disband_multiunit":
				return new CTooltip { Text = localise("DISBAND_ALL_TITLE") };
			default:
				// close_multiunit says nothing.
				return CTooltip.Empty;
		}
	}

	private static CTooltip Flags(Func<string, string> localise, string key, IReadOnlyList<CUnit> units, EUnitSupport what)
	{
		return new CTooltip
		{
			Text = CTextTemplate.Fill(localise(key),
				("NUM", CMultiUnitText.FlagCount(units, what).ToString(CultureInfo.InvariantCulture)),
				("MAX", units.Count.ToString(CultureInfo.InvariantCulture))),
		};
	}

	/// <summary>A load button: the carrier found, or that there is none; and always what the units weigh together.</summary>
	private static CTooltip Load(Func<string, string> localise, IReadOnlyList<CUnit> units, CUnit? carrier, string none)
	{
		return new CTooltip
		{
			Text = carrier is null ? localise(none) : localise("MUL_LOAD_ONTO") + " §Y" + carrier.Name + "§W",
			Delayed = localise("TOTAL_LOAD_WILL_BE") + " §Y"
				+ CMultiUnitText.Weight(units).ToString(CultureInfo.InvariantCulture) + "§W",
		};
	}
}
