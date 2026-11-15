using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Map;

/// <summary>
/// What the naval map mode draws over the map: the naval bases as large pictures sized by their level, and the four icons of
/// the past week's naval battles about a province's point. Shown only while the mode is on, and made again when it is chosen
/// and each day. See <c>docs/mechanics/mapmodes.md</c>, section 11.
/// </summary>
public partial class CNavalMapLayer : Node2D
{
	private readonly record struct Large(float X, float Y, float Size, int Frame);

	private CMap _map = null!;
	private CGameState _state = null!;
	private CMapPictures _pictures = null!;
	private readonly List<Large> _large = new();
	private CNavalMapFlags[] _flags = Array.Empty<CNavalMapFlags>();
	private Rect2 _view;

	/// <summary>The country being played, whose battles the icons are of.</summary>
	public CCountryTag? Player { get; set; }

	public void Setup(CMap map, CGameState state, CGuiContext gui, CBillboardTypes billboards)
	{
		_map = map;
		_state = state;
		_pictures = new CMapPictures(gui, billboards);
		Material = CBillboardMaterial.Make(gui, billboards);
		TextureFilter = TextureFilterEnum.LinearWithMipmaps;
		Visible = false;
	}

	public void Refresh()
	{
		_flags = CNavalMapIcons.Flags(_state, Player);
		_large.Clear();
		var ports = new HashSet<int>();
		foreach (CCountry country in _state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (CMapBuildings.IsInPort(_state, unit))
				{
					ports.Add(unit.Location);
				}
			}
		}
		for (int id = 1; id < _state.Provinces.Count && id < _map.ProvinceCount; id++)
		{
			CProvince province = _state.Provinces[id];
			int level = CAirMapIcons.Level(_state, province, CMovementRules.NavalBase, Player);
			if (level < 1)
			{
				continue;
			}
			(float x, float y) = CMapBuildings.PlaceOf(_map.BuildingPositions, _map.LabelPositions, id, CMovementRules.NavalBase);
			_large.Add(new Large(x, y, CAirMapIcons.Size(level), ports.Contains(id) ? 1 : 0));
		}
		QueueRedraw();
	}

	public void ViewChanged(Rect2 view)
	{
		_view = view;
		QueueRedraw();
	}

	/// <summary>
	/// A point of positions.txt, a world point counting y north, in this node's coordinates: half a pixel east and south of
	/// it (mapdraw.md, section 3).
	/// </summary>
	private Vector2 PlaceOf(float x, float y) => CWorldPoint.ToMap(x, y, _map.Height).ToGodot();

	public override void _Draw()
	{
		foreach (Large large in _large)
		{
			_pictures.Draw(this, CNavalMapIcons.NavalBasePicture, PlaceOf(large.X, large.Y), large.Size, large.Frame, _view,
				_map.Width);
		}
		float step = CAirMapIcons.IconOffset;
		for (int id = 1; id < _flags.Length && id < _map.Positions.Count; id++)
		{
			CNavalMapFlags flags = _flags[id];
			Vector2 point = PlaceOf(_map.Positions[id].X, _map.Positions[id].Y);
			// East, west, north and south of the point, as the original's vectors are read.
			if (flags.ConvoysRaided)
			{
				Icon(CNavalMapIcons.ConvoysRaidedPicture, point + new Vector2(step, 0f));
			}
			if (flags.ConvoysSunk)
			{
				Icon(CNavalMapIcons.ConvoysSunkPicture, point + new Vector2(-step, 0f));
			}
			if (flags.RaiderDefended)
			{
				Icon(CNavalMapIcons.RaiderDefendedPicture, point + new Vector2(0f, -step));
			}
			if (flags.RaiderAttacked)
			{
				Icon(CNavalMapIcons.RaiderAttackedPicture, point + new Vector2(0f, step));
			}
		}
	}

	private void Icon(string name, Vector2 place) => _pictures.Draw(this, name, place, 1f, 0, _view, _map.Width);
}
