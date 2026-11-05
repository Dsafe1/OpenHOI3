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

		// An edit box's own picture behind its text: its corners kept to the box's border and the rest repeated between
		// them, as a text box's in a tooltip, from the box's border to 2 short of its far edges. Measured on the
		// maintainer's screenshot of the save dialog, whose box of 260 by 32 and border 8 by 6 shows the picture's rim
		// 246 by 20 - the picture has 2 clear all round - at 10 and 8 in; how the original draws it was not read
		// (interface.md, section 8).
		if (type.IsEdit && type.TextureFile.Length > 0 && context.Textures.LoadFromGamePath(type.TextureFile) is { } texture)
		{
			var background = new NinePatchRect
			{
				Name = "Background",
				Texture = texture,
				PatchMarginLeft = type.BorderSize.X,
				PatchMarginRight = type.BorderSize.X,
				PatchMarginTop = type.BorderSize.Y,
				PatchMarginBottom = type.BorderSize.Y,
				AxisStretchHorizontal = NinePatchRect.AxisStretchMode.Tile,
				AxisStretchVertical = NinePatchRect.AxisStretchMode.Tile,
				MouseFilter = MouseFilterEnum.Ignore,
				ShowBehindParent = true,
				Position = new Vector2(type.BorderSize.X, type.BorderSize.Y),
				Size = new Vector2(type.MaxWidth - type.BorderSize.X - EditBackgroundShort, type.MaxHeight - type.BorderSize.Y - EditBackgroundShort),
			};
			AddChild(background);
		}
	}

	/// <summary>How far short of an edit box's right and bottom edges its picture ends: measured, see above.</summary>
	private const int EditBackgroundShort = 2;

	/// <summary>
	/// What an edit box made to take typing holds and does with keys; none for a box that only shows text - an edit box
	/// whose screen has not asked for typing among them.
	/// </summary>
	public CEditText? Editor { get; private set; }

	/// <summary>Raised when what the box holds is changed by typing.</summary>
	public event System.Action? Typed;

	/// <summary>
	/// An edit box made to take typing: a click on it, or <see cref="TakeKeyboard"/>, gives it the keyboard, and it then
	/// takes characters and its six keys before anything else, as the original's (<c>interface.md</c>, section 8).
	/// </summary>
	public void MakeEditable()
	{
		var type = (CGuiTextBoxType)GuiType;
		if (!type.IsEdit || _font is null)
		{
			return;
		}
		CGuiFont font = _font;
		Editor = new CEditText(font.Width, type.MaxWidth - 2f * type.BorderSize.X, font.Effect) { Text = _text };
		MouseFilter = MouseFilterEnum.Stop;
	}

	/// <summary>The keyboard given to the box by code, as the original's request is granted at the end of its frame.</summary>
	public void TakeKeyboard()
	{
		if (_context is not null && Editor is not null)
		{
			_context.KeyboardFocus = this;
			QueueRedraw();
		}
	}

	public bool HasKeyboard => Editor is not null && _context?.KeyboardFocus == this && IsVisibleInTree();

	/// <summary>A press of the left button over the box gives it the keyboard; the caret stays where it was.</summary>
	public override void _GuiInput(InputEvent @event)
	{
		if (Editor is not null && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			TakeKeyboard();
			AcceptEvent();
		}
	}

	/// <summary>
	/// A key, before any button's shortcut or the map sees it: the six keys and the characters the box takes go no
	/// further; what it refuses - Escape, Tab, the font's seven, anything with Ctrl - goes on.
	/// </summary>
	public override void _Input(InputEvent @event)
	{
		if (!HasKeyboard || @event is not InputEventKey { Pressed: true } key)
		{
			return;
		}
		CEditText editor = Editor!;
		bool shift = key.ShiftPressed;
		bool ctrl = key.CtrlPressed;
		bool taken = key.Keycode switch
		{
			Key.Left => editor.Key(EEditKey.Left, shift, ctrl),
			Key.Right => editor.Key(EEditKey.Right, shift, ctrl),
			Key.Home => editor.Key(EEditKey.Home, shift, ctrl),
			Key.End => editor.Key(EEditKey.End, shift, ctrl),
			Key.Delete => editor.Key(EEditKey.Delete, shift, ctrl),
			Key.Backspace => editor.Type(CEditText.Backspace, shift, ctrl),
			Key.Enter or Key.KpEnter => editor.Type(CEditText.Enter, shift, ctrl),
			Key.Escape => false,
			_ => key.Unicode is > 0 and <= 0xFFFF && CWindows1252.FromUnicode((char)key.Unicode) is { } typed
				&& editor.Type(typed, shift, ctrl),
		};
		if (!taken)
		{
			return;
		}
		GetViewport().SetInputAsHandled();
		if (editor.Text != _text)
		{
			_text = editor.Text;
			Typed?.Invoke();
		}
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (HasKeyboard)
		{
			bool shown = Editor!.CaretShown;
			Editor.Tick(delta);
			if (shown != Editor.CaretShown)
			{
				QueueRedraw();
			}
		}
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
			if (Editor is not null)
			{
				Editor.Text = value;
			}
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

		// A box that takes typing shows the line its caret is on and only that, and the caret while it has the keyboard:
		// the `cursor` sprite past the border by the width before it and 3, a line's height down for each line above.
		if (Editor is not null)
		{
			string shown = Editor.ShownLine;
			float x = inset.X + CGuiLayout.TextStart(type.Format, type.Orientation, type.BorderSize.X, type.MaxWidth, _font.Width(shown));
			_context?.TextIcons.Draw(this, _font, shown, x, y - _font.Ascent, Colour ?? _font.Color);
			if (HasKeyboard && Editor.CaretShown && _context?.Sprites.Find("cursor") is { } sprite
				&& _context.Textures.Load(sprite) is { } caret)
			{
				float across = type.BorderSize.X + _font.Width(shown[..Editor.CaretColumn]) + 3f;
				float down = type.BorderSize.Y + _font.LineHeight * Editor.CaretRow;
				DrawTexture(caret, new Vector2(across, down));
			}
			return;
		}

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
