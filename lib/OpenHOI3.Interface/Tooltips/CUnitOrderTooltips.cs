using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim.Units;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the rest of a selected unit's panel says in a tooltip: a unit above it, its order and
/// what each kind of order calls itself, the strip of buttons and a headquarters' row for the
/// AI. See <c>docs/mechanics/unitpanel.md</c>, section 10.
/// </summary>
public static class CUnitOrderTooltips
{
	/// <summary>
	/// What each order calls itself: the original's table from an order to its mission and from
	/// a mission to a description, for the orders built here.
	/// </summary>
	private static readonly Dictionary<EUnitOrder, string> Descriptions = new()
	{
		[EUnitOrder.Move] = "MOVE_ORDER_DESC",
		[EUnitOrder.Rebase] = "ORDER_REBASE_DESC",
		[EUnitOrder.RebaseAir] = "ORDER_AIR_REBASE_DESC",
		[EUnitOrder.Reserve] = "ORDER_RESERVE_DESC",
		[EUnitOrder.Patrol] = "ORDER_PATROL_DESC",
		[EUnitOrder.Intercept] = "ORDER_INTERCEPT_DESC",
		[EUnitOrder.Sortie] = "ORDER_SORTIE_DESC",
		[EUnitOrder.ConvoyEscort] = "ORDER_CONVOY_ESCORT_DESC",
		[EUnitOrder.ConvoyRaid] = "ORDER_CONVOY_RAID_DESC",
		[EUnitOrder.Transport] = "ORDER_TRANSPORT_DESC",
		[EUnitOrder.Invasion] = "ORDER_INVADE_DESC",
		[EUnitOrder.AirSuperiority] = "AIRSUP_DESC",
		[EUnitOrder.AirIntercept] = "INTERCEPT_DESC",
		[EUnitOrder.CarrierProtection] = "CAG_DUTY_DESC",
		[EUnitOrder.Paradrop] = "PARADROP_MISSION_DESC",
		[EUnitOrder.Nuke] = "NUKE_MISSION_DESC",
		[EUnitOrder.RebaseCarrier] = "REBASE_TO_CAG_MISSION_DESC",
		[EUnitOrder.AirSupply] = "TRANS_SUP_MISSION_DESC",
		[EUnitOrder.GroundAttack] = "GROUNDATTACK_BOMB_DESC",
		[EUnitOrder.Interdiction] = "INTERDICT_BOMB_DESC",
		[EUnitOrder.NavalStrike] = "NAVALSTRIKE_BOMB_DESC",
		[EUnitOrder.AirConvoyRaid] = "AIR_CONVOY_DESC",
		[EUnitOrder.StrategicBombing] = "STRAT_BOMB_DESC",
		[EUnitOrder.LogisticalStrike] = "LOG_BOMB_DESC",
		[EUnitOrder.RunwayCratering] = "AIRFIELD_BOMB_DESC",
		[EUnitOrder.InstallationStrike] = "INSTALLATION_BOMB_DESC",
		[EUnitOrder.PortStrike] = "PORTSTRIKE_BOMB_DESC",
	};

	/// <summary>The checkboxes, each with its two keys and what it reads of the unit.</summary>
	private static readonly Dictionary<string, (string On, string Off, Func<CUnit, bool> IsOn)> Checkboxes = new()
	{
		["prio_checkbox"] = ("UW_TURN_OFF_PRIO", "UW_TURN_ON_PRIO", unit => unit.IsPrioritised),
		["reinf_checkbox"] = ("UW_TURN_OFF_REINF", "UW_TURN_ON_REINF", unit => unit.MayReinforce),
		["upgrade_checkbox"] = ("UW_TURN_OFF_UPGRADE", "UW_TURN_ON_UPGRADE", unit => unit.MayUpgrade),
	};

	private static readonly Dictionary<string, string> MakeAbove = new()
	{
		["create_corps"] = "UW_CREATE_CORPS",
		["create_army"] = "UW_CREATE_ARMY",
		["create_armygroup"] = "UW_CREATE_ARMYGROUP",
		["create_theatre"] = "UW_CREATE_THEATHRE",
	};

	/// <summary>A unit above, anywhere on its row.</summary>
	public static CTooltip Parent(Func<string, string> localise)
	{
		return new CTooltip { Text = localise("SELECT_PARENT_UNIT") };
	}

	/// <summary>
	/// What a unit's order calls itself. A unit with no order says nothing - the original's idle
	/// order has an empty description - and one sent straight somewhere is on the march.
	/// </summary>
	public static string Description(Func<string, string> localise, CUnit unit)
	{
		if (unit.Order == EUnitOrder.None)
		{
			return unit.IsMoving ? localise(Descriptions[EUnitOrder.Move]) : "";
		}
		return localise(DescriptionKey(unit.Order));
	}

	/// <summary>The key of what an order calls itself, or an empty string for none.</summary>
	public static string DescriptionKey(EUnitOrder order)
	{
		return Descriptions.GetValueOrDefault(order, "");
	}

	/// <summary>
	/// The order's row: the button that calls it off, and its description anywhere else. Every
	/// order built here may be called off, so the button never says it may not.
	/// </summary>
	public static CTooltip Orders(Func<string, string> localise, CUnit unit, string element)
	{
		if (element == "cancel_order_button")
		{
			return new CTooltip { Text = localise("CANCEL_CURRENT_ORDER") };
		}
		return new CTooltip { Text = Description(localise, unit) };
	}

