using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim;

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

	public CColor Color { get; init; } = CColor.Gray;

	public string GraphicalCulture { get; init; } = "";

	/// <summary>The id of the capital province, or 0 for a country that has none.</summary>
	public int Capital { get; set; }

	/// <summary>The level it has of each technology, by the technology's name. Absent means none.</summary>
	public Dictionary<string, int> TechnologyLevels { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// What its technologies are worth in all: each one's effect times its level. Kept
	/// apart from <see cref="Modifier"/>, as the original keeps it, because the two are
	/// applied differently. Rebuilt with it.
	/// </summary>
	public CModifier Technology { get; } = new();

	/// <summary>What it produces at home in a day. Written by <see cref="CResources"/>.</summary>
	public CGoodsPool HomeProduced { get; } = new();

	/// <summary>The law in force from each group, by the group's name.</summary>
	public Dictionary<string, CLaw> Laws { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Everyone the country's file declares, by the number it gives them.</summary>
	public Dictionary<int, CMinister> Ministers { get; } = new();

	/// <summary>Who holds each position, by the position's name.</summary>
	public Dictionary<string, CMinister> Government { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// The sum of everything that currently applies to the country. Rebuilt, never
	/// adjusted: see <see cref="CCountryModifiers"/>.
	/// </summary>
	public CModifier Modifier { get; } = new();

	/// <summary>
	/// The IC of its provinces, in whole IC, before any of the country's own bonuses.
	/// Written by <see cref="CIndustrialCapacity"/>.
	/// </summary>
	public int BaseIc { get; set; }

	/// <summary>
	/// The IC it has to spend, in whole IC. At least 1 as calculated, though a day's
	/// shortage of resources can cut it to nothing.
	/// </summary>
	public int TotalIc { get; set; }

	/// <summary>
	/// The fraction of its IC that goes to each of the six things IC is spent on, by
	/// <see cref="EProductionCategory"/>. Changed only by a command.
	/// </summary>
	public CFixed15[] ProductionShares { get; } = new CFixed15[CProduction.CategoryCount];

	/// <summary>The resources its industry used today. Cleared and counted again each day.</summary>
	public CGoodsPool Usage { get; } = new();

	/// <summary>How unhappy its people are. Never below nothing.</summary>
	public CFixedPoint Dissent { get; set; }

	/// <summary>Game-relative path to the flag, which is named after the tag.</summary>
	public string FlagFile => $"gfx/flags/{Tag}.tga";

	public override string ToString()
	{
		return $"{Tag} ({Name})";
	}
}
