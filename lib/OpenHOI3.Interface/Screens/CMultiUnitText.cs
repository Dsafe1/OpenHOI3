using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the multi-unit panel shows of several selected units: its line, its checkboxes, its rows' layout and what its
/// carrying buttons find. See <c>docs/mechanics/selection.md</c>, section 5.
/// </summary>
public static class CMultiUnitText
{
	/// <summary>The slider's own range, out of which the first row shown is worked out.</summary>
	public const int SliderMaximum = 100;

	/// <summary>"3 Land Units": the count and the kind of the first unit.</summary>
	public static string Desc(Func<string, string> localise, IReadOnlyList<CUnit> units)
	{
		if (units.Count == 0)
		{
			return "";
		}
		string key = CSelection.KindOf(units[0]) switch
		{
			ESelectionKind.Fleet => "NAVIES",
			ESelectionKind.Air => "AIRWINGS",
			_ => "ARMIES",
		};
		return units.Count + " " + localise(key);
	}

	public static bool HasFlag(CUnit unit, EUnitSupport what)
	{
		return what switch
		{
			EUnitSupport.Priority => unit.IsPrioritised,
			EUnitSupport.Reinforcement => unit.MayReinforce,
			_ => unit.MayUpgrade,
		};
	}

	/// <summary>How many of the units have the flag. A checkbox is ticked where any has it: there is no mixed state.</summary>
	public static int FlagCount(IReadOnlyList<CUnit> units, EUnitSupport what)
	{
		int count = 0;
		foreach (CUnit unit in units)
		{
			if (HasFlag(unit, what))
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>How many rows fit under the panel: <c>(height - start.y) / offset.y - 1</c>, in whole rows.</summary>
	public static int RowsThatFit(int screenHeight, int startY, int offsetY)
	{
		return offsetY <= 0 ? 0 : (screenHeight - startY) / offsetY - 1;
	}

	/// <summary>The first row shown, by the slider's value out of its 100 where there are more rows than fit.</summary>
	public static int FirstRow(int rows, int fit, float value)
	{
		if (rows <= fit)
		{
			return 0;
		}
		return (int)MathF.Truncate((rows - fit) * value / SliderMaximum);
	}

	/// <summary>The slider's value after the mouse wheel moved by some notches: one notch, one row.</summary>
	public static float Wheel(float value, int notches, int rows, int fit)
	{
		if (rows <= fit)
		{
			return value;
		}
		float moved = value - notches * (float)SliderMaximum / (rows - fit);
		return Math.Clamp(moved, 0f, SliderMaximum);
	}

	/// <summary>Whether the carrying buttons show: where the first unit is a land unit.</summary>
	public static bool CarryShown(IReadOnlyList<CUnit> units)
	{
		return units.Count >= 2 && CMovementRules.IsLandUnit(units[0]);
	}

	/// <summary>What the units weigh together.</summary>
	public static long Weight(IReadOnlyList<CUnit> units)
	{
		long weight = 0;
		foreach (CUnit unit in units)
		{
			weight += CTransport.Weight(unit);
		}
		return weight;
	}

	/// <summary>
	/// The fleet <c>load_multiunit</c> puts the selection aboard: with two or more selected, all land units in one land
	/// province with a naval base built and none in a battle - the search fails at the first that is not - the first
	/// fleet lying there whose passengers and the whole selection fit and which may carry the last selected unit.
	/// </summary>
	public static CUnit? FleetFor(CGameState state, IReadOnlyList<CUnit> units)
	{
		if (Province(state, units, CMovementRules.NavalBase) is not { } province)
		{
			return null;
		}
		long weight = Weight(units);
		CUnit last = units[^1];
		foreach (CUnit other in state.UnitsIn(province.Id))
		{
			if (other.Kind == EUnitKind.Navy && CTransport.Load(other) + weight <= CTransport.Capacity(other)
				&& CTransport.CanCarry(other, last))
			{
				return other;
			}
		}
		return null;
	}

	/// <summary>The air unit <c>load_on_air_multiunit</c> puts the selection aboard: the same, by the air base.</summary>
	public static CUnit? PlaneFor(CGameState state, IReadOnlyList<CUnit> units)
	{
		if (Province(state, units, CMovementRules.AirBase) is not { } province)
		{
			return null;
		}
		long weight = Weight(units);
		CUnit last = units[^1];
		foreach (CUnit other in state.UnitsIn(province.Id))
		{
			if (other.Kind == EUnitKind.Air && other.Base == province.Id && CAirMissions.IsAtOwnBase(other)
				&& CTransport.Load(other) + weight <= CTransport.Capacity(other) && CAirTransport.MayLoad(state, other, last))
			{
				return other;
			}
		}
		return null;
	}

	/// <summary>How many of the units are aboard a plane or a ship: what <c>unmount_multiunit</c> takes off.</summary>
	public static int Aboard(IReadOnlyList<CUnit> units)
	{
		int count = 0;
		foreach (CUnit unit in units)
		{
			if (CMovementRules.IsLandUnit(unit) && (unit.Plane is not null || unit.Transport is not null))
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>
	/// The one province every unit stands in, walking them in order: null at the first that is not a land unit, stands
	/// elsewhere than the one before, stands at sea, is in a battle, or stands where the building is not built.
	/// </summary>
	private static CProvince? Province(CGameState state, IReadOnlyList<CUnit> units, string building)
	{
		if (units.Count < 2)
		{
			return null;
		}
		CProvince? province = null;
		foreach (CUnit unit in units)
		{
			if (!CMovementRules.IsLandUnit(unit) || unit.Location <= 0 || unit.Location >= state.Provinces.Count
				|| (province is not null && unit.Location != province.Id))
			{
				return null;
			}
			province = state.Province(unit.Location);
			if (province.IsSea || unit.IsFighting
				|| CMovementRules.BuildingLevel(state, province, building) <= CFixedPoint.Zero)
			{
				return null;
			}
		}
		return province;
	}
}
