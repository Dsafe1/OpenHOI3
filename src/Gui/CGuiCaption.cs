using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;

namespace OpenHOI3.Gui;

/// <summary>
/// The words on a button: one line, in the button's font and its colour, its colour escapes obeyed,
/// in the middle of the button - or at its left or right edge where the button's file says
/// <c>format</c>. It is not an element of the interface files' own - a button declares its caption -
/// so it has no type and is found by nothing.
/// </summary>
public partial class CGuiCaption : Control
{
	private CGuiFont? _font;
	private string _text = "";
	private EGuiTextFormat _format = EGuiTextFormat.Centre;
	private List<CTextRun> _runs = new();

	public void Initialise(string text, CGuiFont? font, EGuiTextFormat format = EGuiTextFormat.Centre)
	{
		Name = "Text";
		_font = font;
		_format = format;
		Text = text;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>The words shown. Setting them to what is already shown does nothing.</summary>
	public string Text
	{
		get => _text;
		set
		{
			if (value != _text || _runs.Count == 0)
			{
				_text = value;
				_runs = CColouredText.Parse(value);
				QueueRedraw();
			}
		}
	}

	public override void _Draw()
	{
		if (_font is null)
		{
			return;
		}
		float width = 0f;
		foreach (CTextRun run in _runs)
		{
			width += _font.Width(run.Text);
		}
		float x = _format switch
		{
			EGuiTextFormat.Left => 0f,
			EGuiTextFormat.Right => Size.X - width,
			_ => (Size.X - width) / 2f,
		};
		float y = (Size.Y - _font.LineHeight) / 2f + _font.Ascent;
		foreach (CTextRun run in _runs)
		{
			Color colour = CTextColours.TryGet(run.Colour, out CColor named) ? named.ToGodot() : _font.Color;
			_font.Draw(this, new Vector2(Mathf.Round(x), Mathf.Round(y)), run.Text, colour);
			x += _font.Width(run.Text);
		}
	}
}
