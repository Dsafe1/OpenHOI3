using System;
using System.Globalization;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// A technology's <c>allow</c> block as a requirement tooltip writes it: a line for each
/// condition with a red or green asterisk for whether it holds, a header over a group, and
/// the inside of a group a level deeper. The indentation is the corrected one the mod's DLL
/// puts into the original, by the maintainer's word - the original's own leaves what is
/// inside a group flush with it (<c>docs/compatibility.md</c>, entry 10). See
/// <c>docs/mechanics/technologyscreen.md</c>, section 9.
/// </summary>
public static class CRequirementText
{
	/// <summary>One level of indent.</summary>
	private const string Indent = "   ";
	private const string Unmet = "(§R*§W)";
	private const string Met = "(§G*§W)";

	/// <summary>
	/// The whole block, for a country: an implicit "all of", at the top, where it draws no
	/// header and its conditions are not indented.
	/// </summary>
	public static string For(Func<string, string> localise, CGameState state, CCountry country, CResearchCondition allow)
	{
		return Block(localise, state, country, allow, false, 0);
	}

	/// <summary>
	/// A condition's own block, at a depth: a group's header and its lines; a leaf has none.
	/// <paramref name="unmetWhen"/> is what counts as unmet - false, and true inside a "none of".
	/// </summary>
	private static string Block(Func<string, string> localise, CGameState state, CCountry country,
		CResearchCondition condition, bool unmetWhen, int depth)
	{
		switch (condition.Kind)
		{
			case EResearchCondition.All:
				if (depth == 0)
				{
					return Lines(localise, state, country, condition, unmetWhen, 0);
				}
				return localise("AND_TRIGGER_STARTS") + ":\n" + Inside(localise, state, country, condition, unmetWhen, depth);
			case EResearchCondition.Any:
				return (depth == 0 ? Icon(state, country, condition, unmetWhen) : "")
					+ localise("OR_TRIGGER_STARTS") + ":\n" + Inside(localise, state, country, condition, unmetWhen, depth);
			case EResearchCondition.None:
				// A "none of" draws inside a line its parent has already placed, so the indent
				// its lines open with is taken off the front.
				return Lines(localise, state, country, condition, !unmetWhen, depth).TrimStart(' ');
			case EResearchCondition.OwnsProvinceWithBuilding:
				return (depth == 0 ? Icon(state, country, condition, unmetWhen) : "")
					+ localise("ANY_PROVINCE_STARTS") + " (" + country.Name + "):\n"
					+ BuildingLine(localise, state, condition, unmetWhen, depth + 1);
			default:
				return "";
		}
	}

	/// <summary>
	/// The plain list a group of conditions is written as at the top or inside a "none of":
	/// each condition with a line of its own indented to the depth and given its asterisk
	/// only at the top; a group indented and given its asterisk below the top, and then its
	/// own block.
	/// </summary>
	private static string Lines(Func<string, string> localise, CGameState state, CCountry country,
		CResearchCondition group, bool unmetWhen, int depth)
	{
		var text = new StringBuilder();
		foreach (CResearchCondition child in group.Children)
		{
			string line = Line(localise, state, child, unmetWhen);
			if (line.Length != 0)
			{
				text.Append(Indents(depth));
				if (depth == 0)
				{
					text.Append(Icon(state, country, child, unmetWhen));
				}
				text.Append(line);
			}
			else if (depth > 0 && IsGroup(child))
			{
				text.Append(Indents(depth)).Append(Icon(state, country, child, unmetWhen));
			}
			text.Append(Block(localise, state, country, child, unmetWhen, depth));
		}
		return text.ToString();
	}

	/// <summary>
	/// What is inside an "all of" or a "one of" below the top: each child a level deeper than
	/// the group, with its asterisk, its own block or else its line.
	/// </summary>
	private static string Inside(Func<string, string> localise, CGameState state, CCountry country,
		CResearchCondition group, bool unmetWhen, int depth)
	{
		var text = new StringBuilder();
		foreach (CResearchCondition child in group.Children)
		{
			string inner = Block(localise, state, country, child, unmetWhen, depth + 1);
			if (inner.Length == 0 && !IsGroup(child))
			{
				inner = Line(localise, state, child, unmetWhen);
			}
			if (inner.Length != 0)
			{
				text.Append(Indents(depth + 1)).Append(Icon(state, country, child, unmetWhen)).Append(inner);
			}
		}
		return text.ToString();
	}

	/// <summary>
	/// The one line a condition writes of itself, in the words its polarity asks for - the
	/// opposite inside a "none of". A group writes none.
	/// </summary>
	private static string Line(Func<string, string> localise, CGameState state, CResearchCondition condition, bool unmetWhen)
	{
		string amount = condition.Amount.ToInt().ToString(CultureInfo.InvariantCulture);
		switch (condition.Kind)
		{
			case EResearchCondition.TechnologyLevel:
			{
				string folder = state.Technologies.TryGetValue(condition.Name, out CTechnology? technology)
					? localise(technology.Folder)
					: "";
				return "§Y" + localise(condition.Name) + " (" + folder + ")§W"
					+ localise(unmetWhen ? "TECHNOLOGY_LOWER_THAN" : "TECHNOLOGY_HIGHER_THAN") + "§Y" + amount + "§W\n";
			}
			case EResearchCondition.Ports:
				return localise(unmetWhen ? "HAVE_LESS_PORTS_THAN" : "HAVE_MORE_PORTS_THAN") + "§Y" + amount + "§W\n";
			default:
				return "";
		}
	}

	/// <summary>
	/// The one condition inside "any owned province": the building, in the province of the
	/// scope it is asked in - which for a country is the province numbered nothing.
	/// </summary>
	private static string BuildingLine(Func<string, string> localise, CGameState state, CResearchCondition condition,
		bool unmetWhen, int depth)
	{
		string where = state.Provinces.Count > 0 ? state.Province(0).Name : "";
		string line = CTextTemplate.Fill(localise(unmetWhen ? "DONT_HAVE_A_X_INY" : "HAVE_A_X_INY"),
			("X", "§Y" + localise(condition.Name) + "§W"), ("Y", "§Y" + where + "§W")) + "\n";
		// A line below the top has no asterisk of its own here: what it is about is a
		// province, and the country is what it would be asked of.
		return Indents(depth) + line;
	}

	private static bool IsGroup(CResearchCondition condition)
	{
		return condition.Kind is EResearchCondition.All or EResearchCondition.Any or EResearchCondition.None
			or EResearchCondition.OwnsProvinceWithBuilding;
	}

	private static string Icon(CGameState state, CCountry country, CResearchCondition condition, bool unmetWhen)
	{
		return condition.Holds(state, country) == unmetWhen ? Unmet : Met;
	}

	private static string Indents(int depth)
	{
		var text = new StringBuilder();
		for (int level = 0; level < depth; level++)
		{
			text.Append(Indent);
		}
		return text.ToString();
	}
}
