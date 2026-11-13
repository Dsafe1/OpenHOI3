using System;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The theatre editor the unit panel's <c>edit_theatre</c> opens for a theatre headquarters: the map in its own colours,
/// and a drag box that paints the player's provinces into the headquarters' theatre, gathered again whenever the pointer has
/// moved more than two map units, a province leaving the box taken out again; what is under the box at the release is what
/// the theatre is given. See <c>docs/mechanics/attaching.md</c>, section 7, and
/// <c>reversing/findings/FINDINGS-attachbuttons.md</c>, section 9.
/// </summary>
public sealed class CTheatreEditor
{
	/// <summary>How far outside the box a province's label point may lie and still be in it, in map units.</summary>
	public const float Margin = 2f;

	/// <summary>The box is gathered again once the pointer has moved more than this many whole map units either way.</summary>
	public const int Resample = 2;

	private readonly List<int> _edit = new();

	/// <summary>
	/// What earlier releases painted in the theatre's colour: the original paints a province as it joins the box and repaints
	/// it only as it leaves the box while the drag lasts, so a release leaves the paint, and the map is coloured whole again
	/// only as the editor is opened.
	/// </summary>
	private readonly HashSet<int> _painted = new();
	private (int X, int Y)? _sample;

	public CTheatreEditor(CUnit headquarters)
	{
		Headquarters = headquarters;
	}

	/// <summary>The headquarters the editor was opened for.</summary>
	public CUnit Headquarters { get; }

	/// <summary>The provinces under the box now, in the order they came into it.</summary>
	public IReadOnlyList<int> Edit => _edit;

	/// <summary>The theatre being painted: the one whose headquarters heads the unit's chain.</summary>
	public static CTheatre? TheatreOf(CGameState state, CUnit unit) => CTheatres.FindFor(state, unit);

	/// <summary>Whether a selection may keep the editor open: exactly one unit, a theatre headquarters.</summary>
	public static bool Keeps(IReadOnlyCollection<CUnit> selected)
	{
		if (selected.Count != 1)
		{
			return false;
		}
		foreach (CUnit unit in selected)
		{
			return unit.Kind == EUnitKind.Theatre && !unit.IsRemoved;
		}
		return false;
	}

	/// <summary>A new drag: nothing gathered, and the sample where it starts - ours, the original's start of it not read.</summary>
	public void Begin(float x, float y)
	{
		_edit.Clear();
		_sample = ((int)x, (int)y);
	}

	/// <summary>Whether the pointer has moved far enough since the last gathering to gather again, and if so takes the sample.</summary>
	public bool ShouldGather(float x, float y)
	{
		(int X, int Y) here = ((int)x, (int)y);
		if (_sample is { } last && Math.Abs(here.X - last.X) <= Resample && Math.Abs(here.Y - last.Y) <= Resample)
		{
			return false;
		}
		_sample = here;
		return true;
	}

	/// <summary>
	/// The box gathered: every province the player controls, not already in the theatre and in an area, whose label point -
	/// its bounding box's centre - lies inside the box grown by two on every side, joined in the order of the provinces;
	/// one that has left the box taken out. The corners are in the positions file's frame and the map wraps east to west.
	/// </summary>
	public void Gather(CGameState state, CCountryTag player, CTheatre theatre, float x1, float y1, float x2, float y2, int mapWidth)
	{
		float left = MathF.Min(x1, x2) - Margin;
		float right = MathF.Max(x1, x2) + Margin;
		float bottom = MathF.Min(y1, y2) - Margin;
		float top = MathF.Max(y1, y2) + Margin;
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var inside = new HashSet<int>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0 || province.Controller != player || province.TheatreId == theatre.Id
				|| network.AreaOf(province.Id) is null)
			{
				continue;
			}
			float y = province.CentreY;
			if (y < bottom || y > top || !Within(province.CentreX, left, right, mapWidth))
			{
				continue;
			}
			inside.Add(province.Id);
		}
		_edit.RemoveAll(id => !inside.Contains(id));
		foreach (CProvince province in state.Provinces)
		{
			if (inside.Contains(province.Id) && !_edit.Contains(province.Id))
			{
				_edit.Add(province.Id);
			}
		}
	}

	/// <summary>What the release gives the theatre, the list then emptied; nothing where nothing was gathered.</summary>
	public int[] Release()
	{
		int[] taken = _edit.ToArray();
		_painted.UnionWith(_edit);
		_edit.Clear();
		_sample = null;
		return taken;
	}

	/// <summary>
	/// A province's colour in the editor: grey - 0.5, or 0.25 where the player knows it below 2 - and each of the player's
	/// theatres, the k-th of its list, in the map's k-th region's colour at full or half brightness by the same test, or the
	/// plain grey where there is no k-th region; the box's provinces in the painted theatre's colour.
	/// </summary>
	public CColor Colour(CGameState state, CCountryTag player, CProvince province, IReadOnlyList<CColor> regionColours)
	{
		float b = CVictoryPointMapColours.Brightness(state, province, player);
		var grey = new CColor(0.5f * b, 0.5f * b, 0.5f * b);
		if (state.Country(player) is not { } country)
		{
			return grey;
		}
		CTheatre? painted = TheatreOf(state, Headquarters);
		int theatreIndex = -1;
		int index = 0;
		foreach (CTheatre theatre in country.Strategy.Theatres)
		{
			if (ReferenceEquals(theatre, painted))
			{
				theatreIndex = index;
			}
			index++;
		}
		int k = _edit.Contains(province.Id) || _painted.Contains(province.Id) ? theatreIndex : IndexOf(country, province);
		if (k < 0)
		{
			return grey;
		}
		return k < regionColours.Count ? CSimplifiedTerrainColours.Scaled(regionColours[k], b) : grey;
	}

	/// <summary>The place in the player's list of the last of its theatres to hold the province, or -1.</summary>
	private static int IndexOf(CCountry country, CProvince province)
	{
		int found = -1;
		int index = 0;
		foreach (CTheatre theatre in country.Strategy.Theatres)
		{
			if (theatre.AllProvinces.Contains(province.Id))
			{
				found = index;
			}
			index++;
		}
		return found;
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
