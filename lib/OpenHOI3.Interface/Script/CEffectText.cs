using System;
using System.Collections.Generic;
using System.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Interface.Script;

/// <summary>
/// A block of effects as the original writes it for an option's tooltip, under an event's
/// description and for a decision's button: each effect's line, a scope's head, the blocks over a
/// set once for each member, the chance of a <c>random</c>, the entries of a <c>random_list</c>. The
/// indent is three spaces a level, put before a block's own line and before each of its effects, so
/// a line at depth d stands 6d spaces in - the original's, and not corrected. See
/// <c>docs/mechanics/scripttext.md</c>, section 4.
/// </summary>
public static class CEffectText
{
	private const string Indent = "   ";

	/// <summary>A list of effects - an option's, a decision's - at depth 0, trimmed.</summary>
	public static string For(CScriptWords words, IReadOnlyList<CEffect> effects, CEventScope scope)
	{
		return Base(words, "", effects, scope, 0, "").Trim();
	}

	/// <summary>
	/// The original's base: the indent and the block's own line where it is longer than a character,
	/// then each effect's block with the indent before it. Effects of one keyword that follow each
	/// other and name a target give the last of them the others' names before its own.
	/// </summary>
	private static string Base(CScriptWords words, string own, IReadOnlyList<CEffect> children, CEventScope scope, int depth,
		string group)
	{
		string indent = Indents(depth);
		var text = new StringBuilder();
		if (own.Length > 1)
		{
			text.Append(indent).Append(own);
		}
		string gathered = "";
		for (int i = 0; i < children.Count; i++)
		{
			CEffect child = children[i];
			string label = CEffectLines.Label(words, child, scope);
			if (i + 1 < children.Count && SameKeyword(child, children[i + 1]) && label.Length > 0)
			{
				gathered += label + "§W,§Y ";
				continue;
			}
			text.Append(indent).Append(Block(words, child, scope, depth, gathered));
			gathered = "";
		}
		return text.ToString();
	}

	/// <summary>An effect's whole block: a leaf's line through the base, or a block's own form.</summary>
	private static string Block(CScriptWords words, CEffect effect, CEventScope scope, int depth, string group)
	{
		switch (effect.Kind)
		{
			case EEffectKind.Scope:
				return Context(words, effect, scope, depth);
			case EEffectKind.Region:
				return Region(words, effect, scope, depth);
			case EEffectKind.DeadScope:
			case EEffectKind.Weighted:
				return "";
		}
		switch (effect.Keyword)
		{
			case "any_owned":
			case "any_controlled":
			case "any_nearby_province":
			case "any_neighbor_province":
				return EachProvince(words, effect, scope, depth);
			case "any_country":
			case "any_neighbor_country":
				return EachCountry(words, effect, scope, depth);
			case "random_country":
			case "random_owned":
			case "random_neighbor_province":
			case "random_empty_neighbor_province":
				return OneOf(words, effect, scope, depth);
			case "random":
				return Random(words, effect, scope, depth);
			case "random_list":
				return Indents(depth) + RandomList(words, effect, scope);
			default:
				return Base(words, CEffectLines.Line(words, effect, scope, group), effect.Children, scope, depth, group);
		}
	}

	/// <summary>
	/// A scope: "(Name): " where it is a country not the player's, then its effects a level deeper in
	/// it - nothing at all where they say nothing, and a break after the head where they have none.
	/// </summary>
	private static string Context(CScriptWords words, CEffect effect, CEventScope scope, int depth)
	{
		CEventScope? named = CScopeSwitch.Narrow(words.State, scope, effect.Argument, null);
		string head = named?.Country is { } country && country != words.Player ? "(" + words.Country(country) + "): " : "";
		string indent = Indents(depth);
		var children = new StringBuilder();
		foreach (CEffect child in effect.Children)
		{
			children.Append(indent).Append(Block(words, child, named ?? scope.With(null, 0), depth + 1, ""));
		}
		string inner = children.ToString();
		if (inner.Trim().Length == 0)
		{
			return "";
		}
		if (!inner.Contains('\n'))
		{
			head += "\n";
		}
		return indent + head + inner;
	}

	/// <summary>The block's effects once for each province that passes its limit, in that province; no header.</summary>
	private static string EachProvince(CScriptWords words, CEffect effect, CEventScope scope, int depth)
	{
		var text = new StringBuilder();
		foreach (int province in CEffectSets.Provinces(words.State, effect, scope))
		{
			CEventScope member = scope.With(scope.Country, province);
			if (CEffects.Passes(words.State, effect, member, null))
			{
				text.Append(Base(words, "", effect.Children, member, depth, ""));
			}
		}
		return text.ToString();
	}

