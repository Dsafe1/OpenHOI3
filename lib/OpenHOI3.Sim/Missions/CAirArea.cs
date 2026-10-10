using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// The provinces an air mission is being given, as the air orders window makes them: a
/// shape, a range, an angle and a width, a province a right click aimed it at, and the
/// list they come to. It is the original's order as the window holds it, not part of the
/// game: what reaches the game is the list, in the command accept sends. See
/// <c>docs/mechanics/aircombat.md</c>, section 2.
///
/// The angle and the width are kept in radians in single precision, and the bearing and
/// the arc are worked out so, because that is how the original decides which provinces
/// fall inside a cone; nothing here is game state.
/// </summary>
public sealed class CAirArea
{
	/// <summary>What the original adds to the angle of the line between two provinces: three quarters of a turn.</summary>
	private const double QuarterTurns = 4.7123891115188599;

	private const double Turn = 6.2831854820251465;

	private readonly CGameState _state;
	private readonly CCountry _country;
	private readonly CUnit _unit;
	private readonly List<int> _provinces = new();

	public CAirArea(CGameState state, CCountry country, CUnit unit, EUnitOrder order, int province,
		EAirAreaShape shape = EAirAreaShape.Point)
	{
		_state = state;
		_country = country;
		_unit = unit;
		Order = order;
		Province = province;
		Shape = shape;
	}

	public EUnitOrder Order { get; }

	/// <summary>The province the window is for, which every shape starts from.</summary>
	public int Province { get; }

	public EAirAreaShape Shape { get; private set; }

	/// <summary>How far from the province a circle or a cone reaches, in the units of a unit's range.</summary>
	public CFixedPoint Range { get; private set; }

	/// <summary>Which way a cone points: a bearing, clockwise from north, in radians.</summary>
	public float Angle { get; private set; }

	/// <summary>How wide a cone is, in radians: half of it either side of its angle.</summary>
	public float Width { get; private set; }

	/// <summary>The province a right click aimed the area at, or 0.</summary>
	public int SecondTarget { get; private set; }

	/// <summary>The mission's provinces, in the order they were found.</summary>
	public IReadOnlyList<int> Provinces => _provinces;

	/// <summary>A shape button: the list is made again for the shape, with no second target.</summary>
	public void SetShape(EAirAreaShape shape)
	{
		Shape = shape;
		SetSecondTarget(0, false);
	}

	/// <summary>The range slider: a circle or a cone is filled again.</summary>
	public void SetRange(CFixedPoint range)
	{
		Range = range;
		if (Shape is EAirAreaShape.Circle or EAirAreaShape.Cone)
		{
			Refill();
		}
	}

	/// <summary>The angle slider: a cone is filled again.</summary>
	public void SetAngle(float angle)
	{
		Angle = angle;
		if (Shape == EAirAreaShape.Cone)
		{
			Refill();
		}
	}

	/// <summary>The width slider: a cone is filled again.</summary>
	public void SetWidth(float width)
	{
		Width = width;
		if (Shape == EAirAreaShape.Cone)
		{
			Refill();
		}
	}

	/// <summary>
	/// The original's second target. With <paramref name="toggle"/> - shift - a province is
	/// taken off the list or added to it, and one out of range only ever taken off.
	/// Without, the list is made again: given a province, a circle's or a cone's range
	/// becomes the distance to it, no further than the unit can fly, and a cone's angle the
	/// bearing to it. The window's own province does nothing either way.
	/// </summary>
	public void SetSecondTarget(int province, bool toggle)
	{
		if (province == Province)
		{
			return;
		}
		if (toggle)
		{
			if (!IsInRange(province) || _provinces.Contains(province))
			{
				_provinces.Remove(province);
			}
			else
			{
				_provinces.Add(province);
			}
			return;
		}

		_provinces.Clear();
		SecondTarget = province;
		if (province > 0 && province < _state.Provinces.Count)
		{
			CProvince centre = _state.Province(Province);
			CProvince target = _state.Province(province);
			CFixedPoint distance = CFixedPoint.FromInt(CMapDistance.Between(centre, target, _state.MapWidth));
			CFixedPoint reach = CUnitOrders.RangeOf(_unit);
			Range = distance > reach ? reach : distance;
			Angle = Bearing(centre, target);
		}
		switch (Shape)
		{
			case EAirAreaShape.Circle:
			case EAirAreaShape.Cone:
				Fill();
				break;
			case EAirAreaShape.Point:
				if (IsInRange(Province))
				{
					_provinces.Add(Province);
				}
				break;
			case EAirAreaShape.Region:
				if (FirstRegion(Province) is { } region)
				{
					foreach (int id in region.Provinces)
					{
						if (IsInRange(id))
						{
							_provinces.Add(id);
						}
					}
				}
				else if (IsInRange(Province))
				{
					_provinces.Add(Province);
				}
				break;
		}
	}

