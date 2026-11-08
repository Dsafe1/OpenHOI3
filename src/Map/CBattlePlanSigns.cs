using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content.BattlePlans;
using OpenHOI3.Content.Map;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.BattlePlans;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim.Plans;

namespace OpenHOI3.Map;

/// <summary>
/// The words of battle plans, on the map's 2D layer in the outline typeface <c>allies_country</c> stands for, as the
/// project draws every text: each sign centred on its point, and each arrow's label bent along the arrow. See
/// <c>docs/mechanics/battleplans.md</c>, section 12.
/// </summary>
public partial class CBattlePlanSigns : Node2D
{
	private const float Pixel = 0.05f;

	/// <summary>A label starts at the arrow's sixth point.</summary>
	private const int SkippedPoints = 5;

	private CMap _map = null!;
	private CGuiFonts? _fonts;
	private CPlanObjectTypes _types = null!;
	private List<CPlanText> _texts = new();
	private List<CPlanStroke> _arrows = new();
	private Color _ink = Colors.Red;
	private double _time;

	public void Setup(CMap map, CGuiFonts fonts, CPlanObjectTypes types)
	{
		_map = map;
		_fonts = fonts;
		_types = types;
	}

	public void Show(List<CPlanText> texts, List<CPlanStroke> arrows, Color ink, double time)
	{
		_texts = texts;
		_arrows = arrows;
		_ink = ink;
		_time = time;
		QueueRedraw();
	}

	private float Alpha(bool selected) => selected ? 0.5f * ((float)Math.Sin(_time) + 1f) : 1f;

	public override void _Draw()
	{
		if (_fonts?.Find("allies_country") is not { Font: not null } font)
		{
			DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
			return;
		}
		for (int copy = -1; copy <= 1; copy++)
		{
			float shift = copy * _map.Width;
			foreach (CPlanText sign in _texts)
			{
				DrawSign(sign, font, shift);
			}
			foreach (CPlanStroke arrow in _arrows)
			{
				if (arrow.Label.Length > 0 && _types.Arrow(arrow.Type) is { } type)
				{
					DrawLabel(arrow, type, font, shift);
				}
			}
		}
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	private void DrawSign(CPlanText sign, CGuiFont font, float shift)
	{
		string shown = CWindows1252.ToDisplay(sign.Text);
		float k = Pixel * sign.Size;
		float width = font.Font!.GetStringSize(shown, HorizontalAlignment.Left, -1f, font.Size).X * k * font.Narrowing;
		float x = sign.X + sign.OriginX + shift, y = _map.Height - (sign.Y + sign.OriginY);
		var along = new Vector2(Mathf.Cos(-sign.Rotation), Mathf.Sin(-sign.Rotation));
		DrawSetTransform(new Vector2(x, y) - along * (width / 2f), -sign.Rotation, new Vector2(k * font.Narrowing, k));
		DrawString(font.Font!, Vector2.Zero, shown, HorizontalAlignment.Left, -1f, font.Size, _ink with { A = Alpha(sign.IsSelected) });
	}

	/// <summary>Each letter stands by the middle of its width on the arrow's curve, turned to the way the curve goes there.</summary>
	private void DrawLabel(CPlanStroke arrow, CBattleArrowType type, CGuiFont font, float shift)
	{
		var points = new List<(float X, float Y)>(arrow.Points);
		float east = points.Count >= 2 ? points[^1].X - points[0].X : 0f;
		bool reversed = east < 0f;
		if (reversed)
		{
			points.Reverse();
		}
		int skip = SkippedPoints + (!reversed && arrow.SnapPrefix != -1 ? arrow.SnapPrefix : 0);
		if (skip >= points.Count)
		{
			skip = 0;
		}
		List<(float X, float Y)> samples = CArrowMesh.Samples(points.Skip(skip).ToList());
		if (samples.Count < 2)
		{
			return;
		}
		string shown = CWindows1252.ToDisplay(arrow.Label);
		float k = Pixel * type.TextScale;
		float travelled = 0f;
		int at = 0;
		float lengthSoFar = 0f;
		Color ink = _ink with { A = Alpha(arrow.IsSelected) };
		foreach (char letter in shown)
		{
			string one = letter.ToString();
			float advance = font.Font!.GetStringSize(one, HorizontalAlignment.Left, -1f, font.Size).X * k * font.Narrowing;
			float middle = travelled + advance / 2f;
			while (at + 1 < samples.Count - 1)
			{
				float dx = samples[at + 1].X - samples[at].X, dy = samples[at + 1].Y - samples[at].Y;
				float length = MathF.Sqrt(dx * dx + dy * dy);
				if (lengthSoFar + length >= middle)
				{
					break;
				}
				lengthSoFar += length;
				at++;
			}
			float sx = samples[at + 1].X - samples[at].X, sy = samples[at + 1].Y - samples[at].Y;
			float span = MathF.Max(MathF.Sqrt(sx * sx + sy * sy), 0.001f);
			float share = (middle - lengthSoFar) / span;
			float wx = samples[at].X + sx * share + arrow.OriginX + shift, wy = samples[at].Y + sy * share + arrow.OriginY;
			float angle = -MathF.Atan2(sy, sx);
			var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			DrawSetTransform(new Vector2(wx, _map.Height - wy) - along * (advance / 2f), angle, new Vector2(k * font.Narrowing, k));
			DrawString(font.Font!, Vector2.Zero, one, HorizontalAlignment.Left, -1f, font.Size, ink);
			travelled += advance;
		}
	}
}
