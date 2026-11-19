using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Clausewitz;

/// <summary>
/// One entry in a Clausewitz script file. Either a scalar (<c>key = value</c>) or a
/// block (<c>key = { ... }</c>).
///
/// Keys are not unique - a window declares many <c>guiButtonType</c> entries - so
/// children are kept as an ordered list rather than a dictionary, and lookups are
/// case-insensitive because the game files spell keys inconsistently
/// (<c>Orientation</c> vs <c>orientation</c>, <c>textureFile</c> vs <c>texturefile</c>).
/// </summary>
public sealed class ClausewitzNode
{
	public string? Key { get; init; }
	public string? Value { get; init; }

	/// <summary>Whether the value was written in quotes, which an interface file's <c>format</c> cares about.</summary>
	public bool IsQuoted { get; init; }

	/// <summary>
	/// The line of the file the entry begins on, counted from one - its key's, or a bare value's own - for a report of a
	/// fault in it (<c>docs/mechanics/logs.md</c>, section 4); nought for a node not read from a text.
	/// </summary>
	public int Line { get; init; }

	public List<ClausewitzNode> Children { get; } = new();

	public bool IsBlock => Value is null;

	public ClausewitzNode? Get(string key)
	{
		foreach (ClausewitzNode child in Children)
		{
			if (string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase))
			{
				return child;
			}
		}
		return null;
	}

	public IEnumerable<ClausewitzNode> All(string key)
	{
		foreach (ClausewitzNode child in Children)
		{
			if (string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase))
			{
				yield return child;
			}
		}
	}

	public string GetString(string key, string fallback = "")
	{
		return Get(key)?.Value ?? fallback;
	}

	public float GetFloat(string key, float fallback = 0f)
	{
		string? raw = Get(key)?.Value;
		return raw is not null &&
			float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
			? v : fallback;
	}

	public int GetInt(string key, int fallback = 0)
	{
		return (int)GetFloat(key, fallback);
	}

	public bool GetBool(string key, bool fallback = false)
	{
		string? raw = Get(key)?.Value;
		if (raw is null)
		{
			return fallback;
		}
		return raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
			raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1";
	}

	/// <summary>Reads a <c>{ x = .. y = .. }</c> block.</summary>
	public Vector2 GetVector2(string key, Vector2 fallback = default)
	{
		ClausewitzNode? node = Get(key);
		if (node is null || !node.IsBlock)
		{
			return fallback;
		}
		return new Vector2(node.GetFloat("x", fallback.X), node.GetFloat("y", fallback.Y));
	}

	/// <summary>
	/// Reads a <c>{ r g b }</c> list of 0-255 components, the shape the game data uses
	/// for country, unit and centre-of-trade colours.
	/// </summary>
	public CColor GetRgb(string key, CColor fallback = default)
	{
		ClausewitzNode? node = Get(key);
		if (node is null || !node.IsBlock)
		{
			return fallback;
		}

		Span<float> parts = stackalloc float[3];
		int found = 0;
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Value is null || found == 3)
			{
				continue;
			}
			if (float.TryParse(child.Value, NumberStyles.Float, CultureInfo.InvariantCulture,
				out float component))
			{
				parts[found++] = component;
			}
		}
		if (found < 3)
		{
			return fallback;
		}
		// Components are written as bytes, but a few files use 0-1 floats instead.
		float scale = parts[0] > 1f || parts[1] > 1f || parts[2] > 1f ? 255f : 1f;
		return new CColor(parts[0] / scale, parts[1] / scale, parts[2] / scale);
	}

	/// <summary>
	/// Reads an interface type's colour, <c>{ r g b }</c> or <c>{ r g b a }</c>, as the original's colour reads one: each of
	/// the first three divided by 255 where it is above one, on its own; the alpha one unless a fourth number follows, which
	/// is taken as written (<c>reversing/findings/FINDINGS-progressfx.md</c>, section 1). The fallback where there are not
	/// three numbers.
	/// </summary>
	public CColor GetInterfaceColor(string key, CColor fallback = default)
	{
		return Get(key) is { } node ? InterfaceColor(node, fallback) : fallback;
	}

	/// <summary><see cref="GetInterfaceColor"/> of a block in hand, for a list whose keys differ only by case.</summary>
	public static CColor InterfaceColor(ClausewitzNode node, CColor fallback = default)
	{
		if (!node.IsBlock)
		{
			return fallback;
		}
		var parts = new List<float>(4);
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Value is not null && parts.Count < 4
				&& float.TryParse(child.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float component))
			{
				parts.Add(component);
			}
		}
		if (parts.Count < 3)
		{
			return fallback;
		}
		static float Channel(float value) => value > 1f ? value / 255f : value;
		return new CColor(Channel(parts[0]), Channel(parts[1]), Channel(parts[2]), parts.Count == 4 ? parts[3] : 1f);
	}

	/// <summary>Reads an <c>0xAARRGGBB</c> literal as used by <c>bitmapfont</c> entries.</summary>
	public CColor GetColor(string key, CColor fallback)
	{
		string raw = GetString(key);
		if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			raw = raw[2..];
		}
		if (!uint.TryParse(raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
		{
			return fallback;
		}
		return new CColor(
			((v >> 16) & 0xFF) / 255f,
			((v >> 8) & 0xFF) / 255f,
			(v & 0xFF) / 255f,
			((v >> 24) & 0xFF) / 255f);
	}

	public override string ToString()
	{
		return IsBlock ? $"{Key} = {{ {Children.Count} }}" : $"{Key} = {Value}";
	}
}
