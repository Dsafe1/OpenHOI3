using System.Collections.Generic;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Interface;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;

namespace OpenHOI3.Gui;

/// <summary>
/// A <c>textBoxType</c> or an <c>instantTextBoxType</c>: text in one of the fonts the
/// interface files name, with its colour escapes obeyed.
///
/// It draws its own glyphs rather than using a label, because a run of text changes
/// colour in the middle and the font's own colour is the tint of the whole.
/// </summary>
public partial class CGuiTextBox : CGuiObject
{
	private CGuiFont? _font;
	private string _text = "";
	private readonly List<List<CTextRun>> _lines = new();

	/// <summary>
	/// The colour its text is drawn in where no colour escape says otherwise, in place of the font's:
	/// what the original sets on a box, as the diplomacy screen's relation figures.
	/// </summary>
	public Color? Colour
	{
		get => _colour;
		set
		{
			if (_colour != value)
			{
				_colour = value;
				QueueRedraw();
			}
		}
	}

	private Color? _colour;
	private CGuiContext? _context;

	public void Initialise(CGuiTextBoxType type, CGuiContext context)
	{
		_context = context;
		Place(type, new Vector2(type.MaxWidth, type.MaxHeight));
		MouseFilter = MouseFilterEnum.Ignore;

		_font = context.Fonts.Find(type.Font);
		if (_font is null)
		{
			CDiagnostics.Warn($"interface: '{type.Name}' names the font '{type.Font}', which could not be loaded");
		}

		// Until something sets it, a box shows what its file wrote in it.
		Text = context.Localisation.Localise(type.Text);
	}

	/// <summary>What the box shows. Setting it to what it already shows does nothing.</summary>
	public string Text
	{
		get => _text;
		set
		{
			if (value == _text)
			{
				return;
			}
			_text = value;
			_lines.Clear();
			foreach (string paragraph in value.Split('\n'))
			{
				foreach (string line in Wrap(paragraph))
				{
					_lines.Add(CColouredText.Parse(Cut(line)));
				}
			}
			QueueRedraw();
		}
	}

	/// <summary>What a line too long for a box of fixed size ends in.</summary>
	private const string CutMark = "...";

	/// <summary>
	/// A line too wide for a box that says <c>fixedsize = yes</c> is cut, and ends in
	/// three dots: as many of its letters as leave the dots room. Seen on a screenshot
	/// of the original's battle window, where a division's name ends that way in the
	/// middle of a word; the routine was not read, so how the original measures the room
	/// is not known. A line with a colour escape in it is left alone.
	/// </summary>
	private string Cut(string line)
	{
		var type = (CGuiTextBoxType)GuiType;
		float inside = type.MaxWidth - 2f * type.BorderSize.X;
		if (!type.FixedSize || _font is null || inside <= 0f || line.Contains(CColouredText.Escape)
			|| _font.Width(line) <= inside)
		{
			return line;
		}
		int kept = line.Length;
		while (kept > 0 && _font.Width(line[..kept] + CutMark) > inside)
		{
			kept--;
		}
		return line[..kept] + CutMark;
	}

	/// <summary>
	/// How tall the box's text is drawn: its lines and its border above and below. A box of no
	/// height in its file - a popup's lines - is as tall as this, and the next placed under it.
	/// </summary>
	public float DrawnHeight => _font is null ? 0f : _lines.Count * _font.LineHeight + 2f * ((CGuiTextBoxType)GuiType).BorderSize.Y;

	/// <summary>
	/// Breaks a line that is too wide for the box at its spaces - but only in a box tall
	/// enough for a second line, so that a figure a little wider than its box is not
	/// pushed out of sight, or in one of no height, which grows with its text.
	/// </summary>
	private IEnumerable<string> Wrap(string paragraph)
	{
		var type = (CGuiTextBoxType)GuiType;
		float inside = type.MaxWidth - 2f * type.BorderSize.X;
		if (_font is null || inside <= 0f || (type.MaxHeight != 0 && type.MaxHeight < 2f * _font.LineHeight))
		{
			return new[] { paragraph };
		}
		return CWordWrap.Wrap(paragraph, inside, _font.Width);
	}

	/// <summary>
	/// A paragraph broken at its spaces to the box's width in its font, however tall the box: for a list
	/// that makes a row of each piece, as the diplomacy screen's status list does.
	/// </summary>
	public IEnumerable<string> Pieces(string paragraph)
	{
		var type = (CGuiTextBoxType)GuiType;
		float inside = type.MaxWidth - 2f * type.BorderSize.X;
		if (_font is null || inside <= 0f)
		{
			return new[] { paragraph };
		}
		CGuiFont font = _font;
		return CWordWrap.Wrap(paragraph, inside, text => CTextIcons.Measure(CColouredText.Strip(text), font.Width));
	}

	public override void _Draw()
	{
		if (_font is null)
		{
			return;
		}
		var type = (CGuiTextBoxType)GuiType;
		float y = type.BorderSize.Y + _font.Ascent;
		// An edit box's text starts further in than a text box's: nearly a second border
		// across, and a little more than one down. Measured against a screenshot of the
		// original's unit panel, whose name box has a border of 8 by 4 and its text 15
		// across and 9 down from its corner; the routine that draws an edit box was not
		// read, so the one case is all the rule there is.
		Vector2 inset = type.IsEdit ? new Vector2(type.BorderSize.X - 1, type.BorderSize.Y + 1) : Vector2.Zero;
		y += inset.Y;

		foreach (List<CTextRun> line in _lines)
		{
			float width = 0f;
			foreach (CTextRun run in line)
			{
				width += CTextIcons.Measure(run.Text, _font.Width);
			}
			float x = inset.X + CGuiLayout.TextStart(type.Format, type.Orientation, type.BorderSize.X, type.MaxWidth, width);

			foreach (CTextRun run in line)
			{
				Color colour = CTextColours.TryGet(run.Colour, out CColor named) ? named.ToGodot() : Colour ?? _font.Color;
				x = _context is null ? x : _context.TextIcons.Draw(this, _font, run.Text, x, y - _font.Ascent, colour);
			}
			y += _font.LineHeight;
		}
	}
}
