using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content.Map;
using OpenHOI3.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;

namespace OpenHOI3.Map;

/// <summary>
/// The objectives of a selected headquarters under an AI and of the player's other plans: <c>icon_objective_strip</c> at each and the
/// province's name beside it. See <c>docs/mechanics/mapunits.md</c>, section 12.
/// </summary>
public partial class CPlanMarkerLayer : Node2D
{
	private const string FontName = "MaptextFont";

	/// <summary>The name's offset from the picture and the height it is drawn at [mod: core.gfx], the original's three-times scale.</summary>
	private static readonly Vector2 TextOffset = new(2f, -1f);
	private const float TextScale = 3f;
	private const int FontSize = 32;

	private CMap _map = null!;
	private CGameState _state = null!;
	private CMapPictures _pictures = null!;
	private CGuiFonts _fonts = null!;
	private List<(int Province, bool Own)> _objectives = new();

	public void Setup(CMap map, CGameState state, CGuiContext gui, CBillboardTypes billboards)
	{
		_map = map;
		_state = state;
		_fonts = gui.Fonts;
		_pictures = new CMapPictures(gui, billboards);
	}

	public void SetObjectives(List<(int Province, bool Own)> objectives)
	{
		if (!objectives.SequenceEqual(_objectives))
		{
			_objectives = objectives;
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		var everywhere = new Rect2(-_map.Width, -_map.Height, 3f * _map.Width, 3f * _map.Height);
		Font? font = _fonts.Find(FontName)?.Font;
		foreach ((int province, bool own) in _objectives)
		{
			CMapPosition position = _map.Positions[province];
			var place = new Vector2(position.X + 0.5f, _map.DisplayRow(position.Y) + 0.5f);
			_pictures.Draw(this, CPlanMarkers.Picture, place, 1f, own ? 0 : 1, everywhere, _map.Width);
			if (font is not null)
			{
				float scale = TextScale / FontSize;
				DrawSetTransform(place + TextOffset, 0f, new Vector2(scale, scale));
				DrawString(font, Vector2.Zero, _state.Province(province).Name, HorizontalAlignment.Left, -1, FontSize, own ? Colors.White : new Color(0.8f, 0.8f, 0.8f));
				DrawSetTransform(Vector2.Zero);
			}
		}
	}
}
