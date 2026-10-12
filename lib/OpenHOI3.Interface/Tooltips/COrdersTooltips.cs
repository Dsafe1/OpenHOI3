using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the window a right click with a fleet or an air unit brings up says in a tooltip: why
/// <c>accept</c> cannot be pressed, the stance and the priority, the air window's shapes, its
/// continuous box and its day readout, and each order's description - with, in the fleet's,
/// how far out of range it is. See <c>docs/mechanics/fleetsandair.md</c>, section 8.
/// </summary>
public static class COrdersTooltips
{
	/// <summary>
	/// What each row of an order nothing here can give calls itself: the original's
	/// descriptions of transport, invasion, the supply flight, the air's raid on convoys and the
	/// air reserve, by the row's name.
	/// </summary>
	private static readonly Dictionary<string, string> UnbuiltDescriptions = new(StringComparer.Ordinal)
	{
		["ORDER_TRANSPORT"] = "ORDER_TRANSPORT_DESC",
		["ORDER_INVADE"] = "ORDER_INVADE_DESC",
		["TRANS_SUP_MISSION"] = "TRANS_SUP_MISSION_DESC",
		["ORDER_AIR_CONVOY_RAID"] = "AIR_CONVOY_DESC",
		["ORDER_AIR_RESERVE"] = "ORDER_AIR_RESERVE_DESC",
	};

	private static readonly HashSet<string> Stance = new(StringComparer.Ordinal)
	{
		"stance_label", "stance_value", "decrease_stance_button", "increase_stance_button",
	};

	/// <summary>
	/// The priority's elements the original answers for. Its two buttons are not among them:
	/// it asks twice for a name no element has.
	/// </summary>
	private static readonly HashSet<string> Priority = new(StringComparer.Ordinal) { "priority_label", "priority_value" };

	private static readonly Dictionary<string, string> Shapes = new(StringComparer.Ordinal)
	{
		["point_target_button"] = "AirOrdersBtn_Point",
		["region_target_button"] = "AirOrdersBtn_Region",
		["circle_target_button"] = "AirOrdersBtn_Circle",
		["cone_target_button"] = "AirOrdersBtn_Conic",
	};

	private static readonly HashSet<string> DayTime = new(StringComparer.Ordinal)
	{
		"daytime_title", "daytime_icon", "daytime_icondesc",
	};

	/// <summary>
	/// The fleet's window, by the element's name. <paramref name="ticked"/> is the order being
	/// made, or <see cref="EUnitOrder.None"/> for none.
	/// </summary>
	public static CTooltip Naval(Func<string, string> localise, CGameState state, CUnit unit, CProvince province,
		bool acceptDisabled, EUnitOrder ticked, string element)
	{
		if (element == "accept")
		{
			if (!acceptDisabled)
			{
				return CTooltip.Empty;
			}
			if (ticked == EUnitOrder.None)
			{
				return Text(localise("OV_NO_VALID_ORDER"));
			}
			// A key with no text in either table; not reached, accept being on with a row ticked.
			return CUnitOrders.IsInRange(state, unit, ticked, province) ? CTooltip.Empty : Text(localise("OV_OUT_OF_RANGE"));
		}
		return Shared(localise, element);
	}

	/// <summary>
	/// The air window, by the element's name. <paramref name="startHour"/> is the hour its
	/// mission begins, which the day readout is for.
	/// </summary>
	public static CTooltip Air(Func<string, string> localise, CGameState state, CProvince province, bool acceptDisabled,
		bool hasOrder, bool hasProvinces, long startHour, string element)
	{
		if (DayTime.Contains(element))
		{
			bool night = CAirOrdersText.IsNightAtStart(state, province, startHour);
			return Text(CTextTemplate.Fill(localise("ORDER_DAY_TIME_DESC"),
				("PROV", province.Name), ("TIME", localise(night ? "ORDER_NIGHT" : "ORDER_DAY"))));
		}
		if (element == "accept")
		{
			if (!acceptDisabled)
			{
				return CTooltip.Empty;
			}
			return !hasOrder ? Text(localise("OV_NO_VALID_ORDER"))
				: !hasProvinces ? Text(localise("OV_NO_VALID_PROVINCES"))
				: CTooltip.Empty;
		}
		if (element == "continous_checkbox")
		{
			return Text(localise("OV_CONT"));
		}
		if (Shapes.TryGetValue(element, out string? shape))
		{
			return Text(localise(shape));
		}
		return Shared(localise, element);
	}

	private static CTooltip Shared(Func<string, string> localise, string element)
	{
		if (Stance.Contains(element))
		{
			return Text(localise("OV_STANCE"));
		}
		return Priority.Contains(element) ? Text(localise("OV_PRIO")) : CTooltip.Empty;
	}

	/// <summary>
	/// A row of the list: its order's description; in the fleet's window, where the province is
	/// out of the fleet's reach, after a moment how far it reaches and how far it would have to.
	/// A row of an order not built has no reach to ask (<i>ours</i>).
	/// </summary>
	public static CTooltip Row(Func<string, string> localise, CGameState state, CUnit unit, CProvince province, COrderRow row)
	{
		string key = row.Order != EUnitOrder.None
			? CUnitOrderTooltips.DescriptionKey(row.Order)
			: UnbuiltDescriptions.GetValueOrDefault(row.NameKey, "");
		string text = key.Length == 0 ? "" : localise(key);
		if (unit.Kind != EUnitKind.Navy || row.Order == EUnitOrder.None || CUnitOrders.IsInRange(state, unit, row.Order, province))
		{
			return Text(text);
		}
		return new CTooltip
		{
			Text = text,
			Delayed = CTextTemplate.Fill(localise("OUT_OF_RANGE"),
				("MYRANGE", CNumberText.Format(CUnitOrders.RangeOf(unit), 1)),
				("RANGE", CNumberText.Trimmed(CUnitOrders.DistanceFromBase(state, unit, province)))),
		};
	}

	private static CTooltip Text(string text)
	{
		return text.Length == 0 ? CTooltip.Empty : new CTooltip { Text = text };
	}
}
