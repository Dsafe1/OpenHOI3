using Godot;

namespace OpenHOI3.Map;

/// <summary>
/// The drag box while the left button is held: a white outline two pixels wide and no fill, between the press's point
/// of the map and the pointer's, drawn over the counters. See <c>docs/mechanics/selection.md</c>, section 2.2.
/// </summary>
public partial class CSelectionBox : Node2D
{
	/// <summary>The outline's width, in pixels of the screen.</summary>
	private const float Width = 2f;

	private Vector2 _from;
	private Vector2 _to;
	private float _zoom = 1f;

	/// <summary>Shows the box between two points of the map, at the map's present zoom.</summary>
	public void ShowBox(Vector2 from, Vector2 to, float zoom)
	{
		_from = from;
		_to = to;
		_zoom = zoom;
		Visible = true;
		QueueRedraw();
	}

	public void HideBox()
	{
		Visible = false;
	}

	public override void _Draw()
	{
		var points = new[] { _from, new Vector2(_to.X, _from.Y), _to, new Vector2(_from.X, _to.Y), _from };
		DrawPolyline(points, Colors.White, Width / _zoom);
	}
}
