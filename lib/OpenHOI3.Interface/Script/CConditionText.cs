using System;
using System.Collections.Generic;
using System.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Interface.Script;

/// <summary>
/// A block of conditions as the original writes it for a decision's <c>allow</c>: a line for each
/// condition with a green or red asterisk for whether it holds, a header over a block and the block's
/// conditions a level deeper, the words turned round inside a <c>not</c>. The indentation is the
/// corrected one the mod's DLL puts into the original (<c>docs/compatibility.md</c>, entry 10). See
/// <c>docs/mechanics/scripttext.md</c>, section 3.
/// </summary>
public static class CConditionText
{
	/// <summary>One level of indent.</summary>
	private const string Indent = "   ";
	private const string Unmet = "(§R*§W)";
	private const string Met = "(§G*§W)";

	/// <summary>The whole block, at the top: a plain list, no header, every condition with its asterisk.</summary>
	public static string For(CScriptWords words, CTrigger block, CEventScope scope)
	{
		return Base(words, block.Children, scope, false, 0);
	}

	/// <summary>
	/// The original's base: each condition's line, indented and with its asterisk where the depth is
	/// nought or came in below nought, a block's header and asterisk below the top, and then each
	/// condition's own block at the same depth. A region empties the province of the scope for what
	/// follows it.
	/// </summary>
	private static string Base(CScriptWords words, IReadOnlyList<CTrigger> children, CEventScope scope, bool redWhen, int depth,
		bool insideNot = false)
	{
		bool asterisks = depth <= 0;
		depth = Math.Abs(depth);
		var text = new StringBuilder();
		CEventScope current = scope;
		foreach (CTrigger child in children)
		{
			string line = CConditionLines.Line(words, child, current, redWhen);
			if (line.Length != 0)
			{
				text.Append(Indents(depth));
				if (asterisks)
				{
					text.Append(Icon(words, child, current, redWhen));
				}
				text.Append(line);
			}
			else if (depth > 0 && IsBlock(child))
			{
				// Not inside a "none of", whose own asterisk stands for what it holds - the maintainer saw two.
				text.Append(Indents(depth)).Append(insideNot ? "" : Icon(words, child, current, redWhen));
			}
			text.Append(Block(words, child, current, redWhen, depth));
			if (child.Kind == ETriggerKind.Region && RegionHasProvinces(words, child))
			{
				current = current.With(current.Country, 0);
			}
		}
		return text.ToString();
	}

	/// <summary>A condition's own block: a block's header and its conditions; a leaf has none.</summary>
	private static string Block(CScriptWords words, CTrigger trigger, CEventScope scope, bool redWhen, int depth)
	{
		switch (trigger.Kind)
		{
			case ETriggerKind.Scope:
				return Context(words, trigger, scope, redWhen, depth);
			case ETriggerKind.Region:
				return Header(words, trigger, scope, redWhen, depth, RegionName(words, trigger) + ":\n")
					+ Base(words, trigger.Children, RegionHasProvinces(words, trigger) ? scope.With(scope.Country, 0) : scope,
						redWhen, depth + 1);
			case ETriggerKind.Keyword:
				break;
			default:
				return "";
		}
		switch (trigger.Keyword)
		{
			case "and":
			case "enemy":
				if (depth == 0)
				{
					return Base(words, trigger.Children, scope, redWhen, 0);
				}
				return words.Localise("AND_TRIGGER_STARTS") + ":\n" + Inside(words, trigger, scope, redWhen, depth);
			case "or":
				return Header(words, trigger, scope, redWhen, depth, words.Localise("OR_TRIGGER_STARTS") + ":\n")
					+ Inside(words, trigger, scope, redWhen, depth);
			case "not":
				// It draws inside a line its parent has already placed.
				return Base(words, trigger.Children, scope, !redWhen, depth, insideNot: true).TrimStart(' ');
			case "any_owned_province":
				return Header(words, trigger, scope, redWhen, depth,
						words.Localise("ANY_PROVINCE_STARTS") + " (" + words.Country(scope.Country) + "):\n")
					+ Base(words, trigger.Children, scope, redWhen, depth + 1);
			case "any_neighbor_province":
				return Header(words, trigger, scope, redWhen, depth, words.Localise("NEIGHBOR_PROVINCE_STARTS") + ":\n")
					+ Base(words, trigger.Children, scope, redWhen, depth + 1);
			case "any_core":
				return Header(words, trigger, scope, redWhen, depth, words.Localise("ANY_CORE_STARTS") + ":\n")
					+ Base(words, trigger.Children, scope, redWhen, depth + 1);
			case "any_neighbor_country":
				return Header(words, trigger, scope, redWhen, depth, words.Localise("NEIGHBOR_COUNTRY_STARTS") + ":\n")
					+ Base(words, trigger.Children, scope, redWhen, depth + 1);
			default:
				return "";
		}
	}