	/// <summary>
	/// The strip of buttons. A button switched off says what the original says when it may not
	/// be pressed. The load and unload buttons say whether a fleet is found and whether the
	/// fleet is in port (<c>transport.md</c>, section 12); the planes' whether an air unit is
	/// found - or that the brigades are too worn - and whether it is at its base (section 13.6).
	/// </summary>
	public static CTooltip Buttons(Func<string, string> localise, CUnit unit, bool attachEnabled, string element,
		Screens.CCarryButtons? carry = null)
	{
		carry ??= Screens.CCarryButtons.None;
		if (Checkboxes.TryGetValue(element, out (string On, string Off, Func<CUnit, bool> IsOn) box))
		{
			return new CTooltip { Text = localise(box.IsOn(unit) ? box.On : box.Off) };
		}
		if (MakeAbove.TryGetValue(element, out string? make))
		{
			return new CTooltip { Text = localise(make), Delayed = localise("UW_CREATE_ABOVE_DRO") };
		}
		return element switch
		{
			"attach_button" => new CTooltip { Text = localise(attachEnabled ? "UW_ATTACH" : "UW_NOT_POSSIBLE_TO_ATTACH") },
			"detach_button" => new CTooltip { Text = localise("UW_DETACH") },
			"oob_button" => new CTooltip { Text = localise("UW_OPEN_OOB_BROWSER") },
			"disbandbutton" => new CTooltip { Text = localise("DISBAND_UNIT"), Delayed = localise("DISBAND_UNIT_DRO") },
			"newunitbutton" => new CTooltip
			{
				Text = localise("NEW_UNIT"),
				Delayed = localise("NEW_UNIT_DRO") + "\n\n" + CTextTemplate.Fill(localise("SPLIT_UNITS_DESC"),
					("NUM", unit.SubUnits.Count.ToString(CultureInfo.InvariantCulture))),
			},
			"edit_theatre" => new CTooltip { Text = localise("UW_EDIT_THEATRE"), Delayed = localise("UW_EDIT_THEATRE_DRO") },
			"unload_button" => new CTooltip { Text = localise(carry.UnloadEnabled ? "UW_UNLOAD_VALID" : "UW_UNLOAD_NOT_VALID") },
			"load_button" => new CTooltip { Text = localise(carry.LoadEnabled ? "UW_LOAD_IS_VALID" : "UW_LOAD_NO_FLEET_FOUND") },
			"unload_plane_button" => new CTooltip
			{
				Text = localise(carry.PlaneUnloadEnabled ? "UW_UNLOAD_PLANE_BASE_OK" : "UW_UNLOAD_PLANE_BASE_ONLY"),
			},
			"load_plane_button" => new CTooltip
			{
				Text = localise(carry.PlaneLoadEnabled ? "UW_LOAD_PLANE"
					: carry.LowOrganisation ? "UW_LOAD_PLANE_FAIL_LOW_ORG" : "UW_LOAD_PLANE_FAIL"),
			},
			_ => CTooltip.Empty,
		};
	}

	/// <summary>
	/// A headquarters' row for the AI.
	/// </summary>
	public static CTooltip Objectives(Func<string, string> localise, CGameState state, CUnit unit, string element)
	{
		switch (element)
		{
			case "objectives":
				return new CTooltip
				{
					Text = unit.Plan.Active ? string.Join(", ", unit.Plan.Objectives.Select(objective => state.Province(objective.Province).Name)) : "",
					Delayed = localise("OBJECTIVES_DESC"),
				};
			case "aggression_checkbox":
				return new CTooltip { Text = localise(unit.HasOwnAggression ? "AGGRESSION_ON" : "AGGRESSION_OFF") };
			case "tac_aggro_slider_bg":
			case "tac_aggro_bg":
			case "aggro_TrackButton":
			case "aggro_SliderButton":
				long level = CUnitAggression.Of(unit);
				return new CTooltip
				{
					Text = CTextTemplate.Fill(localise("AGGRESSION_LEVEL"), ("STATUS",
						localise(level > 300 ? "AGGRESSION_AGGRESSIVE" : level < -300 ? "AGGRESSION_DEFENSIVE" : "AGGRESSION_NEUTRAL"))),
				};
			case "automate_checkbox":
				return new CTooltip
				{
					Text = localise(CAgents.HighestAutomated(unit) is not null ? "AUTOMATED" : Automate(state, unit)),
					Delayed = localise("AUTOMATED_DESC"),
				};
			case "reorg_hq_checkbox":
				return new CTooltip { Text = localise("ALLOW_HQ_REORG") };
			default:
				return CTooltip.Empty;
		}
	}

	/// <summary>
	/// Why a unit is not handed to an AI, by its level: a division never is, a theatre needs an
	/// area, a fleet or an air unit only goes with an army, and anything else is the player's.
	/// </summary>
	public static string Automate(CGameState state, CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Division => "CANT_AUTO_DIVISIONS",
			EUnitKind.Theatre when state.Country(unit.Owner) is not { } country || CAgents.TheatreHeadedBy(state, country, unit) is null => "CANT_AUTO_NO_AREA",
			EUnitKind.Navy or EUnitKind.Air => "ONLY_AUTO_ARMIES",
			_ => "PLAYER_CONTROLLED",
		};
	}
}
