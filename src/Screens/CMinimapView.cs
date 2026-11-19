using System;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Map;

namespace OpenHOI3.Screens;

/// <summary>
/// The minimap at the lower right of the in-game screen, as the original makes it out of <c>menubar.gui</c>'s three top-level
/// icons: <c>minimap_bg</c>, the picture <c>minimap_pic</c> - a fixed picture of the whole map that no map mode, date or
/// owner changes - and <c>minimap_overlay</c>, the day and night slid along it by the hour; over them the camera's view as
/// a white frame a pixel wide; and a left press inside the picture taking the camera there at once, its height left
/// alone. No drag, no tooltip, no way to shut it; hidden while a country screen is up. See
/// <c>docs/mechanics/minimap.md</c>.
/// </summary>
public partial class CMinimapView : Control
{
	private const string Background = "minimap_bg";
	private const string Picture = "minimap_pic";
	private const string Overlay = "minimap_overlay";

	private CGuiContext _gui = null!;
	private CMapView _map = null!;
	private CGuiIcon? _picture;
	private CGuiIcon? _overlay;
	private CMinimapFrameLine _frame = null!;
	private int _hour = -1;

	/// <summary>Has to be called before the node enters the tree.</summary>
	public void Setup(CGuiContext gui, CMapView map)
	{
		_gui = gui;
		_map = map;
	}

	public override void _Ready()
	{
		Name = "Minimap";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		// The background reaches up behind the lower part of the map modes' buttons on the bar below it (menubar.gui), which
		// the original, drawing them over it, lets take the press; so it lets the mouse through, and the bar's shadow under it
		// keeps a press on the frame off the map.
		if (Icon(Background) is { } background)
		{
			background.MouseFilter = MouseFilterEnum.Ignore;
		}
		_picture = Icon(Picture);
		_overlay = Icon(Overlay);
		_frame = new CMinimapFrameLine { Name = "Frame" };
		AddChild(_frame);
		if (_picture is not null)
		{
			// The picture takes the press; what lies over it lets the mouse through.
			_picture.GuiInput += PictureInput;
		}
		_map.CameraMoved += UpdateFrame;
		Resized += UpdateFrame;
		UpdateFrame();
	}

	public override void _ExitTree()
	{
		_map.CameraMoved -= UpdateFrame;
	}

	/// <summary>The picture's upper left corner on the screen - where a province's tooltip stands - or null where there is none.</summary>
	public Vector2? Corner => _picture?.GetGlobalRect().Position;

	/// <summary>The hour of the day, which slides the overlay: set every hour, as the original sets it.</summary>
	public void SetHour(int hour)
	{
		if (hour == _hour)
		{
			return;
		}
		_hour = hour;
		_overlay?.SetFrame(hour);
	}

	private CGuiIcon? Icon(string name)
	{
		if (_gui.Types.Find(name) is not CGuiIconType type)
		{
			Content.CDiagnostics.Problem($"interface: no file declares the minimap's icon '{name}'");
			return null;
		}
		var icon = new CGuiIcon();
		icon.Initialise(type, _gui);
		AddChild(icon);
		return icon;
	}

	/// <summary>
	/// The frame worked out again from the camera - its place, its height and the picture's place - as the original does
	/// whenever one of them changes.
	/// </summary>
	private void UpdateFrame()
	{
		if (_picture is null || !IsInsideTree())
		{
			return;
		}
		System.Numerics.Vector2 camera = _map.CameraOnMap;
		System.Numerics.Vector2 mapSize = _map.MapSize;
		Rect2 rect = _picture.GetGlobalRect();
		System.Numerics.Vector2[] corners = CMinimapFrame.Corners(camera.X, camera.Y, _map.CameraHeight, mapSize.X, mapSize.Y,
			new System.Numerics.Vector2(rect.Position.X, rect.Position.Y), new System.Numerics.Vector2(rect.Size.X, rect.Size.Y));
		var points = new Vector2[corners.Length];
		Vector2 origin = GetGlobalRect().Position;
		for (int index = 0; index < corners.Length; index++)
		{
			points[index] = new Vector2(corners[index].X, corners[index].Y) - origin;
		}
		_frame.SetPoints(points);
	}

	/// <summary>A left press strictly inside the picture takes the camera there; nothing else does anything.</summary>
	private void PictureInput(InputEvent input)
	{
		if (input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } press || _picture is null)
		{
			return;
		}
		Rect2 rect = _picture.GetGlobalRect();
		System.Numerics.Vector2 mapSize = _map.MapSize;
		Vector2 mouse = _picture.GetGlobalTransform() * press.Position;
		if (CMinimapFrame.Press(new System.Numerics.Vector2(mouse.X, mouse.Y),
			new System.Numerics.Vector2(rect.Position.X, rect.Position.Y), new System.Numerics.Vector2(rect.Size.X, rect.Size.Y),
			mapSize.X, mapSize.Y, out float x, out float y))
		{
			_map.JumpTo(x, y);
		}
		_picture.AcceptEvent();
	}
}