	/// <summary>
	/// Each country but the scope's own: "(Name): " and its effects. <c>any_country</c> leaves out a
	/// country that owns nothing, which the block itself does not.
	/// </summary>
	private static string EachCountry(CScriptWords words, CEffect effect, CEventScope scope, int depth)
	{
		IReadOnlyList<CCountry> countries = effect.Keyword == "any_country"
			? words.State.Countries
			: words.State.Country(scope.Country) is { } own ? CCountryLists.Neighbours(words.State, own) : new List<CCountry>();
		var text = new StringBuilder();
		foreach (CCountry other in countries)
		{
			if (other.Tag == scope.Country
				|| (effect.Keyword == "any_country" && CCountryLists.OwnedCount(words.State, other.Tag) == 0))
			{
				continue;
			}
			CEventScope member = scope.With(other.Tag, scope.Province);
			if (CEffects.Passes(words.State, effect, member, null))
			{
				text.Append("(" + other.Name + "): \n").Append(Base(words, "", effect.Children, member, depth, ""));
			}
		}
		return text.ToString();
	}

	/// <summary>The one member a draw picks, the draw moving the tooltip scope's chance on; no header.</summary>
	private static string OneOf(CScriptWords words, CEffect effect, CEventScope scope, int depth)
	{
		if (effect.Keyword == "random_country")
		{
			var countries = new List<CCountryTag>();
			foreach (CCountry other in words.State.Countries)
			{
				if (other.Tag != scope.Country && CCountryLists.OwnedCount(words.State, other.Tag) > 0
					&& CEffects.Passes(words.State, effect, scope.With(other.Tag, scope.Province), null))
				{
					countries.Add(other.Tag);
				}
			}
			if (countries.Count == 0)
			{
				return "";
			}
			CCountryTag drawn = countries[scope.Draw(countries.Count)];
			return Base(words, "", effect.Children, scope.With(drawn, scope.Province), depth, "");
		}
		var provinces = new List<int>();
		foreach (int province in CEffectSets.Provinces(words.State, effect, scope))
		{
			if (CEffects.Passes(words.State, effect, scope.With(scope.Country, province), null))
			{
				provinces.Add(province);
			}
		}
		if (provinces.Count == 0)
		{
			return "";
		}
		int picked = provinces[scope.Draw(provinces.Count)];
		return Base(words, "", effect.Children, scope.With(scope.Country, picked), depth, "");
	}

	/// <summary>"30% chance of" and its effects at depth 0, in brackets where there is more than one.</summary>
	private static string Random(CScriptWords words, CEffect effect, CEventScope scope, int depth)
	{
		CFixedPoint chance = CEffects.Field(effect, "chance")?.Amount ?? CFixedPoint.Zero;
		bool several = effect.Children.Count > 1;
		return CScriptWords.Yellow + CScriptWords.Trimmed(chance) + CScriptWords.White + "% chance of "
			+ (several ? " (\n" : "") + Indents(depth) + Base(words, "", effect.Children, scope, 0, "")
			+ (several ? ")" : "") + "\n";
	}

	/// <summary>"One of n options will happen." and each entry's chance - its weight over the total, whole - and effects.</summary>
	private static string RandomList(CScriptWords words, CEffect effect, CEventScope scope)
	{
		long total = 0;
		foreach (CEffect entry in effect.Children)
		{
			total += CConditions.WholeOf(entry.Argument.Amount);
		}
		var text = new StringBuilder(words.T("RANDOM_LIST_EFFECT", ("SIZE", CScriptWords.Whole(effect.Children.Count))));
		foreach (CEffect entry in effect.Children)
		{
			long chance = total > 0 ? CConditions.WholeOf(entry.Argument.Amount) * 100 / total : 0;
			text.Append(words.T("RANDOM_EFFECT", ("CHANCE", CScriptWords.Whole(chance))));
			string inner = Base(words, "", entry.Children, scope, 0, "");
			text.Append(inner.Length > 1 ? inner : words.Localise("NO_EFFECT")).Append('\n');
		}
		return text.ToString();
	}

	/// <summary>A region: its name and ": ", a break where its effects make two lines or more, and them, in province none.</summary>
	private static string Region(CScriptWords words, CEffect effect, CEventScope scope, int depth)
	{
		var named = new CTrigger(ETriggerKind.Region, effect.Key, CScriptArgument.None);
		string inner = Base(words, "", effect.Children, scope.With(scope.Country, 0), depth + 1, "");
		return CConditionText.RegionName(words, named) + ": " + (inner.Split('\n').Length > 2 ? "\n" : "") + inner;
	}

	/// <summary>Whether two effects were made by one keyword - the original's test of folding them onto one line.</summary>
	private static bool SameKeyword(CEffect one, CEffect other)
	{
		return one.Kind == other.Kind && (one.Kind == EEffectKind.UnitType || one.Keyword == other.Keyword);
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
