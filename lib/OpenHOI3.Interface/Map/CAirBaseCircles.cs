using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The rings of range the air map mode draws round the player's air bases. See
/// <c>docs/mechanics/airmap.md</c>, section 6:
///
///     for each air base of the player's with units based there, the units by range,
///     shortest first; a unit within 0.1 of the range of the group before joins it,
///     and the group takes its range; otherwise it starts a group
///     a ring for each group: its range / 4 in map pixels across from the base, its
///     strength over its fullest, and dimmed to 0.3 where another air unit is selected
///
/// and the colour and the see-through of a ring, as the original's <c>circle.fx</c> makes them.
/// </summary>
public static class CAirBaseCircles
{
	/// <summary>How near two ranges are to share a ring, in thousandths of a kilometre.</summary>
	public const long Nearness = 100;

	/// <summary>Map pixels a ring reaches for each kilometre of range.</summary>
	public const double PixelsPerKilometre = 0.25;

	/// <summary>What a ring is dimmed to while an air unit not of its group is selected.</summary>
	public const float Dimmed = 0.3f;

	/// <summary>How see-through a ring is at its brightest: its picture's alpha times this, times its dim.</summary>
	public const float Opacity = 0.3f;

	/// <summary>The original's picture of a quarter of a ring, its centre at its corner.</summary>
	public const string Picture = "map/Sector.dds";

	private sealed class Group
	{
		public long Range;
		public long Strength;
		public long Most;
		public bool Selected;
	}

	/// <summary>Every ring, base by base in the order of the provinces.</summary>
	public static List<CAirBaseCircle> Build(CGameState state, CMap map, CCountryTag? player, CUnit? selected)
	{
		return Build(state, map.BuildingPositions, map.LabelPositions, player, selected);
	}

	/// <summary>The same from the two things of the map it reads: where buildings stand, and each province's label point.</summary>
	public static List<CAirBaseCircle> Build(CGameState state, IReadOnlyList<CBuildingPosition[]> placed,
		IReadOnlyList<CMapPosition> labels, CCountryTag? player, CUnit? selected)
	{
		var circles = new List<CAirBaseCircle>();
		if (player is not { } us)
		{
			return circles;
		}
		bool airSelected = selected is { Kind: EUnitKind.Air };
		for (int id = 1; id < state.Provinces.Count && id < placed.Count && id < labels.Count; id++)
		{
			CProvince province = state.Provinces[id];
			if (province.Controller != us
				|| CMovementRules.BuildingLevel(state, province, CMovementRules.AirBase) <= CFixedPoint.Zero)
			{
				continue;
			}
			List<CUnit> based = CMapBuildings.UnitsOf(state, id, EMapBuilding.Airfield);
			if (based.Count == 0)
			{
				continue;
			}
			(float x, float y) = CMapBuildings.PlaceOf(placed, labels, id, CMovementRules.AirBase);
			foreach (Group group in Groups(based, selected))
			{
				float share = group.Most > 0
					? (float)(group.Strength / 1000.0) / (float)(group.Most / 1000.0)
					: 0f;
				float kilometres = (float)(group.Range / 1000.0);
				float radius = (float)(kilometres * PixelsPerKilometre);
				float dim = airSelected && !group.Selected ? Dimmed : 1f;
				circles.Add(new CAirBaseCircle(id, x, y, radius, share, dim));
			}
		}
		return circles;
	}

	private static List<Group> Groups(List<CUnit> based, CUnit? selected)
	{
		var groups = new List<Group>();
		// OrderBy keeps units of equal range in the order they were in.
		foreach (CUnit unit in based.OrderBy(unit => CUnitOrders.RangeOf(unit).Raw))
		{
			long range = CUnitOrders.RangeOf(unit).Raw;
			Group? last = groups.Count > 0 ? groups[^1] : null;
			if (last is null || Math.Abs(last.Range - range) > Nearness)
			{
				last = new Group();
				groups.Add(last);
			}
			last.Range = range;
			last.Strength += CUnitTotals.Strength(unit).Raw;
			last.Most += CUnitTotals.FullStrength(unit).Raw;
			last.Selected |= ReferenceEquals(unit, selected);
		}
		return groups;
	}

	/// <summary>
	/// A ring's colour at full: red with no strength, yellow at half, green at full - what
	/// <c>circle.fx</c> makes of its share - and its alpha before its picture's, 0.3 times its dim.
	/// </summary>
	public static CColor ColourOf(CAirBaseCircle circle)
	{
		// Red to yellow over the first half, then yellow to green.
		float toYellow = Math.Clamp(circle.Share * 2f, 0f, 1f);
		float toGreen = Math.Clamp((circle.Share - 0.5f) * 2f, 0f, 1f);
		float red = 1f - toGreen;
		float green = toYellow + (1f - toYellow) * toGreen;
		return new CColor(red, green, 0f, Opacity * circle.Dim);
	}
}