	/// <summary>
	/// Adds a province not already on the list, where the order may list it - the original's
	/// slot 36, which shift with a right click calls for a point.
	/// </summary>
	public void Add(int province)
	{
		if (province > 0 && province < _state.Provinces.Count && !_provinces.Contains(province)
			&& CAirMissions.MayList(_state, _unit, Order, _state.Province(province)))
		{
			_provinces.Add(province);
		}
	}

	/// <summary>Shift with a right click for a region: every province of the province's first region, or itself.</summary>
	public void AddRegionOf(int province)
	{
		if (FirstRegion(province) is { } region)
		{
			foreach (int id in region.Provinces)
			{
				Add(id);
			}
			return;
		}
		Add(province);
	}

	/// <summary>The first region that lists a province, or null.</summary>
	public CRegion? FirstRegion(int province)
	{
		if (province <= 0 || province >= _state.Provinces.Count)
		{
			return null;
		}
		List<int> regions = _state.Province(province).Regions;
		return regions.Count == 0 ? null : _state.Regions[regions[0]];
	}

	/// <summary>Within the unit's range of its base: the original's slot 38.</summary>
	public bool IsInRange(int province)
	{
		return province > 0 && province < _state.Provinces.Count
			&& CUnitOrders.IsInRange(_state, _unit, Order, _state.Province(province));
	}

	/// <summary>
	/// The compass bearing of one province from another, between their points: north 0,
	/// east a quarter turn, clockwise, in single precision. The original adds three
	/// quarters of a turn to the angle of the line, so it runs from a quarter turn to one
	/// and a quarter. x does not wrap round the map.
	/// </summary>
	public static float Bearing(CProvince from, CProvince to)
	{
		float up = (float)to.CentreY - from.CentreY;
		float across = (float)from.CentreX - to.CentreX;
		float angle = (float)Math.Atan2(up, across);
		return (float)(angle + QuarterTurns);
	}

	/// <summary>
	/// Whether a bearing lies in an arc, as the original works it out: each end and the
	/// bearing taken modulo a turn, keeping the sign, so an arc whose start falls below
	/// north keeps no part of itself west of north.
	/// </summary>
	public static bool IsWithinArc(float bearing, float angle, float halfWidth)
	{
		float high = (float)((float)(angle + halfWidth) % Turn);
		float low = (float)((float)(angle - halfWidth) % Turn);
		float at = (float)(bearing % Turn);
		if (high > low)
		{
			return at >= low && at <= high;
		}
		return at >= low || at <= high;
	}

	private void Refill()
	{
		_provinces.Clear();
		Fill();
	}

	/// <summary>
	/// The original's fill: from the window's province, through neighbours the unit may fly
	/// to that are within its range of its base and within the area's range of the window's
	/// province - and, for a cone, within its arc - each added as it is reached where it is
	/// in range.
	/// </summary>
	private void Fill()
	{
		if (Province <= 0 || Province >= _state.Provinces.Count)
		{
			return;
		}
		var seen = new HashSet<int> { Province };
		Walk(_state.Province(Province), seen);
	}

	private void Walk(CProvince from, HashSet<int> seen)
	{
		if (IsInRange(from.Id))
		{
			_provinces.Add(from.Id);
		}
		CProvince centre = _state.Province(Province);
		float half = (float)(Width * 0.5);
		foreach (CProvinceEdge edge in from.Edges)
		{
			if (!seen.Add(edge.To))
			{
				continue;
			}
			CProvince next = _state.Province(edge.To);
			if (!CMovementRules.MayMoveTo(_state, _country, _unit, next) || !IsInRange(next.Id)
				|| CFixedPoint.FromInt(CMapDistance.Between(centre, next, _state.MapWidth)) > Range)
			{
				continue;
			}
			if (Shape == EAirAreaShape.Cone && !IsWithinArc(Bearing(centre, next), Angle, half))
			{
				continue;
			}
			Walk(next, seen);
		}
	}
}
