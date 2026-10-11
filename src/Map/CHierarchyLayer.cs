using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Map;

/// <summary>
/// The hierarchy of a selected unit, drawn on the map under the counters: a band from
/// each unit to the unit that commands it, in the original's own picture, with its
/// arrowheads running from the commander down to the unit. The original draws it while
/// its <c>render_hierarchy</c> setting is on, which the H key switches. See
/// <c>docs/mechanics/mapunits.md</c>, section 8.
/// </summary>
public partial class CHierarchyLayer : Node2D
{
	/// <summary>The picture: arrowheads in its left half, a broken line in its right.</summary>
	private const string TextureFile = "gfx/mapitems/hierarchy_line.tga";

	private CMap _map = null!;
	private CGameState _state = null!;
	private Texture2D? _texture;
	private CUnit? _unit;
	private bool _shown = true;
	private double _phase;
	private readonly Vector2[] _quad = new Vector2[4];
	private readonly Vector2[] _picture = new Vector2[4];
	private readonly Color[] _colours = new Color[4];

	public void Setup(CMap map, CGameState state, CGuiContext gui)
	{
		_map = map;
		_state = state;
		if (gui.Textures.Exists(TextureFile))
		{
			_texture = gui.Textures.LoadFromGamePath(TextureFile);
		}
		// The picture is repeated along a line, and drawn under whatever its parent draws.
		TextureRepeat = TextureRepeatEnum.Enabled;
		TextureFilter = TextureFilterEnum.Linear;
		ShowBehindParent = true;
	}

	/// <summary>Whether the lines are drawn at all: the original's setting. On when a game starts.</summary>
	public bool Shown
	{
		get => _shown;
		set
		{
			_shown = value;
			QueueRedraw();
		}
	}

	/// <summary>The unit whose hierarchy is drawn, or null.</summary>
	public CUnit? Unit
	{
		get => _unit;
		set
		{
			_unit = value;
			QueueRedraw();
		}
	}

	public override void _Process(double delta)
	{
		if (_unit is null || !_shown || _texture is null)
		{
			return;
		}
		_phase += delta * CHierarchyLines.PhaseASecond;
		if (_phase > CHierarchyLines.PhaseEnd)
		{
			_phase -= CHierarchyLines.PhaseEnd;
		}
		QueueRedraw();                                       // the arrowheads run
	}

	public override void _Draw()
	{
		if (_unit is null || !_shown || _texture is null)
		{
			return;
		}
		List<CHierarchyLine> lines = CHierarchyLines.Build(_unit, PointOf, _map.Width,
			unit => CCommandReach.IsOutOfRange(_state, unit));
		Span<System.Numerics.Vector2> corners = stackalloc System.Numerics.Vector2[4];
		float slide = (float)_phase * CHierarchyLines.SlideAPhase;
		foreach (CHierarchyLine line in lines)
		{
			if (!CHierarchyLines.Corners(line, corners, out float pictures))
			{
				continue;
			}
			// The left half of the picture runs; the right half, for a broken line, stands.
			float left = line.IsBroken ? 0.5f : 0f;
			float start = line.IsBroken ? 0f : slide;
			var colour = line.Colour.ToGodot();
			for (int index = 0; index < 4; index++)
			{
				_colours[index] = colour;
			}
			_picture[0] = new Vector2(left, start);
			_picture[1] = new Vector2(left + 0.5f, start);
			_picture[2] = new Vector2(left + 0.5f, start + pictures);
			_picture[3] = new Vector2(left, start + pictures);
			for (int copy = -1; copy <= 1; copy++)
			{
				var shift = new Vector2(copy * _map.Width, 0f);
				_quad[0] = new Vector2(corners[0].X, corners[0].Y) + shift;
				_quad[1] = new Vector2(corners[1].X, corners[1].Y) + shift;
				_quad[2] = new Vector2(corners[3].X, corners[3].Y) + shift;
				_quad[3] = new Vector2(corners[2].X, corners[2].Y) + shift;
				DrawPrimitive(_quad, _colours, _picture, _texture);
			}
		}
	}

	private System.Numerics.Vector2 PointOf(int province)
	{
		CMapPosition position = _map.Positions[province];
		return new System.Numerics.Vector2(position.X + 0.5f, _map.DisplayRow(position.Y) + 0.5f);
	}
}
