using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Interface.Modifiers;

/// <summary>
/// A modifier written out as the original's tooltips write one: the sources of one kind,
/// a source's own line, and every value a modifier holds. See
/// <c>docs/mechanics/modifiers.md</c>, section 8.
/// </summary>
public static class CModifierText
{
	private const string White = "§W";

	/// <summary>
	/// The sources of one kind, a line each, in the order they were added. A source with
	/// nothing of the kind writes nothing, and no line break.
	/// </summary>
	public static string Sources(Func<string, string> localise, IEnumerable<CModifierSource> sources, string kind)
	{
		var text = new StringBuilder();
		int last = 0;
		foreach (CModifierSource source in sources)
		{
			if (text.Length > last)
			{
				text.Append('\n');
			}
			last = text.Length;
			text.Append(Line(localise, source, kind).Trim(Trimmed));
		}
		return text.ToString();
	}

	/// <summary>What the original trims off a source's line: line breaks, tabs, returns and spaces.</summary>
	private static readonly char[] Trimmed = { '\n', '\t', '\r', ' ' };

	/// <summary>
	/// One source's line for a kind, or nothing where it has none of it: its name, the
	/// value coloured by whether more is better, and the white that closes it. A province's
	/// is headed by its name.
	/// </summary>
	public static string Line(Func<string, string> localise, CModifierSource source, string kind)
	{
		CFixedPoint value = source.Modifier[kind];
		if (value.Raw == 0)
		{
			return "";
		}
		string name = source.Heading ?? localise(source.Modifier.Name);
		return name + ": " + Value(CModifierKinds.Of(kind), value);
	}

	/// <summary>Every value the modifier holds, a line each in the original's order of kinds, each under the kind's own name.</summary>
	public static string AllValues(Func<string, string> localise, CModifier modifier)
	{
		var lines = new List<string>();
		foreach ((string key, CFixedPoint value) in modifier.Values.OrderBy(entry => CModifierKinds.Order(entry.Key)))
		{
			if (value.Raw == 0)
			{
				continue;
			}
			CModifierKind kind = CModifierKinds.Of(key);
			lines.Add(localise(kind.DisplayKey) + ": " + Value(kind, value));
		}
		return string.Join("\n", lines);
	}

	/// <summary>
	/// A value of a kind: its colour, a plus where it is positive, the figure - a hundred
	/// times it to one place with a % for a percentage, else to two places - and white.
	/// </summary>
	public static string Value(CModifierKind kind, CFixedPoint value)
	{
		long raw = value.Raw;
		char colour = raw == 0 ? CColouredText.Yellow
			: (raw > 0) == kind.MoreIsBetter ? CColouredText.Green
			: CColouredText.Red;
		string sign = raw > 0 ? "+" : "";
		string figure = kind.Percent
			? CNumberText.Format(new CFixedPoint(raw * 100), 1) + "%"
			: CNumberText.Format(value, 2);
		return CColouredText.In(colour) + sign + figure + White;
	}
}
