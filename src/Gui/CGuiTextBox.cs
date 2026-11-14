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
				Redraw();
			}
		}
	}

	private Color? _colour;
	private CGuiContext? _context;

	/// <summary>The size of a textBoxType's picture behind its text, where it has one that could be loaded.</summary>
	private (int Width, int Height)? _background;

	/// <summary>What a textBoxType's lines are drawn on, cut off where the original's picture of them ends; none for the other kinds.</summary>
	private Control? _clip;

	/// <summary>Draws the text again, wherever it is drawn.</summary>
	private void Redraw()
	{
		QueueRedraw();
		_clip?.QueueRedraw();
	}

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

		// A textBoxType's font draws it into a picture of its own, of which only part is shown: its lines are drawn on a
		// child that cuts them off there. The box's textureFile, which the original draws into that picture behind the
		// lines, is not drawn here, but having one centres the lines up and down (interface.md, section 6).
		if (!type.IsInstant && !type.IsEdit)
		{
			if (type.TextureFile.Length > 0 && context.Textures.LoadFromGamePath(type.TextureFile) is { } picture)
			{
				_background = (picture.GetWidth(), picture.GetHeight());
			}
			_clip = new Control { Name = "Text", ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
			_clip.Draw += DrawClipped;
			AddChild(_clip);
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

	/// <summary>Enter pressed while the box has the keyboard. The box takes the key, so nothing behind it sees it.</summary>
	public event System.Action? Entered;

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

	/// <summary>A box that goes gives up the keyboard, so that nothing asks it afterwards.</summary>
	public override void _ExitTree()
	{
		if (_context?.KeyboardFocus == this)
		{
			_context.KeyboardFocus = null;
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
		if (key.Keycode is Key.Enter or Key.KpEnter)
		{
			Entered?.Invoke();
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
			foreach (string line in CColouredText.CarryColour(Lines(value)))
			{
				_lines.Add(CColouredText.Parse(line));
			}
			Clip();
			Redraw();
		}
	}

	/// <summary>The width a screen that lays a box out itself gives it, for breaking, cutting and placing its lines, in place of its file's: the Statistics screen's cells.</summary>
	public float? LaidOutWidth
	{
		get => _laidOutWidth;
		set
		{
			_laidOutWidth = value;
			Clip();
		}
	}

	private float? _laidOutWidth;

	/// <summary>
	/// A textBoxType's width and height as its font takes them: its file's, or the screen's width, with a box of fixed
	/// size given its picture's where it gives nought (<see cref="CTextBoxPlacement.Size"/>).
	/// </summary>
	private (int Width, int Height) FontSize()
	{
		var type = (CGuiTextBoxType)GuiType;
		int width = LaidOutWidth is { } laidOut ? (int)laidOut : type.MaxWidth;
		return CTextBoxPlacement.Size(width, type.MaxHeight, type.FixedSize, type.BorderSize.X, type.BorderSize.Y,
			_background?.Width, _background?.Height);
	}

	/// <summary>Whether a textBoxType's lines are laid out in a picture of the box's size (<see cref="CTextBoxPlacement.Fits"/>).</summary>
	private bool Fits()
	{
		(int width, int height) = FontSize();
		return CTextBoxPlacement.Fits(width, height, ((CGuiTextBoxType)GuiType).FixedSize);
	}

	/// <summary>Sizes what a textBoxType's lines are drawn on to the part of the original's picture of them that is shown.</summary>
	private void Clip()
	{
		if (_clip is null || _font is null)
		{
			return;
		}
		var type = (CGuiTextBoxType)GuiType;
		(int width, int height) = FontSize();
		(float shownWidth, float shownHeight) = CTextBoxPlacement.Shown(Fits(), width, height, type.BorderSize.X,
			type.BorderSize.Y, Widest(), _lines.Count, _font.LineHeight);
		_clip.Position = Vector2.Zero;
		_clip.Size = new Vector2(Mathf.Max(0f, shownWidth), Mathf.Max(0f, shownHeight));
	}

	/// <summary>How wide a laid-out line is.</summary>
	private float LineWidth(List<CTextRun> line)
	{
		float width = 0f;
		foreach (CTextRun run in line)
		{
			width += CTextIcons.Measure(run.Text, _font!.Width);
		}
		return width;
	}

	/// <summary>The widest of the laid-out lines.</summary>
	private float Widest()
	{
		float widest = 0f;
		foreach (List<CTextRun> line in _lines)
		{
			widest = Mathf.Max(widest, LineWidth(line));
		}
		return widest;
	}

	/// <summary>
	/// The width an <c>instantTextBoxType</c> of no <c>maxWidth</c> is given by the object that draws it, after the cut
	/// of a box of fixed size and before its font breaks the text (<c>reversing/findings/FINDINGS-textdraw.md</c>,
	/// section 1). No file in the base game has one.
	/// </summary>
	public const float InstantDefaultWidth = 200f;

	/// <summary>
	/// The text as the original's font lays it out (<c>docs/mechanics/interface.md</c>, section 6): a box of fixed size
	/// cut first (<see cref="CFixedSizeCut"/>), then broken at its width whatever its height, the lines running on down
	/// past it - an <c>instantTextBoxType</c>'s by <see cref="CFontLineBreak"/>, a <c>textBoxType</c>'s by
	/// <see cref="CTextBoxLineBreak"/>. An edit box keeps the rule from before the reading (<see cref="EditLines"/>).
	/// </summary>
	private List<string> Lines(string text)
	{
		var type = (CGuiTextBoxType)GuiType;
		if (_font is null)
		{
			return new List<string>(text.Split('\n'));
		}
		if (type.IsEdit)
		{
			return EditLines(text);
		}
		CGuiFont font = _font;
		float Measure(string run) => CTextIcons.Measure(run, font.Width);
		if (type.IsInstant)
		{
			float width = LaidOutWidth ?? type.MaxWidth;
			if (type.FixedSize)
			{
				text = CFixedSizeCut.Cut(text, width, type.MaxHeight, type.BorderSize.X, font.LineHeight, Measure);
			}
			return CFontLineBreak.Break(text, width == 0f ? InstantDefaultWidth : width, type.BorderSize.X, Measure);
		}

		// A textBoxType is cut only where it has a height, its picture's where it gives none and is of fixed size.
		(int boxWidth, int boxHeight) = FontSize();
		if (type.FixedSize && boxHeight != 0)
		{
			text = CFixedSizeCut.Cut(text, boxWidth, boxHeight, type.BorderSize.X, font.LineHeight, Measure);
		}
		return CTextBoxLineBreak.Break(text, boxWidth, type.BorderSize.X, Measure);
	}

	/// <summary>
	/// How tall the box's text is drawn: its lines and its border above and below. A box of no
	/// height in its file - a popup's lines - is as tall as this, and the next placed under it.
	/// </summary>
	public float DrawnHeight => _font is null ? 0f : _lines.Count * _font.LineHeight + 2f * ((CGuiTextBoxType)GuiType).BorderSize.Y;

	/// <summary>
	/// An edit box's text when it shows it all: each paragraph broken at its spaces, but only in a box tall enough for a
	/// second line. The original's edit box shows one line of its text layout, the caret's (<c>interface.md</c>, section
	/// 8); which line that is when a screen sets a text was not read, so this is ours.
	/// </summary>
	private List<string> EditLines(string text)
	{
		var type = (CGuiTextBoxType)GuiType;
		var lines = new List<string>();
		CGuiFont font = _font!;
		float inside = (LaidOutWidth ?? type.MaxWidth) - 2f * type.BorderSize.X;
		bool wraps = inside > 0f && (type.MaxHeight == 0 || type.MaxHeight >= 2f * font.LineHeight);
		foreach (string paragraph in text.Split('\n'))
		{
			lines.AddRange(wraps ? CWordWrap.Wrap(paragraph, inside, run => CTextIcons.Measure(run, font.Width)) : new[] { paragraph });
		}
		return lines;
	}

	/// <summary>The box's <c>maxWidth</c> as its file gives it, border and all.</summary>
	public float MaxWidth => ((CGuiTextBoxType)GuiType).MaxWidth;

	/// <summary>How wide a text is in the box's font, colour escapes and all as given; nought with no font.</summary>
	public float Measure(string text) => _font is null ? 0f : CTextIcons.Measure(text, _font.Width);

	/// <summary>
	/// A paragraph laid out by the original's text layout in the box's font at the box's <c>maxWidth</c>, however tall the
	/// box (<see cref="CTextLayoutLineBreak"/>): for a list that makes a row of each piece, as the diplomacy screen's
	/// status list does.
	/// </summary>
	public IEnumerable<string> Pieces(string paragraph)
	{
		var type = (CGuiTextBoxType)GuiType;
		if (_font is null || type.MaxWidth <= 0)
		{
			return new[] { paragraph };
		}
		CGuiFont font = _font;
		return CTextLayoutLineBreak.Break(paragraph, type.MaxWidth, text => CTextIcons.Measure(text, font.Width));
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

		// A textBoxType's lines are drawn on the child that cuts them off (DrawClipped).
		if (_clip is not null)
		{
			return;
		}
		float widest = Widest();
		foreach (List<CTextRun> line in _lines)
		{
			float x = inset.X + CGuiLayout.TextStart(type.Format, type.Orientation, type.BorderSize.X, type.MaxWidth, LineWidth(line), widest);
			DrawLine(this, line, x, y - _font.Ascent);
			y += _font.LineHeight;
		}
	}

	/// <summary>One laid-out line, its colour escapes obeyed, from its left end and its top.</summary>
	private void DrawLine(CanvasItem item, List<CTextRun> line, float x, float top)
	{
		foreach (CTextRun run in line)
		{
			Color colour = CTextColours.TryGet(run.Colour, out CColor named) ? named.ToGodot() : Colour ?? _font!.Color;
			x = _context is null ? x : _context.TextIcons.Draw(item, _font!, run.Text, x, top, colour);
		}
	}

	/// <summary>
	/// A textBoxType's lines, placed as the original's font places them in the picture it draws them into
	/// (<see cref="CTextBoxPlacement"/>), on the child that is as large as the part of that picture shown.
	/// </summary>
	private void DrawClipped()
	{
		if (_font is null || _clip is null)
		{
			return;
		}
		var type = (CGuiTextBoxType)GuiType;
		(int width, int height) = FontSize();
		bool fits = Fits();
		float top = CTextBoxPlacement.Top(fits, _background is not null, height, type.BorderSize.Y, _lines.Count, _font.LineHeight);
		foreach (List<CTextRun> line in _lines)
		{
			float x = CTextBoxPlacement.LineStart(type.Format, fits, width, type.BorderSize.X, LineWidth(line));
			DrawLine(_clip, line, x, top);
			top += _font.LineHeight;
		}
	}
}
