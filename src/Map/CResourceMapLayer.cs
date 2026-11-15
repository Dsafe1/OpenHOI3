using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;

namespace OpenHOI3.Map;

/// <summary>
/// The resources map mode's icons: each good a province can yield on its building's point and its strategic resource on its
/// label point. Made once, as the layer is set up - the original makes them as a game is entered and never again - and drawn
/// only in the mode and under a camera height of 500. See <c>docs/mechanics/mapmodes.md</c>, section 5.
/// </summary>
public partial class CResourceMapLayer : Node2D
{
	private CMap _map = null!;
	private CMapPictures _pictures = null!;
	private List<CResourceMapIcon> _icons = new();
	private Rect2 _view;

	public void Setup(CMap map, CGameState state, CGuiContext gui, CBillboardTypes billboards)
	{
		_map = map;
		_pictures = new CMapPictures(gui, billboards);
		_icons = CResourceMapIcons.Build(state, map.BuildingPositions);
		Material = CBillboardMaterial.Make(gui, billboards);
		TextureFilter = TextureFilterEnum.LinearWithMipmaps;
		Visible = false;
	}

	public void ViewChanged(Rect2 view)
	{
		_view = view;
		QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (CResourceMapIcon icon in _icons)
		{
			// A world point, drawn half a pixel east and south of it as a building's picture is (mapdraw.md, section 3).
			_pictures.Draw(this, icon.Picture, CWorldPoint.ToMap(icon.X, icon.Y, _map.Height).ToGodot(), 1f, 0, _view,
				_map.Width);
		}
	}
}
