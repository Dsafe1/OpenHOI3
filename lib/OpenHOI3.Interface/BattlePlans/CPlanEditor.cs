using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.BattlePlans;
using OpenHOI3.Sim.Plans;

namespace OpenHOI3.Interface.BattlePlans;

/// <summary>
/// The tools of a battle plan and what the mouse does with them, in map pixels: which tool, each one's option, the drawing
/// under way and the selection. The state lasts as the original's does, between openings of the window. See
/// <c>docs/mechanics/battleplans.md</c>, sections 9 and 10.
/// </summary>
public sealed class CPlanEditor
{
	/// <summary>How far a mouse goes from a press and still clicks.</summary>
	private const float ClickRange = 5f;

	private const float HitTolerance = 1.25f;

	/// <summary>How near, squared, a click comes to a stroke's point.</summary>
	private const float StrokeRange = 31.25f;

	private const float RotationStep = 0.15f;
	private const float SizeStep = 0.75f;
	private const float SmallestSize = 0.25f;
	private const float LargestSize = 20f;
	public const string NewText = "New Text";

	private readonly CBattlePlanSettings _settings;
	private readonly CPlanObjectTypes _types;
	private readonly int[] _options = new int[7];
	private CPlanStroke? _stroke;
	private CPlanIcon? _icon;
	private (float X, float Y)? _press;

	public CPlanEditor(CBattlePlanSettings settings, CPlanObjectTypes types)
	{
		_settings = settings;
		_types = types;
	}

	public EPlanTool Tool { get; private set; }

	/// <summary>The plan drawn on: chosen, live and the player's; null otherwise.</summary>
	public CBattlePlan? Plan { get; set; }

	public float Rotation { get; private set; }

	public float Size { get; private set; } = 2f;

	public bool PlacingText { get; private set; }

	public string Text { get; private set; } = "";

	/// <summary>The sign under the mouse while placing, drawn and not yet on the plan.</summary>
	public CPlanText? Preview { get; private set; }

	/// <summary>The stroke under the mouse, drawn and not yet on the plan.</summary>
	public CPlanStroke? Drawing => _stroke;

	/// <summary>The icon held, drawn and not yet on the plan.</summary>
	public CPlanIcon? HeldIcon => _icon;

	public bool IsDrawing => _stroke is not null || _icon is not null;

	public CPlanTool? OptionsOf(EPlanTool tool) => tool switch
	{
		EPlanTool.Colours => _settings.Colours,
		EPlanTool.Arrows => _settings.Arrows,
		EPlanTool.Borders => _settings.Borders,
		EPlanTool.Icons => _settings.Icons,
		_ => null,
	};

	public int OptionOf(EPlanTool tool) => _options[(int)tool];

	public int LongestOptions => new[] { _settings.Colours, _settings.Arrows, _settings.Borders, _settings.Icons }.Max(tool => tool.Options.Count);

	/// <summary>A palette colour as red, green and blue; past the palette's end magenta.</summary>
	public (float R, float G, float B) Colour(int index)
	{
		List<CPlanToolOption> options = _settings.Colours.Options;
		return index >= 0 && index < options.Count ? (options[index].Red, options[index].Green, options[index].Blue) : (1f, 0f, 1f);
	}

	private string CurrentType(EPlanTool tool)
	{
		List<CPlanToolOption> options = OptionsOf(tool)!.Options;
		int index = _options[(int)tool];
		return index < options.Count ? options[index].Type : "";
	}

	private int CurrentColour => _options[(int)EPlanTool.Colours];

	/// <summary>A tool's button. Erase deletes what is selected and leaves the tool as it was; Text answers what the box is to hold.</summary>
	public string? PressTool(EPlanTool tool)
	{
		if (tool == EPlanTool.Erase)
		{
			DeleteSelected();
			return null;
		}
		string? box = tool == EPlanTool.Text ? PrepareText() : null;
		Tool = tool;
		if (tool != EPlanTool.Text)
		{
			PlacingText = false;
			Preview = null;
		}
		return box;
	}

	private string PrepareText()
	{
		if (Plan?.Arrows.FirstOrDefault(arrow => arrow.IsSelected) is { } arrow)
		{
			PlacingText = false;
			return arrow.Label;
		}
		if (Plan?.Texts.FirstOrDefault(sign => sign.IsSelected) is { } sign2)
		{
			PlacingText = false;
			return sign2.Text;
		}
		PlacingText = true;
		Text = NewText;
		return NewText;
	}

	/// <summary>The text prompt's Apply.</summary>
	public void Apply(string typed)
	{
		if (Plan is not { } plan)
		{
			return;
		}
		if (plan.Arrows.FirstOrDefault(arrow => arrow.IsSelected) is { } arrow)
		{
			arrow.Label = typed;
			plan.IsDirty = true;
		}
		else if (plan.Texts.FirstOrDefault(sign => sign.IsSelected) is { } sign)
		{
			if (typed.Length == 0)
			{
				plan.Texts.Remove(sign);
				Tool = EPlanTool.Select;
			}
			else
			{
				sign.Text = typed;
			}
			plan.IsDirty = true;
		}
		else if (typed.Length == 0)
		{
			Tool = EPlanTool.Select;
			PlacingText = false;
			Preview = null;
		}
		else
		{
			Text = typed;
		}
	}

