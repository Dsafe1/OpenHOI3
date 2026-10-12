using System.Collections.Generic;
using Godot;
using OpenHOI3.Gfx;
using OpenHOI3.Interface.Text;

namespace OpenHOI3.Gui;

/// <summary>
/// The pictures a text draws in its line - a country's flag for <c>@TAG</c>, a unit's counter for
/// <c>£name</c> - kept once a game, and the drawing of a run of text with them in it. The tooltip box
/// and every text box draw through it: the original's diplomacy screen writes <c>@TAG</c> into a list's
/// text box (<c>docs/mechanics/diplomacyscreen.md</c>, section 5.3).
/// </summary>
public sealed class CTextIconTextures
{
	private readonly CTextureManager _textures;
	private readonly CSpriteTypes _sprites;
	private readonly Dictionary<string, Texture2D?> _flags = new();
	private readonly Dictionary<string, Texture2D?> _counters = new();

	public CTextIconTextures(CTextureManager textures, CSpriteTypes sprites)
	{
		_textures = textures;
		_sprites = sprites;
	}

	/// <summary>
	/// Draws a run in a colour from x on a line whose top is given, and answers where it ends. A flag
	/// stands below the line's top and a counter at it; text stands on the font's baseline.
	/// </summary>
	public float Draw(CanvasItem item, CGuiFont font, string text, float x, float top, Color colour)
	{
		foreach (CTextPiece piece in CTextIcons.Split(text))
		{
			if (piece.Counter is not null)
			{
				if (Counter(piece.Counter) is { } counter)
				{
					Vector2 size = counter.GetSize();
					item.DrawTextureRectRegion(counter,
						new Rect2(Mathf.Round(x), Mathf.Round(top), CCounterIcons.Width, CCounterIcons.Height),
						new Rect2(size.X * CCounterIcons.Left, size.Y * CCounterIcons.Top,
							size.X * (CCounterIcons.Right - CCounterIcons.Left), size.Y * (CCounterIcons.Bottom - CCounterIcons.Top)));
				}
				x += CCounterIcons.Advance;
				continue;
			}
			if (piece.Tag is null)
			{
				font.Draw(item, new Vector2(Mathf.Round(x), Mathf.Round(top + font.Ascent)), piece.Text, colour);
				x += font.Width(piece.Text);
				continue;
			}
			if (Flag(piece.Tag) is { } flag)
			{
				item.DrawTextureRect(flag, new Rect2(Mathf.Round(x), Mathf.Round(top + CFlagIcons.BelowLineTop),
					CFlagIcons.Width, CFlagIcons.Height), false);
			}
			x += CFlagIcons.Advance;
		}
		return x;
	}

	/// <summary>
	/// A country's flag, from <c>gfx/flags</c>. The original draws it out of an atlas of all
	/// of them, which was not read; a tag nobody has draws nothing here, where the original
	/// draws the flag of the country numbered 1.
	/// </summary>
	public Texture2D? Flag(string tag)
	{
		if (!_flags.TryGetValue(tag, out Texture2D? flag))
		{
			string path = "gfx/flags/" + tag + ".tga";
			flag = tag.Length == CFlagIcons.TagLength && _textures.Exists(path)
				? _textures.LoadFromGamePath(path) : null;
			_flags[tag] = flag;
		}
		return flag;
	}

	/// <summary>The picture of the sprite <c>GFX_counter_</c> + name, or null where there is none - which draws nothing.</summary>
	public Texture2D? Counter(string name)
	{
		if (!_counters.TryGetValue(name, out Texture2D? counter))
		{
			counter = _sprites.Find(CCounterIcons.SpritePrefix + name) is { } sprite ? _textures.Load(sprite) : null;
			_counters[name] = counter;
		}
		return counter;
	}
}
