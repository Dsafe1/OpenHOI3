using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Reads an effect block: each key a keyword, a name the original looks up, or - written with a
/// block - a scope, and anything else a problem. <c>docs/mechanics/script.md</c>, section 6.
/// </summary>
public static class CEffectReader
{
	/// <summary>The words an effect's block may be scoped to, besides a country and a province.</summary>
	private static readonly string[] ScopeWords =
		{ "owner", "controller", "from", "ally", "this", "capital_scope", "sea_zone", "local_enemy", "overlord" };

	private static readonly EScriptName[] AllLists =
	{
		EScriptName.GovernmentPosition, EScriptName.Building, EScriptName.Ideology, EScriptName.Technology,
		EScriptName.RebelType, EScriptName.Region, EScriptName.UnitType,
	};

	private static readonly CArgumentRule MinisterRule = new(EArgumentForm.Whole, words: new[] { "this", "from" });
	private static readonly CArgumentRule WholeRule = new(EArgumentForm.Whole);

	/// <summary>Reads every entry of a block into a list of effects.</summary>
	public static void ReadInto(List<CEffect> effects, CScriptNode block, CScriptContext context)
	{
		foreach (CScriptNode entry in block.Children)
		{
			if (Read(entry, context) is { } effect)
			{
				effects.Add(effect);
			}
		}
	}

	/// <summary>One entry; null where it is left out, the problem reported.</summary>
	public static CEffect? Read(CScriptNode node, CScriptContext context)
	{
		if (!node.KeyQuoted && CEffectKeywords.Find(node.Key) is { } rule)
		{
			return ReadKeyword(node, rule, context);
		}
		return ReadOther(node, context);
	}

	private static CEffect? ReadKeyword(CScriptNode node, CArgumentRule rule, CScriptContext context)
	{
		CScriptProblems problems = context.Problems;
		if (rule.Form == EArgumentForm.Block)
		{
			if (!node.IsBlock)
			{
				problems.Add(EScriptProblem.WrongShape, node.Line,
					$"'{node.Key}' takes a block and is given '{node.Value}'; the original reads the entries after it as its own");
				return null;
			}
			var block = new CEffect(EEffectKind.Keyword, node.Key, CScriptArgument.None);
			ReadBlock(block, node, rule.Block!, context);
			return block;
		}
		if (node.KeyIs("modify_spies"))
		{
			problems.Add(EScriptProblem.Quirk, node.Line, "the original does not read modify_spies' value");
		}
		if (node.KeyIs("release_vassal") && !node.IsBlock && !rule.TakesWord(node.Value!) && !context.Names.TryTag(node.Value!, out _))
		{
			problems.Add(EScriptProblem.Quirk, node.Line,
				$"'release_vassal = {node.Value}': the original reads a tag no country has as random");
			return new CEffect(EEffectKind.Keyword, node.Key, new CScriptArgument("random", false));
		}
		CScriptArgument? argument = CArgumentReader.Read(node, rule, context);
		return argument is null ? null : new CEffect(EEffectKind.Keyword, node.Key, argument);
	}

	/// <summary>A block keyword's block: its own keys, and effects, a limit or weights where it takes them.</summary>
	private static void ReadBlock(CEffect effect, CScriptNode node, CBlockRule rule, CScriptContext context)
	{
		foreach (CScriptNode entry in node.Children)
		{
			if (rule.TakesWeights)
			{
				ReadWeight(effect, entry, context);
				continue;
			}
			if (!entry.KeyQuoted && rule.Keys.TryGetValue(entry.Key, out CArgumentRule? own))
			{
				if (ReadField(entry, own, context) is { } field)
				{
					effect.Fields.Add(field);
				}
				continue;
			}
			if (rule.TakesLimit && entry.KeyIs("limit"))
			{
				ReadLimit(effect, entry, context);
				continue;
			}
			if (rule.TakesUnitTypes && context.Names.Has(EScriptName.UnitType, entry.Key))
			{
				if (CArgumentReader.Read(entry, new CArgumentRule(EArgumentForm.Text), context) is { } name)
				{
					effect.Fields.Add(new CScriptField(entry.Key, name));
				}
				continue;
			}
			if (rule.TakesChildren)
			{
				if (Read(entry, context) is { } child)
				{
					effect.Children.Add(child);
				}
				continue;
			}
			context.Problems.Add(EScriptProblem.UnknownKey, entry.Line,
				$"'{entry.Key}' is not a key of '{node.Key}'; the original drops it");
		}
	}

	/// <summary>One of a block's own keys, which may have a block of keys of its own: a war goal.</summary>
	private static CScriptField? ReadField(CScriptNode entry, CArgumentRule rule, CScriptContext context)
	{
		if (rule.Form != EArgumentForm.Block)
		{
			CScriptArgument? argument = CArgumentReader.Read(entry, rule, context);
			return argument is null ? null : new CScriptField(entry.Key.ToLowerInvariant(), argument);
		}
		if (!entry.IsBlock)
		{
			context.Problems.Add(EScriptProblem.WrongShape, entry.Line,
				$"'{entry.Key}' takes a block and is given '{entry.Value}'");
			return null;
		}
		var field = new CScriptField(entry.Key.ToLowerInvariant(), CScriptArgument.None);
		foreach (CScriptNode inner in entry.Children)
		{
			if (!inner.KeyQuoted && rule.Block!.Keys.TryGetValue(inner.Key, out CArgumentRule? own))
			{
				if (ReadField(inner, own, context) is { } innerField)
				{
					field.Fields.Add(innerField);
				}
				continue;
			}
			context.Problems.Add(EScriptProblem.UnknownKey, inner.Line,
				$"'{inner.Key}' is not a key of '{entry.Key}'; the original drops it");
		}
		return field;
	}

