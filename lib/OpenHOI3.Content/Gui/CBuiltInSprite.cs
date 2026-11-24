using System;
using System.Globalization;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// What an element's <c>built_in = { style = panel size = { x = 300 y = 120 } color = { r = 0 g = 255 b = 0 } }</c>
/// asks for: a picture the engine paints, where a <c>spriteType</c> names one a file holds. It stands in the element's
/// sprite as a name of its own shape, which says all of it, so that everything that draws a sprite draws it. See
/// <c>docs/mechanics/builtin.md</c>.
/// </summary>
public sealed record CBuiltInSprite(string Style, int Width, int Height, CColor? Colour)
{
	/// <summary>The key an icon or a button takes it under.</summary>
	public const string Key = "built_in";

	/// <summary>What a sprite name begins with that is one of these and no sprite of a <c>.gfx</c> file.</summary>
	public const string Prefix = "openhoi_builtin:";

	/// <summary>The largest side a built-in picture may have.</summary>
	public const int MostSide = 4096;

	private static readonly string[] Keys = { "style", "size", "color", "colour" };

	/// <summary>The sprite name that says this: the style, the two sides, and the colour as eight hex digits or a dash.</summary>
	public string Name => Prefix + Style.ToLowerInvariant() + ":" + Width.ToString(CultureInfo.InvariantCulture) + ":"
		+ Height.ToString(CultureInfo.InvariantCulture) + ":" + (Colour is { } colour ? Hex(colour) : "-");

	/// <summary>Whether a sprite name is a built-in picture's.</summary>
	public static bool Is(string name) => name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

	/// <summary>What a built-in picture's name says, or none where it is not one or is malformed.</summary>
	public static CBuiltInSprite? FromName(string name)
	{
		if (!Is(name))
		{
			return null;
		}
		string[] parts = name[Prefix.Length..].Split(':');
		if (parts.Length != 4 || parts[0].Length == 0
			|| !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int width)
			|| !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int height)
			|| width < 1 || height < 1 || width > MostSide || height > MostSide)
		{
			return null;
		}
		CColor? colour = null;
		if (parts[3] != "-")
		{
			if (parts[3].Length != 8 || !uint.TryParse(parts[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint packed))
			{
				return null;
			}
			colour = new CColor((packed >> 24) / 255f, ((packed >> 16) & 0xFF) / 255f, ((packed >> 8) & 0xFF) / 255f, (packed & 0xFF) / 255f);
		}
		return new CBuiltInSprite(parts[0], width, height, colour);
	}

	/// <summary>
	/// Reads an element's block. A style nothing has is reported and painted as <see cref="CBuiltInStyles.Flat"/>; a
	/// size that is missing, under one or over <see cref="MostSide"/> is reported and held within them.
	/// </summary>
	public static CBuiltInSprite Read(ClausewitzNode block, CBuiltInStyles styles, Action<int, string> complain)
	{
		foreach (ClausewitzNode child in block.Children)
		{
			if (child.Key is not null && Array.IndexOf(Keys, child.Key.ToLowerInvariant()) < 0)
			{
				complain(child.Line, $"{Key} does not take '{child.Key}'");
			}
		}
		string style = block.GetString("style", CBuiltInStyles.Flat);
		if (styles.Find(style) is null)
		{
			complain(block.Line, $"{Key} names the style '{style}', which is none: {string.Join(", ", styles.Names)}");
			style = CBuiltInStyles.Flat;
		}
		ClausewitzNode? size = block.Get("size");
		int width = (int)(size?.GetFloat("x") ?? 0f);
		int height = (int)(size?.GetFloat("y") ?? 0f);
		if (width < 1 || height < 1 || width > MostSide || height > MostSide)
		{
			complain(block.Line, $"{Key} needs a size = {{ x = .. y = .. }} of 1 to {MostSide} each way");
			width = Math.Clamp(width, 1, MostSide);
			height = Math.Clamp(height, 1, MostSide);
		}
		return new CBuiltInSprite(style, width, height, CBuiltInStyles.Colour(block, "color") ?? CBuiltInStyles.Colour(block, "colour"));
	}

	private static string Hex(CColor colour)
	{
		static uint Byte(float part) => (uint)Math.Clamp((int)MathF.Round(part * 255f), 0, 255);
		uint packed = Byte(colour.R) << 24 | Byte(colour.G) << 16 | Byte(colour.B) << 8 | Byte(colour.A);
		return packed.ToString("x8", CultureInfo.InvariantCulture);
	}
}