	/// <summary>An option chosen for the current tool; the colours also recolour what is selected, a text sign excepted.</summary>
	public void ChooseOption(int index)
	{
		_options[(int)Tool] = index;
		if (Tool != EPlanTool.Colours || Plan is not { } plan)
		{
			return;
		}
		foreach (CPlanStroke stroke in plan.Arrows.Concat(plan.Borders).Where(stroke => stroke.IsSelected))
		{
			stroke.Colour = index;
		}
		foreach (CPlanIcon icon in plan.Icons.Where(icon => icon.IsSelected))
		{
			icon.Colour = index;
		}
		plan.IsDirty = true;
	}

	/// <summary>Whether a left press goes no further than the plan: every tool but the plain select.</summary>
	public bool TakesLeftPress(bool ctrl, bool shift) => Plan is not null && (Tool != EPlanTool.Select || ctrl || shift);

	/// <summary>Whether the wheel goes no further than the plan.</summary>
	public bool TakesWheel(bool ctrl, bool shift)
	{
		return Plan is not null && (ctrl || shift) && Tool is EPlanTool.Select or EPlanTool.Icons or EPlanTool.Text;
	}

	public void ClearSelection()
	{
		if (Plan is not { } plan)
		{
			return;
		}
		foreach (CPlanStroke stroke in plan.Arrows.Concat(plan.Borders))
		{
			stroke.IsSelected = false;
		}
		foreach (CPlanIcon icon in plan.Icons)
		{
			icon.IsSelected = false;
		}
		foreach (CPlanText sign in plan.Texts)
		{
			sign.IsSelected = false;
		}
	}

	public void DeleteSelected()
	{
		if (Plan is not { } plan)
		{
			return;
		}
		plan.Arrows.RemoveAll(stroke => stroke.IsSelected);
		plan.Borders.RemoveAll(stroke => stroke.IsSelected);
		plan.Icons.RemoveAll(icon => icon.IsSelected);
		plan.Texts.RemoveAll(sign => sign.IsSelected);
		plan.IsDirty = true;
	}

	/// <summary>A left press on the map, at a point in map pixels.</summary>
	public void Press(float x, float y, bool shift)
	{
		if (Plan is not { } plan)
		{
			return;
		}
		switch (Tool)
		{
			case EPlanTool.Select:
				_press = (x, y);
				break;
			case EPlanTool.Arrows:
				ClearSelection();
				_stroke = NewStroke(CurrentType(EPlanTool.Arrows), x, y);
				if (shift)
				{
					SnapToNearest(plan, _stroke, x, y);
				}
				CArrowMesh.AddPoint(_stroke.Points, x - _stroke.OriginX, y - _stroke.OriginY);
				break;
			case EPlanTool.Borders:
				ClearSelection();
				_stroke = NewStroke(CurrentType(EPlanTool.Borders), x, y);
				CArrowMesh.AddPoint(_stroke.Points, x - _stroke.OriginX, y - _stroke.OriginY);
				break;
			case EPlanTool.Icons:
				ClearSelection();
				_icon = new CPlanIcon { Type = CurrentType(EPlanTool.Icons), Colour = CurrentColour, OriginX = x, OriginY = y, Rotation = Rotation };
				break;
			case EPlanTool.Text:
				if (Preview is { } preview && PlacingText)
				{
					ClearSelection();
					preview.IsSelected = true;
					plan.Texts.Add(preview);
					plan.IsDirty = true;
					Preview = null;
				}
				break;
		}
	}

	private CPlanStroke NewStroke(string type, float x, float y)
	{
		return new CPlanStroke { Type = type, Colour = CurrentColour, OriginX = x, OriginY = y };
	}

	/// <summary>The mouse's place each frame: a held stroke grows, a held icon follows, a sign to be placed goes with it.</summary>
	public void Move(float x, float y)
	{
		if (_stroke is { } stroke)
		{
			CArrowMesh.AddPoint(stroke.Points, x - stroke.OriginX, y - stroke.OriginY);
		}
		if (_icon is { } icon)
		{
			icon.X = x - icon.OriginX;
			icon.Y = y - icon.OriginY;
			icon.Rotation = Rotation;
		}
		if (Plan is not null && Tool == EPlanTool.Text && PlacingText)
		{
			Preview ??= new CPlanText { Text = Text, Size = Size, Rotation = Rotation };
			Preview.Text = Text;
			Preview.OriginX = x;
			Preview.OriginY = y;
			Preview.Size = Size;
			Preview.Rotation = Rotation;
		}
	}

