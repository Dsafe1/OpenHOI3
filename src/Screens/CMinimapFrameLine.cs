using Godot;

namespace OpenHOI3.Screens;

/// <summary>
/// The minimap's frame: a white, opaque line a pixel wide through its five points, as the original's line object draws
/// it (<c>reversing/findings/FINDINGS-minimap.md</c>, section 2). It lets the mouse through.
/// </summary>
public partial class CMinimapFrameLine : Control
{
	private Vector2[] _points = System.Array.Empty<Vector2>();

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public void SetPoints(Vector2[] points)
	{
		_points = points;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_points.Length > 1)
		{
			DrawPolyline(_points, Colors.White, 1f);
		}
	}
}
