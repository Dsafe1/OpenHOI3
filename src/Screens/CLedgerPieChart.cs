using System;
using System.Collections.Generic;
using Godot;
using OpenHOI3.Gfx;
using OpenHOI3.Interface.Ledger;

namespace OpenHOI3.Screens;

/// <summary>
/// One pie of the Statistics screen's pie pages: its slices drawn clockwise from the top, each with a line out to its label. How the original turns a slice's share
/// into an angle was not read. See <c>docs/mechanics/statistics.md</c>, section 4.
/// </summary>
public sealed partial class CLedgerPieChart : Control
{
	private const int SegmentsPerTurn = 64;

	private IReadOnlyList<CLedgerSlice> _slices = Array.Empty<CLedgerSlice>();
	private float _total;

	public void Show(IReadOnlyList<CLedgerSlice> slices)
	{
		_slices = slices;
		_total = 0f;
		foreach (CLedgerSlice slice in slices)
		{
			_total += slice.Value;
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_total <= 0f)
		{
			return;
		}
		Vector2 centre = Size / 2f;
		float radius = Size.X / 2f;
		float start = -Mathf.Pi / 2f;
		foreach (CLedgerSlice slice in _slices)
		{
			float sweep = slice.Value / _total * Mathf.Tau;
			int segments = Math.Max(1, (int)(SegmentsPerTurn * sweep / Mathf.Tau));
			var points = new Vector2[segments + 2];
			points[0] = centre;
			for (int point = 0; point <= segments; point++)
			{
				float angle = start + sweep * point / segments;
				points[point + 1] = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
			}
			DrawColoredPolygon(points, slice.Colour.ToGodot());
			start += sweep;
		}
	}
}
