using System;
using Godot;

namespace OpenHOI3.Game;

/// <summary>
/// A country's three-letter tag, the key everything else hangs off: history files,
/// flags, colours and every reference in the scenario data.
///
/// Like the date type, the original has no RTTI for this, so the name follows the
/// mangled luabind signatures in the binary, which spell it CCountryTag.
/// </summary>
public readonly record struct CCountryTag(string Value)
{
	public const int Length = 3;

	public bool IsValid => Value is { Length: Length };

	public static bool TryParse(string? text, out CCountryTag tag)
	{
		tag = default;
		if (text is null)
		{
			return false;
		}
		string trimmed = text.Trim();
		if (trimmed.Length != Length)
		{
			return false;
		}
		foreach (char c in trimmed)
		{
			// Tags are upper case letters, with the odd digit or underscore.
			if (!char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c) && c != '_')
			{
				return false;
			}
		}
		tag = new CCountryTag(trimmed);
		return true;
	}

	public override string ToString()
	{
		return Value ?? "";
	}
}

/// <summary>
/// One country, as far as the front end needs it: what it is called, what colour it
/// flies, and where its definition lives.
/// </summary>
public sealed class CCountry
{
	public required CCountryTag Tag { get; init; }

	/// <summary>Localised name; falls back to the tag when the string is missing.</summary>
	public required string Name { get; init; }

	/// <summary>Game-relative path from <c>common/countries.txt</c>.</summary>
	public required string DefinitionFile { get; init; }

	public Color Color { get; init; } = Colors.Gray;

	public string GraphicalCulture { get; init; } = "";

	/// <summary>Game-relative path to the flag, which is named after the tag.</summary>
	public string FlagFile => $"gfx/flags/{Tag}.tga";

	public override string ToString()
	{
		return $"{Tag} ({Name})";
	}
}