	private static void ReadLimit(CEffect effect, CScriptNode entry, CScriptContext context)
	{
		if (!entry.IsBlock)
		{
			context.Problems.Add(EScriptProblem.WrongShape, entry.Line,
				$"'limit' takes a block and is given '{entry.Value}'");
			return;
		}
		effect.Limit ??= CTrigger.All();
		CTriggerReader.ReadInto(effect.Limit, entry, context);
	}

	/// <summary>A <c>random_list</c>'s entry: a whole number, and a block of effects.</summary>
	private static void ReadWeight(CEffect list, CScriptNode entry, CScriptContext context)
	{
		if (entry.KeyQuoted || entry.Key.Length == 0 || !char.IsAsciiDigit(entry.Key[0]) || !entry.IsBlock)
		{
			context.Problems.Add(EScriptProblem.WrongShape, entry.Line,
				$"'{entry.Key}' in a random_list is not a weight with a block of effects; the original drops it");
			return;
		}
		int weight = CArgumentReader.Whole(entry, entry.Key, context.Problems);
		var weighted = new CEffect(EEffectKind.Weighted, entry.Key,
			new CScriptArgument(entry.Key, false) { Amount = CFixedPoint.FromInt(weight) });
		ReadInto(weighted.Children, entry, context);
		list.Children.Add(weighted);
	}

	private static CEffect? ReadOther(CScriptNode node, CScriptContext context)
	{
		CScriptNames names = context.Names;
		string key = node.Key;

		if (names.Has(EScriptName.GovernmentPosition, key))
		{
			return Leaf(node, EEffectKind.GovernmentPosition, MinisterRule, context);
		}
		if (names.Has(EScriptName.Building, key))
		{
			return Leaf(node, EEffectKind.Building, WholeRule, context);
		}
		if (names.Has(EScriptName.Ideology, key))
		{
			return Scoped(node, EEffectKind.Ideology, CScriptArgument.None, CEffectKeywords.Ideology.Block!, context);
		}
		if (names.Has(EScriptName.Technology, key))
		{
			return Leaf(node, EEffectKind.Technology, WholeRule, context);
		}
		if (names.Has(EScriptName.RebelType, key))
		{
			return Leaf(node, EEffectKind.RebelType, WholeRule, context);
		}
		if (names.Has(EScriptName.Region, key))
		{
			return Scoped(node, EEffectKind.Region, CScriptArgument.None, CEffectKeywords.Region.Block!, context);
		}
		if (names.Has(EScriptName.UnitType, key))
		{
			return Leaf(node, EEffectKind.UnitType, CEffectKeywords.UnitType, context);
		}

		if (!node.IsBlock)
		{
			if (names.GroupOfLaw(node.Value!) is not null && names.Has(EScriptName.LawGroup, key))
			{
				context.Problems.Add(EScriptProblem.UnknownKey, node.Line,
					$"'{key} = {node.Value}': a law cannot be set by an effect in the original");
				return null;
			}
			CTriggerReader.ReportUnknown(node, context, "effect", AllLists);
			return null;
		}
		if (CTriggerReader.Scope(node, context, out bool reported, ScopeWords) is { } scope)
		{
			return Scoped(node, EEffectKind.Scope, scope, CBlockRule.Children, context);
		}

		// A block under a key that names nothing: the original makes it a scope that never runs.
		var dead = new CEffect(EEffectKind.DeadScope, key, new CScriptArgument(key, node.KeyQuoted));
		if (node.KeyIs("limit"))
		{
			context.Problems.Add(EScriptProblem.Quirk, node.Line,
				"'limit' does nothing here: the original never asks its conditions and does the effects beside it anyway");
			dead.Limit = CTrigger.All();
			CTriggerReader.ReadInto(dead.Limit, node, context);
			return dead;
		}
		if (!reported)
		{
			context.Problems.Add(EScriptProblem.UnknownKey, node.Line,
				$"'{key} = {{ }}' names no country, province or scope, so the original never does what is in it");
		}
		ReadInto(dead.Children, node, context);
		return dead;
	}

	private static CEffect? Leaf(CScriptNode node, EEffectKind kind, CArgumentRule rule, CScriptContext context)
	{
		CScriptArgument? argument = CArgumentReader.Read(node, rule, context);
		return argument is null ? null : new CEffect(kind, node.Key, argument);
	}

	private static CEffect? Scoped(CScriptNode node, EEffectKind kind, CScriptArgument argument, CBlockRule rule,
		CScriptContext context)
	{
		if (!node.IsBlock)
		{
			context.Problems.Add(EScriptProblem.WrongShape, node.Line,
				$"'{node.Key}' takes a block and is given '{node.Value}'; the original reads the entries after it as its own");
			return null;
		}
		var effect = new CEffect(kind, node.Key, argument);
		ReadBlock(effect, node, rule, context);
		return effect;
	}
}
