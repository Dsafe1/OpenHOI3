using System;
using System.Collections.Generic;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// What a drag box takes: the player's land units standing on land, then its fleets, then its air units, each where it
/// may join the selection and its point lies in the box grown by two map units on every side. So the land units win
/// over the fleets and the fleets over the air units. See <c>docs/mechanics/selection.md</c>, section 2.2.
/// </summary>
public static class CBoxSelection
{
	/// <summary>How far outside the box a unit's point may lie and still be in it, in map units.</summary>
	public const float Margin = 2f;

	/// <summary>How far the pointer's point of the map must move from the press for its release to be a drag.</summary>
	public const float DragDistance = 2f;

	/// <summary>Whether a press and a release this far apart on the map make a drag: 2.0 map units or more.</summary>
	public static bool IsDrag(float fromX, float fromY, float toX, float toY)
	{
		float x = toX - fromX;
		float y = toY - fromY;
		return MathF.Sqrt(x * x + y * y) >= DragDistance;
	}

	/// <summary>
	/// Adds what the box takes to the selection, and answers how many it took. The corners are in the positions file's
	/// frame - x east, y north - and the map is <paramref name="mapWidth"/> wide and wraps east to west.
	/// </summary>
	public static int Take(CGameState state, CCountry player, IReadOnlyList<CMapPosition> unitPoints,
		IReadOnlyList<CBuildingPosition[]> placed, float x1, float y1, float x2, float y2, int mapWidth,
		CSelection selection, IReadOnlyList<CMapPosition>? labelPoints = null)
	{
		float left = MathF.Min(x1, x2) - Margin;
		float right = MathF.Max(x1, x2) + Margin;
		float bottom = MathF.Min(y1, y2) - Margin;
		float top = MathF.Max(y1, y2) + Margin;
		int taken = 0;
		foreach (ESelectionKind kind in new[] { ESelectionKind.Land, ESelectionKind.Fleet, ESelectionKind.Air })
		{
			foreach (CUnit unit in player.AllUnits())
			{
				if (!IsWalked(state, unit, kind) || !selection.MayAdd(unit, player.Tag))
				{
					continue;
				}
				(float x, float y) = PointOf(state, unit, unitPoints, placed, labelPoints);
				if (y < bottom || y > top || !Within(x, left, right, mapWidth))
				{
					continue;
				}
				selection.Add(unit, player.Tag);
				taken++;
			}
		}
		return taken;
	}

	/// <summary>
	/// A unit's point on the map: its province's unit point - for a fleet in port the province's naval base, and for an
	/// air unit at its base the province's air base, where the building's picture stands - with no point for it in the
	/// positions file, the province's label point, which the unit points stand for where none is given.
	/// </summary>
	public static (float X, float Y) PointOf(CGameState state, CUnit unit, IReadOnlyList<CMapPosition> unitPoints,
		IReadOnlyList<CBuildingPosition[]> placed, IReadOnlyList<CMapPosition>? labelPoints = null)
	{
		labelPoints ??= unitPoints;
		int province = unit.Location;
		if (CMapBuildings.IsInPort(state, unit))
		{
			return CMapBuildings.PlaceOfRole(state, placed, labelPoints, province, CMovementRules.NavalBase);
		}
		if (CMapBuildings.IsAtAirfield(unit))
		{
			return CMapBuildings.PlaceOfRole(state, placed, labelPoints, province, CMovementRules.AirBase);
		}
		CMapPosition point = unitPoints[province];
		return (point.X, point.Y);
	}

	/// <summary>Which of the three walks a unit is in: a land unit standing in a land province, a fleet, an air unit.</summary>
	private static bool IsWalked(CGameState state, CUnit unit, ESelectionKind kind)
	{
		if (unit.IsRemoved || unit.Location <= 0 || unit.Location >= state.Provinces.Count
			|| CSelection.KindOf(unit) != kind)
		{
			return false;
		}
		return kind != ESelectionKind.Land || !state.Province(unit.Location).IsSea;
	}

	/// <summary>Ours: the map is a cylinder, so a point is in the box where it is on any copy of the map.</summary>
	private static bool Within(float x, float left, float right, int mapWidth)
	{
		for (int copy = -1; copy <= 1; copy++)
		{
			float shifted = x + copy * mapWidth;
			if (shifted >= left && shifted <= right)
			{
				return true;
			}
		}
		return false;
	}
}