	/// <summary>A header, with its own asterisk at the top only; it is not indented, its parent having placed it.</summary>
	private static string Header(CScriptWords words, CTrigger trigger, CEventScope scope, bool redWhen, int depth, string header)
	{
		return (depth == 0 ? Icon(words, trigger, scope, redWhen) : "") + header;
	}

	/// <summary>The conditions of an "all of" or a "one of" below the top, each a level deeper with its asterisk.</summary>
	private static string Inside(CScriptWords words, CTrigger group, CEventScope scope, bool redWhen, int depth)
	{
		var text = new StringBuilder();
		foreach (CTrigger child in group.Children)
		{
			string inner = Block(words, child, scope, redWhen, depth + 1);
			if (inner.Length == 0 && !IsBlock(child))
			{
				inner = CConditionLines.Line(words, child, scope, redWhen);
			}
			if (inner.Length != 0)
			{
				text.Append(Indents(depth + 1)).Append(Icon(words, child, scope, redWhen)).Append(inner);
			}
		}
		return text.ToString();
	}

	/// <summary>
	/// A scope - a tag, <c>FROM</c>, <c>owner</c>: its name, and its conditions written once in the scope
	/// it names; two lines or more go under it a level deeper with their asterisks, one line goes after
	/// the name, its asterisk taken off and its first letter lowered.
	/// </summary>
	private static string Context(CScriptWords words, CTrigger trigger, CEventScope scope, bool redWhen, int depth)
	{
		string name = (depth == 0 ? Icon(words, trigger, scope, redWhen) : "") + ScopeName(words, trigger);
		CEventScope named = CScopeSwitch.Narrow(words.State, scope, trigger.Argument, null) ?? scope.With(null, 0);
		string once = Base(words, trigger.Children, named, redWhen, 0);
		if (once.Split('\n').Length > 2)
		{
			return name + ":\n" + Base(words, trigger.Children, named, redWhen, -(depth + 1));
		}
		string line = once;
		if (line.Length > 8 && (line.StartsWith(Unmet, StringComparison.Ordinal) || line.StartsWith(Met, StringComparison.Ordinal)))
		{
			line = line[Unmet.Length..];
		}
		if (line.Length > 0)
		{
			line = char.ToLowerInvariant(line[0]) + line[1..];
		}
		return name + " " + line;
	}

	/// <summary>What a scope is called: a tag's country, a province's name, or the word's key.</summary>
	private static string ScopeName(CScriptWords words, CTrigger trigger)
	{
		CScriptArgument named = trigger.Argument;
		string key = named.Text.ToLowerInvariant();
		string? word = key switch
		{
			"owner" => "TRIGGER_OWNER",
			"controller" => "TRIGGER_CONTROLLER",
			"emperor" => "TRIGGER_EMPEROR",
			"from" => "TRIGGER_FROM",
			"ally" => "TRIGGER_ALLY",
			"this" => "TRIGGER_THIS",
			"capital_scope" => "TRIGGER_CAPITAL",
			"sea_zone" => "TRIGGER_SEA_ZONE",
			"local_enemy" => "TRIGGER_LOCAL_ENEMY",
			"revolution_target" => "TRIGGER_REVOLUTION_TARGET",
			"overlord" => "TRIGGER_OVERLORD",
			_ => null,
		};
		if (word is not null)
		{
			return words.Localise(word);
		}
		if (named.Province != 0)
		{
			return words.Province(named.Province);
		}
		return CEventScope.Known(named.Tag) is { } tag ? words.Country(tag) : "";
	}

	/// <summary>A region's name: its own text, or "$NAME$ Region" with its first province where it has none.</summary>
	internal static string RegionName(CScriptWords words, CTrigger trigger)
	{
		foreach (CRegion region in words.State.Regions)
		{
			if (!region.Name.Equals(trigger.Key, StringComparison.Ordinal))
			{
				continue;
			}
			string name = words.Localise(region.Name);
			if (name == region.Name && region.Provinces.Count > 0)
			{
				return words.T("REGION_NAME", ("NAME", words.Province(region.Provinces[0])));
			}
			return name;
		}
		return trigger.Key;
	}

	private static bool RegionHasProvinces(CScriptWords words, CTrigger trigger)
	{
		foreach (CRegion region in words.State.Regions)
		{
			if (region.Name.Equals(trigger.Key, StringComparison.Ordinal))
			{
				return region.Provinces.Count > 0;
			}
		}
		return false;
	}

	private static bool IsBlock(CTrigger trigger)
	{
		return trigger.Kind is ETriggerKind.Scope or ETriggerKind.Region
			|| trigger.Keyword is "and" or "or" or "not" or "enemy" or "any_owned_province" or "any_neighbor_province"
				or "any_core" or "any_neighbor_country";
	}

	/// <summary>Red where whether it holds is what counts as unmet - false, and true inside a <c>not</c>.</summary>
	private static string Icon(CScriptWords words, CTrigger trigger, CEventScope scope, bool redWhen)
	{
		return CConditions.Holds(words.State, trigger, scope) == redWhen ? Unmet : Met;
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
