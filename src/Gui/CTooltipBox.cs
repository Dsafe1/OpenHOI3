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
/// <c>interface/core.gui</c>, its picture behind it. It grows to its text, which is broken
/// into lines at its most less three borders. See <c>docs/mechanics/tooltips.md</c>,
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
	private readonly Dictionary<string, Texture2D?> _flags = new();
	private readonly Dictionary<string, Texture2D?> _counters = new();

	/// <summary>The box the install declares, or null - said once - where it declares none.</summary>
	public static CTooltipBox? Create(CGuiContext context)
	{
		if (context.Types.Find(TypeName) is not CGuiTextBoxType type)
		{
			CDiagnostics.Warn($"interface: no text box '{TypeName}' is declared, so there are no tooltips");
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
			CDiagnostics.Warn($"interface: '{type.Name}' names the font '{type.Font}', which could not be loaded");
		}

		// How the original draws a text box's picture was not read. This one is an even
		// panel inside a narrow frame, so its corners are taken to be the box's border and
		// the rest repeated between them.
		if (type.TextureFile.Length > 0 && context.Textures.LoadFromGamePath(type.TextureFile) is { } texture)
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
			};
			background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			AddChild(background);
		}
	}

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
		float wrap = CTooltipText.WrapWidth(_type.MaxWidth, _type.BorderSize.X);
		float widest = 0f;
		foreach (string paragraph in text.Split('\n'))
		{
			IEnumerable<string> lines = _font is null ? new[] { paragraph } : CWordWrap.Wrap(paragraph, wrap, Measure);
			foreach (string line in lines)
			{
				List<CTextRun> runs = CColouredText.Parse(line);
				_lines.Add(runs);
				widest = Mathf.Max(widest, Width(runs));
			}
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
				foreach (CTextPiece piece in CTextIcons.Split(run.Text))
				{
					if (piece.Counter is not null)
					{
						if (Counter(piece.Counter) is { } counter)
						{
							Vector2 size = counter.GetSize();
							DrawTextureRectRegion(counter,
								new Rect2(Mathf.Round(x), Mathf.Round(top), CCounterIcons.Width, CCounterIcons.Height),
								new Rect2(size.X * CCounterIcons.Left, size.Y * CCounterIcons.Top,
									size.X * (CCounterIcons.Right - CCounterIcons.Left), size.Y * (CCounterIcons.Bottom - CCounterIcons.Top)));
						}
						x += CCounterIcons.Advance;
						continue;
					}
					if (piece.Tag is null)
					{
						_font.Draw(this, new Vector2(Mathf.Round(x), Mathf.Round(top + _font.Ascent)), piece.Text, colour);
						x += _font.Width(piece.Text);
						continue;
					}
					if (Flag(piece.Tag) is { } flag)
					{
						DrawTextureRect(flag, new Rect2(Mathf.Round(x), Mathf.Round(top + CFlagIcons.BelowLineTop),
							CFlagIcons.Width, CFlagIcons.Height), false);
					}
					x += CFlagIcons.Advance;
				}
			}
			top += _font.LineHeight;
		}
	}

	/// <summary>
	/// A country's flag, from <c>gfx/flags</c>. The original draws it out of an atlas of all
	/// of them, which was not read; a tag nobody has draws nothing here, where the original
	/// draws the flag of the country numbered 1.
	/// </summary>
	private Texture2D? Flag(string tag)
	{
		if (!_flags.TryGetValue(tag, out Texture2D? flag))
		{
			string path = "gfx/flags/" + tag + ".tga";
			flag = tag.Length == CFlagIcons.TagLength && _context.Textures.Exists(path)
				? _context.Textures.LoadFromGamePath(path) : null;
			_flags[tag] = flag;
		}
		return flag;
	}

	/// <summary>The picture of the sprite <c>GFX_counter_</c> + name, or null where there is none - which draws nothing.</summary>
	private Texture2D? Counter(string name)
	{
		if (!_counters.TryGetValue(name, out Texture2D? counter))
		{
			counter = _context.Sprites.Find(CCounterIcons.SpritePrefix + name) is { } sprite ? _context.Textures.Load(sprite) : null;
			_counters[name] = counter;
		}
		return counter;
	}
}
