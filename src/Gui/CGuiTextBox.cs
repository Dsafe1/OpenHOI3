using System.Collections.Generic;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Interface;
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

	public void Initialise(CGuiTextBoxType type, CGuiContext context)
	{
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
					_lines.Add(CColouredText.Parse(line));
				}
			}
			QueueRedraw();
		}
	}

	/// <summary>
	/// Breaks a line that is too wide for the box at its spaces - but only in a box tall
	/// enough for a second line, so that a figure a little wider than its box is not
	/// pushed out of sight. A colour that is on when a line breaks is on for the next.
	/// How the original breaks lines was not read.
	/// </summary>
	private IEnumerable<string> Wrap(string paragraph)
	{
		var type = (CGuiTextBoxType)GuiType;
		float inside = type.MaxWidth - 2f * type.BorderSize.X;
		if (_font is null || inside <= 0f || type.MaxHeight < 2f * _font.LineHeight
			|| _font.Width(CColouredText.Strip(paragraph)) <= inside)
		{
			yield return paragraph;
			yield break;
		}

		string line = "";
		string colour = "";
		foreach (string word in paragraph.Split(' '))
		{
			string longer = line.Length == 0 ? word : line + " " + word;
			if (line.Length > 0 && _font.Width(CColouredText.Strip(longer)) > inside)
			{
				yield return line;
				longer = colour + word;
			}
			line = longer;

			// The last escape in the word is the colour the next line starts in.
			int escape = word.LastIndexOf(CColouredText.Escape);
			if (escape >= 0 && escape + 1 < word.Length)
			{
				colour = word.Substring(escape, 2);
			}
		}
		if (line.Length > 0)
		{
			yield return line;
		}
	}

	public override void _Draw()
	{
		if (_font is null)
		{
			return;
		}
		var type = (CGuiTextBoxType)GuiType;
		float y = type.BorderSize.Y + _font.Ascent;

		foreach (List<CTextRun> line in _lines)
		{
			float width = 0f;
			foreach (CTextRun run in line)
			{
				width += _font.Width(run.Text);
			}
			float x = CGuiLayout.TextStart(type.Format, type.Orientation, type.BorderSize.X, type.MaxWidth, width);

			foreach (CTextRun run in line)
			{
				Color colour = CTextColours.TryGet(run.Colour, out CColor named) ? named.ToGodot() : _font.Color;
				_font.Draw(this, new Vector2(Mathf.Round(x), Mathf.Round(y)), run.Text, colour);
				x += _font.Width(run.Text);
			}
			y += _font.LineHeight;
		}
	}
}
