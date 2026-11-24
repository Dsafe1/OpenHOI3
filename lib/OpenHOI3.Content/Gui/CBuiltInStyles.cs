using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// The styles a built-in element may name: the engine's own, each of which a mod's
/// <c>interface/openhoi/builtin_styles.txt</c> may change key by key, and those the file adds. See
/// <c>docs/mechanics/builtin.md</c>, section 3.
/// </summary>
public sealed class CBuiltInStyles
{
	/// <summary>The mod's file, in the folder the original does not open.</summary>
	public const string FilePath = "interface/openhoi/builtin_styles.txt";

	/// <summary>The style an element gets where it names none, or one nothing has.</summary>
	public const string Flat = "flat";

	private static readonly string[] Keys = { "fill", "border", "border_width", "bevel", "gradient", "hollow" };

	private static readonly CColor Ink = Rgb(40, 34, 20);

	private readonly Dictionary<string, CBuiltInStyle> _styles = new(StringComparer.OrdinalIgnoreCase);

	public CBuiltInStyles()
	{
		foreach (CBuiltInStyle style in Defaults())
		{
			_styles[style.Name] = style;
		}
	}

	/// <summary>The engine's styles, in the colours of the original's panels.</summary>
	private static IEnumerable<CBuiltInStyle> Defaults()
	{
		// A plain rectangle of one colour: a mark, a swatch, a bar.
		yield return new CBuiltInStyle { Name = Flat, Fill = CColor.White };
		// What a window or a group of rows stands on.
		yield return new CBuiltInStyle { Name = "panel", Fill = Rgb(200, 186, 142), Border = Ink, BorderWidth = 1, Bevel = 0.18f, Gradient = 0.06f };
		// Something pressed.
		yield return new CBuiltInStyle { Name = "button", Fill = Rgb(188, 166, 104), Border = Ink, BorderWidth = 1, Bevel = 0.35f, Gradient = 0.18f };
		// A line of a list.
		yield return new CBuiltInStyle { Name = "row", Fill = Rgb(218, 203, 154), Border = Rgb(132, 118, 80), BorderWidth = 1, Gradient = 0.10f };
		// The dark ribbon over a group.
		yield return new CBuiltInStyle { Name = "header", Fill = Rgb(72, 74, 44), Border = Rgb(16, 16, 10), BorderWidth = 1, Bevel = 0.20f, Gradient = 0.12f };
		// A thin line between two things.
		yield return new CBuiltInStyle { Name = "divider", Fill = Ink };
		// A border around what lies under it.
		yield return new CBuiltInStyle { Name = "frame", Border = Ink, BorderWidth = 2, Hollow = true };
		// A sunken field: a value, a text typed into.
		yield return new CBuiltInStyle { Name = "field", Fill = Rgb(232, 222, 188), Border = Ink, BorderWidth = 1, Bevel = -0.25f };
	}

	/// <summary>Every style's name, the engine's first in their order and then the file's.</summary>
	public IEnumerable<string> Names => _styles.Keys;

	/// <summary>The style of that name, whatever its case, or none.</summary>
	public CBuiltInStyle? Find(string name) => _styles.TryGetValue(name, out CBuiltInStyle? style) ? style : null;

	/// <summary>The engine's styles with the mod's file laid over them, where the folders have one.</summary>
	public static CBuiltInStyles Load(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var styles = new CBuiltInStyles();
		string path = directories.Resolve(FilePath);
		if (!File.Exists(path))
		{
			return styles;
		}
		try
		{
			styles.Read(ClausewitzParser.ParseFile(path), path, warn);
		}
		catch (Exception e) when (e is IOException or FormatException)
		{
			warn(CDiagnostics.Located(path, 0, $"could not be read: {e.Message}"));
		}
		return styles;
	}

	/// <summary>The engine's styles with a text laid over them. For tests, which have no folder.</summary>
	public static CBuiltInStyles Parse(string text, Action<string>? warn = null)
	{
		var styles = new CBuiltInStyles();
		styles.Read(ClausewitzParser.Parse(text), "text", warn ?? CDiagnostics.Problem);
		return styles;
	}

	/// <summary>
	/// A block a style: one the engine has is changed in the keys the block gives and keeps the rest; another name is a
	/// new style, begun as <see cref="Flat"/>. A key that is none of a style's is reported.
	/// </summary>
	private void Read(ClausewitzNode root, string file, Action<string> warn)
	{
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			CBuiltInStyle style = Find(block.Key) ?? new CBuiltInStyle { Name = block.Key.ToLowerInvariant() };
			foreach (ClausewitzNode child in block.Children)
			{
				if (child.Key is not null && !Keys.Contains(child.Key.ToLowerInvariant()))
				{
					warn(CDiagnostics.Located(file, child.Line, $"a built-in style does not take '{child.Key}'"));
				}
			}
			_styles[style.Name] = style with
			{
				Fill = Colour(block, "fill") ?? style.Fill,
				Border = Colour(block, "border") ?? style.Border,
				BorderWidth = Math.Max(0, block.GetInt("border_width", style.BorderWidth)),
				Bevel = Math.Clamp(block.GetFloat("bevel", style.Bevel), -1f, 1f),
				Gradient = Math.Clamp(block.GetFloat("gradient", style.Gradient), 0f, 1f),
				Hollow = block.GetBool("hollow", style.Hollow),
			};
		}
	}

	/// <summary>
	/// A colour as <c>{ r = 0 g = 255 b = 0 }</c>, each part 0 to 255, with <c>a</c> beside them where it is not solid -
	/// or the three or four numbers bare, as the original's colours are written. None where the key is not there.
	/// </summary>
	public static CColor? Colour(ClausewitzNode node, string key)
	{
		if (node.Get(key) is not { IsBlock: true } block)
		{
			return null;
		}
		if (block.Get("r") is not null || block.Get("g") is not null || block.Get("b") is not null)
		{
			return new CColor(Part(block, "r", 0f), Part(block, "g", 0f), Part(block, "b", 0f), Part(block, "a", 255f));
		}
		var bare = new List<float>();
		foreach (ClausewitzNode child in block.Children)
		{
			if (child.Key is null && child.Value is not null
				&& float.TryParse(child.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float part))
			{
				bare.Add(Math.Clamp(part, 0f, 255f) / 255f);
			}
		}
		return bare.Count < 3 ? null : new CColor(bare[0], bare[1], bare[2], bare.Count > 3 ? bare[3] : 1f);
	}

	private static float Part(ClausewitzNode block, string key, float fallback)
	{
		return Math.Clamp(block.GetFloat(key, fallback), 0f, 255f) / 255f;
	}

	private static CColor Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
}