	/// <summary>The left button let go.</summary>
	public void Release(float x, float y)
	{
		if (_stroke is { } stroke && Plan is { } plan)
		{
			_stroke = null;
			CArrowMesh.Smooth(stroke.Points, stroke.SnapPrefix);
			stroke.IsSelected = true;
			(Tool == EPlanTool.Borders ? plan.Borders : plan.Arrows).Add(stroke);
			plan.IsDirty = true;
		}
		if (_icon is { } icon && Plan is { } owner)
		{
			_icon = null;
			icon.IsSelected = true;
			owner.Icons.Add(icon);
			owner.IsDirty = true;
		}
		if (_press is { } press)
		{
			_press = null;
			float dx = x - press.X, dy = y - press.Y;
			if (MathF.Sqrt(dx * dx + dy * dy) <= ClickRange)
			{
				ClearSelection();
				HitTest(x, y);
			}
		}
	}

	/// <summary>A click selects one thing: an icon, a sign, then the nearest arrow by its points, then the nearest border.</summary>
	public void HitTest(float x, float y)
	{
		if (Plan is not { } plan)
		{
			return;
		}
		foreach (CPlanIcon icon in plan.Icons)
		{
			float scale = _types.Icon(icon.Type)?.Scale ?? 0f;
			if (Within(icon.X + icon.OriginX - x, icon.Y + icon.OriginY - y, HitTolerance * scale * 0.5f))
			{
				icon.IsSelected = true;
				return;
			}
		}
		foreach (CPlanText sign in plan.Texts)
		{
			if (Within(sign.X + sign.OriginX - x, sign.Y + sign.OriginY - y, HitTolerance * sign.Text.Length * sign.Size * 0.25f))
			{
				sign.IsSelected = true;
				return;
			}
		}
		if (Nearest(plan.Arrows, x, y) is { } arrow)
		{
			arrow.IsSelected = true;
			return;
		}
		if (Nearest(plan.Borders, x, y) is { } border)
		{
			border.IsSelected = true;
		}
	}

	private static bool Within(float dx, float dy, float radius) => dx * dx + dy * dy <= radius * radius;

	private static CPlanStroke? Nearest(List<CPlanStroke> strokes, float x, float y)
	{
		CPlanStroke? best = null;
		float bestRange = StrokeRange;
		foreach (CPlanStroke stroke in strokes)
		{
			foreach ((float px, float py) in stroke.Points)
			{
				float dx = px + stroke.OriginX - x, dy = py + stroke.OriginY - y;
				float range = dx * dx + dy * dy;
				if (range < bestRange)
				{
					bestRange = range;
					best = stroke;
				}
			}
		}
		return best;
	}

	/// <summary>Shift held at a press: the new arrow begins along the nearest other, up to its nearest point.</summary>
	private static void SnapToNearest(CBattlePlan plan, CPlanStroke stroke, float x, float y)
	{
		CPlanStroke? best = null;
		int bestPoint = 0;
		float bestRange = float.MaxValue;
		foreach (CPlanStroke other in plan.Arrows)
		{
			for (int index = 0; index < other.Points.Count; index++)
			{
				float dx = other.Points[index].X + other.OriginX - x, dy = other.Points[index].Y + other.OriginY - y;
				float range = dx * dx + dy * dy;
				if (range < bestRange)
				{
					bestRange = range;
					best = other;
					bestPoint = index;
				}
			}
		}
		if (best is null)
		{
			return;
		}
		stroke.OriginX = best.OriginX;
		stroke.OriginY = best.OriginY;
		stroke.Points.AddRange(best.Points.Take(bestPoint + 1));
		stroke.SnapPrefix = bestPoint - 1;
		stroke.SnapParentPoints = best.Points.Count;
	}

	/// <summary>Shift and the wheel turn, Ctrl and the wheel size; what is selected takes it under the select tool.</summary>
	public void Wheel(int sign, bool ctrl, bool shift)
	{
		if (Plan is not { } plan)
		{
			return;
		}
		if (shift && Tool is EPlanTool.Select or EPlanTool.Icons or EPlanTool.Text)
		{
			Rotation += RotationStep * sign;
			if (Tool == EPlanTool.Select)
			{
				if (plan.Icons.FirstOrDefault(icon => icon.IsSelected) is { } icon)
				{
					icon.Rotation = Rotation;
				}
				if (plan.Texts.FirstOrDefault(text => text.IsSelected) is { } selected)
				{
					selected.Rotation = Rotation;
				}
			}
		}
		if (ctrl && (Tool == EPlanTool.Text || (Tool == EPlanTool.Select && plan.Texts.Any(text => text.IsSelected))))
		{
			Size = Math.Clamp(Size + SizeStep * sign, SmallestSize, LargestSize);
			if (Tool != EPlanTool.Text || !PlacingText)
			{
				foreach (CPlanText text in plan.Texts.Where(text => text.IsSelected))
				{
					text.Size = Size;
				}
				plan.IsDirty = true;
			}
		}
	}
}
