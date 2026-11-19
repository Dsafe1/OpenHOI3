using System.Collections.Generic;
using Godot;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Interface;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;

namespace OpenHOI3.Gui;

/// <summary>
/// The words on a button, in the button's font and its colour, its colour escapes obeyed. It is not an element of the
/// interface files' own - a button declares its caption - so it has no type and is found by nothing.
///
/// A button's caption is laid out as the original's button lays it out (<see cref="CButtonCaption"/>;
/// <c>docs/mechanics/interface.md</c>, section 6): on a button that names a sprite as a <c>textBoxType</c>'s text in the
/// sprite's picture - cut, broken, each line centred and the lines centred up and down, what falls outside the picture
/// less the border cut off (<see cref="InitialiseSprite"/>) - and on a button that names none as an
/// <c>instantTextBoxType</c>'s text in the box its <c>size</c> gives (<see cref="InitialiseText"/>). A scrollbar's end
/// buttons keep the plain rule from before the reading, one line in the middle or at the edge its <c>format</c> says
/// (<see cref="Initialise"/>): how the original draws theirs was not read.
/// </summary>
public partial class CGuiCaption : Control
{
	private enum ELayout
	{
		Plain,
		Sprite,
		Text,
	}

	private CGuiFont? _font;
	private string _text = "";
	private EGuiTextFormat _format = EGuiTextFormat.Centre;
	private EGuiOrientation _orientation = EGuiOrientation.UpperLeft;
	private ELayout _layout = ELayout.Plain;
	private int _width;
	private int _height;
	private int _borderX;
	private int _borderY;
	private bool _cut;
	private readonly List<List<CTextRun>> _lines = new();

	/// <summary>One line, in the middle of what holds it or at its left or right edge: a scrollbar's end buttons.</summary>
	public void Initialise(string text, CGuiFont? font, EGuiTextFormat format = EGuiTextFormat.Centre)
	{
		Name = "Text";
		_font = font;
		_format = format;
		_layout = ELayout.Plain;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		SetText(text);
	}

	/// <summary>
	/// A sprite button's caption: laid out in the box <see cref="CButtonCaption.SpriteBox"/> gives, from the button's upper
	/// left corner, and shown as far as that box less the border.
	/// </summary>
	public void InitialiseSprite(string text, CGuiFont? font, int width, int height, int borderX, int borderY, bool cut)
	{
		Name = "Text";
		_font = font;
		_layout = ELayout.Sprite;
		_width = width;
		_height = height;
		_borderX = borderX;
		_borderY = borderY;
		_cut = cut;
		MouseFilter = MouseFilterEnum.Ignore;
		ClipContents = true;
		(float shownWidth, float shownHeight) = CTextBoxPlacement.Shown(true, width, height, borderX, borderY, 0f, 0, 0f);
		Position = Vector2.Zero;
		Size = new Vector2(Mathf.Max(0f, shownWidth), Mathf.Max(0f, shownHeight));
		SetText(text);
	}

	/// <summary>A caption of a button with no sprite: an instantTextBoxType's text at the button's place, no border.</summary>
	public void InitialiseText(string text, CGuiFont? font, EGuiTextFormat format, EGuiOrientation orientation, int width)
	{
		Name = "Text";
		_font = font;
		_layout = ELayout.Text;
		_format = format;
		_orientation = orientation;
		_width = width;
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		SetText(text);
	}

	/// <summary>The words shown. Setting them to what is already shown does nothing.</summary>
	public string Text
	{
		get => _text;
		set
		{
			if (value != _text || _lines.Count == 0)
			{
				SetText(value);
			}
		}
	}

	private void SetText(string text)
	{
		_text = text;
		_lines.Clear();
		foreach (string line in CColouredText.CarryColour(Lines(text), _font is null ? null : _font.ChangesColour))
		{
			_lines.Add(CColouredText.Parse(line));
		}
		QueueRedraw();
	}

	/// <summary>The text as the caption's layout breaks it: not at all for the plain rule.</summary>
	private List<string> Lines(string text)
	{
		if (_font is null || _layout == ELayout.Plain)
		{
			return new List<string> { text };
		}
		CGuiFont font = _font;
		float Measure(string run) => font.Width(run);
		if (_layout == ELayout.Text)
		{
			return CFontLineBreak.Break(text, _width, 0f, Measure);
		}
		if (_cut && _height != 0)
		{
			text = CFixedSizeCut.Cut(text, _width, _height, _borderX, font.LineHeight, Measure);
		}
		return CTextBoxLineBreak.Break(text, _width, _borderX, Measure);
	}

	private float Width(List<CTextRun> line)
	{
		float width = 0f;
		foreach (CTextRun run in line)
		{
			width += _font!.Width(run.Text);
		}
		return width;
	}

	public override void _Draw()
	{
		if (_font is null)
		{
			return;
		}
		float widest = 0f;
		foreach (List<CTextRun> line in _lines)
		{
			widest = Mathf.Max(widest, Width(line));
		}
		float top = _layout switch
		{
			ELayout.Sprite => CTextBoxPlacement.Top(true, true, _height, _borderY, _lines.Count, _font.LineHeight),
			ELayout.Text => 0f,
			_ => (Size.Y - _font.LineHeight) / 2f,
		};
		foreach (List<CTextRun> line in _lines)
		{
			float width = Width(line);
			float x = _layout switch
			{
				ELayout.Sprite => CTextBoxPlacement.LineStart(EGuiTextFormat.Centre, true, _width, _borderX, width),
				ELayout.Text => CGuiLayout.TextStart(_format, _orientation, 0, _width, width, widest),
				_ => _format switch
				{
					EGuiTextFormat.Left => 0f,
					EGuiTextFormat.Right => Size.X - width,
					_ => (Size.X - width) / 2f,
				},
			};
			float y = top + _font.Ascent;
			if (_layout == ELayout.Plain)
			{
				x = Mathf.Round(x);
				y = Mathf.Round(y);
			}
			Color colour = _font.Color;
			foreach (CTextRun run in line)
			{
				colour = _font.RunColour(run.Colour, colour, _font.Color);
				_font.Draw(this, new Vector2(x, y), run.Text, colour);
				x += _font.Width(run.Text);
			}
			top += _font.LineHeight;
		}
	}
}
