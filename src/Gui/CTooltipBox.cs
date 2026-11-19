using System.Collections.Generic;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Gui;
using OpenHOI3.Gfx;
using OpenHOI3.Interface;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;

namespace OpenHOI3.Gui;

/// <summary>
/// The one box every tooltip is shown in: the <c>ToolTip</c> text box of
/// <c>interface/core.gui</c>, its picture behind it. It grows to its text, which is laid
/// out at its most less three borders by the original's text layout
/// (<see cref="CTextLayoutLineBreak"/>). See <c>docs/mechanics/tooltips.md</c>,
/// sections 1 and 3.
/// </summary>
public partial class CTooltipBox : Control
{
	/// <summary>The name the interface files give the box.</summary>
	public const string TypeName = "ToolTip";

	private CGuiTextBoxType _type = null!;
	private CGuiFont? _font;
	private CGuiContext _context = null!;
	private string _text = "";
	private readonly List<List<CTextRun>> _lines = new();

	/// <summary>The box the install declares, or null - said once - where it declares none.</summary>
	public static CTooltipBox? Create(CGuiContext context)
	{
		if (context.Types.Find(TypeName) is not CGuiTextBoxType type)
		{
			CDiagnostics.Problem($"interface: no text box '{TypeName}' is declared, so there are no tooltips");
			return null;
		}
		var box = new CTooltipBox { Name = "TooltipBox" };
		box.Initialise(type, context);
		return box;
	}

	private void Initialise(CGuiTextBoxType type, CGuiContext context)
	{
		_type = type;
		_context = context;
		MouseFilter = MouseFilterEnum.Ignore;
		Visible = false;
		_font = context.Fonts.Find(type.Font);
		if (_font is null)
		{
			CDiagnostics.Problem($"interface: '{type.Name}' names the font '{type.Font}', which could not be loaded");
		}

		// Its picture, drawn behind the text in nine parts as the original's font draws a textBoxType's (_Draw).
		if (type.TextureFile.Length > 0)
		{
			_picture = context.Textures.LoadFromGamePath(type.TextureFile);
		}
	}

	private Texture2D? _picture;

	/// <summary>
	/// Shows a text, or hides the box for an empty one. The text it shows already is left
	/// as it is.
	/// </summary>
	public void Show(string text)
	{
		Visible = text.Length > 0;
		if (text == _text)
		{
			return;
		}
		_text = text;
		_lines.Clear();
		// The original lays the text out at the box's most less three borders and sets what that gives into the box, whose
		// font draws it as one text: no line is broken again there, and a colour runs on over a break.
		float wrap = CTooltipText.WrapWidth(_type.MaxWidth, _type.BorderSize.X);
		IEnumerable<string> laidOut = _font is null ? text.Split('\n') : CTextLayoutLineBreak.Break(text, wrap, Measure);
		float widest = 0f;
		foreach (string line in CColouredText.CarryColour(laidOut))
		{
			List<CTextRun> runs = CColouredText.Parse(line);
			_lines.Add(runs);
			widest = Mathf.Max(widest, Width(runs));
		}

		// Ours: the box is its text with the border all round. Where the original puts the
		// text inside its box was not read.
		float lineHeight = _font?.LineHeight ?? 0f;
		Size = new Vector2(Mathf.Ceil(widest) + 2 * _type.BorderSize.X,
			Mathf.Ceil(_lines.Count * lineHeight) + 2 * _type.BorderSize.Y);
		QueueRedraw();
	}

	/// <summary>How wide a text is in the box's font, a flag counting 24 and a counter 30.</summary>
	private float Measure(string text)
	{
		return _font is null ? 0f : CTextIcons.Measure(text, _font.Width);
	}

	private float Width(List<CTextRun> runs)
	{
		float width = 0f;
		foreach (CTextRun run in runs)
		{
			width += Measure(run.Text);
		}
		return width;
	}

	public override void _Draw()
	{
		// The box does not fit - its text is set with no height - so what it shows is its text and the border all round,
		// which is its size, and the picture is drawn over that less two each way (interface.md, section 6).
		if (_picture is { } picture)
		{
			CGuiTextBox.DrawPicture(this, picture, Vector2.Zero, Size.X, Size.Y, _type.BorderSize.X, _type.BorderSize.Y);
		}
		if (_font is null)
		{
			return;
		}
		float top = _type.BorderSize.Y;
		foreach (List<CTextRun> line in _lines)
		{
			float x = _type.BorderSize.X;
			foreach (CTextRun run in line)
			{
				Color colour = CTextColours.TryGet(run.Colour, out CColor named) ? named.ToGodot() : _font.Color;
				x = _context.TextIcons.Draw(this, _font, run.Text, x, top, colour);
			}
			top += _font.LineHeight;
		}
	}
}
