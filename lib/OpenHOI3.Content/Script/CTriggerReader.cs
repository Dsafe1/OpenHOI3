using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Reads a trigger block: each key a keyword, a name the original looks up, an ideology, a
/// region or a scope, in that order, and anything else a problem. <c>docs/mechanics/script.md</c>,
/// section 5.
/// </summary>
public static class CTriggerReader
{
	/// <summary>The words a block may be scoped to, besides a country and a province.</summary>
	private static readonly string[] ScopeWords =
		{ "this", "from", "owner", "controller", "ally", "capital_scope", "sea_zone", "local_enemy", "overlord" };

	/// <summary>The names tried before an ideology, in the original's order, with what each one's value is.</summary>
	private static readonly (EScriptName Names, ETriggerKind Kind)[] LookedUp =
	{
		(EScriptName.GovernmentPosition, ETriggerKind.GovernmentPosition),
		(EScriptName.Building, ETriggerKind.Building),
		(EScriptName.Technology, ETriggerKind.Technology),
		(EScriptName.UnitType, ETriggerKind.UnitType),
		(EScriptName.LawGroup, ETriggerKind.LawGroup),
		(EScriptName.CombinedArms, ETriggerKind.CombinedArms),
	};

	/// <summary>Reads every entry of a block into a group's conditions.</summary>
	public static void ReadInto(CTrigger group, CScriptNode block, CScriptContext context)
	{
		foreach (CScriptNode entry in block.Children)
		{
			if (Read(entry, context) is { } trigger)
			{
				group.Children.Add(trigger);
			}
		}
	}

	/// <summary>One entry; null where it is left out, the problem reported.</summary>
	public static CTrigger? Read(CScriptNode node, CScriptContext context)
	{
		if (!node.KeyQuoted && CTriggerKeywords.Find(node.Key) is { } rule)
		{
			return ReadKeyword(node, rule, context);
		}
		return ReadOther(node, context);
	}

	private static CTrigger? ReadKeyword(CScriptNode node, CArgumentRule rule, CScriptContext context)
	{
		CScriptProblems problems = context.Problems;
		if (node.KeyIs("has_hostile_spy_mission"))
		{
			problems.Add(EScriptProblem.Quirk, node.Line,
				"has_hostile_spy_mission is broken in the original: it reads a block whatever follows and tests a value nothing set");
			return null;
		}
		if (rule.Form == EArgumentForm.Block)
		{
			if (!node.IsBlock)
			{
				problems.Add(EScriptProblem.WrongShape, node.Line,
					$"'{node.Key}' takes a block and is given '{node.Value}'; the original reads the entries after it as its own");
				return null;
			}
			var block = new CTrigger(ETriggerKind.Keyword, node.Key, CScriptArgument.None);
			ReadBlock(block, node, rule.Block!, context);
			return block;
		}
		if ((node.KeyIs("unit_has_leader") || node.KeyIs("unit_in_battle")) && !node.IsBlock && !node.ValueQuoted
			&& node.Value!.Equals("no", StringComparison.OrdinalIgnoreCase))
		{
			problems.Add(EScriptProblem.Quirk, node.Line,
				$"'{node.Key} = no' is not a negation: the original does not read the value");
		}
		CScriptArgument? argument = CArgumentReader.Read(node, rule, context);
		return argument is null ? null : new CTrigger(ETriggerKind.Keyword, node.Key, argument);
	}

	/// <summary>A block keyword's block: its own keys, and conditions where it takes them.</summary>
	private static void ReadBlock(CTrigger trigger, CScriptNode node, CBlockRule rule, CScriptContext context)
	{
		foreach (CScriptNode entry in node.Children)
		{
			if (!entry.KeyQuoted && rule.Keys.TryGetValue(entry.Key, out CArgumentRule? own))
			{
				if (CArgumentReader.Read(entry, own, context) is { } argument)
				{
					trigger.Fields.Add(new CScriptField(entry.Key.ToLowerInvariant(), argument));
				}
				continue;
			}
			if (rule.TakesChildren)
			{
				if (Read(entry, context) is { } child)
				{
					trigger.Children.Add(child);
				}
				continue;
			}
			context.Problems.Add(EScriptProblem.UnknownKey, entry.Line,
				$"'{entry.Key}' is not a key of '{node.Key}'; the original drops it");
		}
	}

	private static CTrigger? ReadOther(CScriptNode node, CScriptContext context)
	{
		CScriptNames names = context.Names;
		CScriptProblems problems = context.Problems;
		string key = node.Key;

		foreach ((EScriptName list, ETriggerKind kind) in LookedUp)
		{
			if (!names.Has(list, key))
			{
				continue;
			}
			if (node.IsBlock)
			{
				problems.Add(EScriptProblem.WrongShape, node.Line,
					$"'{key}' takes one value and is given a block; the original reads the block's contents as its parent's");
				return null;
			}
			return new CTrigger(kind, key, LookedUpValue(node, kind, context));
		}

		if (names.Has(EScriptName.Ideology, key))
		{
			return Scoped(node, ETriggerKind.Ideology, CScriptArgument.None, CTriggerKeywords.Ideology.Block!, context);
		}
		if (names.Has(EScriptName.Region, key))
		{
			return Scoped(node, ETriggerKind.Region, CScriptArgument.None, CBlockRule.Children, context);
		}
		if (Scope(node, context, out bool reported) is { } scope)
		{
			return Scoped(node, ETriggerKind.Scope, scope, CBlockRule.Children, context);
		}
		if (!reported)
		{
			ReportUnknown(node, context, "condition", AllLists);
		}
		return null;
	}

	private static readonly EScriptName[] AllLists =
	{
		EScriptName.GovernmentPosition, EScriptName.Building, EScriptName.Technology, EScriptName.UnitType,
		EScriptName.LawGroup, EScriptName.CombinedArms, EScriptName.Ideology, EScriptName.Region,
	};

	/// <summary>
	/// The value of a looked-up name. The original hands it on as a name, never as a word, so
	/// <c>this</c> and <c>from</c> are read as nought.
	/// </summary>
	private static CScriptArgument LookedUpValue(CScriptNode node, ETriggerKind kind, CScriptContext context)
	{
		string text = node.Value!;
		switch (kind)
		{
			case ETriggerKind.LawGroup:
			{
				CArgumentReader.CheckName(node, EScriptName.Law, text, context);
				string? group = context.Names.GroupOfLaw(text);
				if (group is not null && group != node.Key)
				{
					context.Problems.Add(EScriptProblem.Quirk, node.Line,
						$"'{node.Key} = {text}': that law is of {group}, and the original never finds it in {node.Key}");
				}
				return new CScriptArgument(text, node.ValueQuoted);
			}
			case ETriggerKind.CombinedArms:
				context.Problems.Add(EScriptProblem.Quirk, node.Line,
					$"'{node.Key} = {text}': the original reads a combined-arms group as no, whatever is written, and so asks for none of it");
				return new CScriptArgument(text, node.ValueQuoted);
			default:
				return new CScriptArgument(text, node.ValueQuoted)
				{
					Amount = Sim.CFixedPoint.FromInt(CArgumentReader.Whole(node, text, context.Problems)),
				};
		}
	}

	private static CTrigger? Scoped(CScriptNode node, ETriggerKind kind, CScriptArgument argument, CBlockRule rule,
		CScriptContext context)
	{
		if (!node.IsBlock)
		{
			context.Problems.Add(EScriptProblem.WrongShape, node.Line,
				$"'{node.Key}' takes a block and is given '{node.Value}'; the original reads the entries after it as its own");
			return null;
		}
		var trigger = new CTrigger(kind, node.Key, argument);
		ReadBlock(trigger, node, rule, context);
		return trigger;
	}

	/// <summary>
	/// What a key names as a scope: a province by its number, a scope word, or a country by its
	/// tag. Null for none of them, <paramref name="reported"/> saying whether why has been said.
	/// </summary>
	public static CScriptArgument? Scope(CScriptNode node, CScriptContext context, out bool reported,
		string[]? words = null)
	{
		reported = false;
		string key = node.Key;
		if (!node.KeyQuoted && key.Length > 0 && (char.IsAsciiDigit(key[0]) || key[0] == '-'))
		{
			int id = CArgumentReader.Whole(node, key, context.Problems);
			if (!context.Names.IsProvince(id))
			{
				context.Problems.Add(EScriptProblem.UnknownName, node.Line, $"'{key} = {{ }}': there is no province {id}");
			}
			return new CScriptArgument(key, false) { Province = id };
		}
		if (!node.KeyQuoted)
		{
			foreach (string word in words ?? ScopeWords)
			{
				if (key.Equals(word, StringComparison.OrdinalIgnoreCase))
				{
					return new CScriptArgument(key, false);
				}
			}
			if (CScriptNames.IsOriginalWord(key) || CTriggerKeywords.Find(key) is not null || CEffectKeywords.Find(key) is not null)
			{
				if (context.Names.TryTag(key, out _))
				{
					context.Problems.Add(EScriptProblem.Quirk, node.Line,
						$"'{key} = {{ }}' is never a scope: the original reads {key} as its own word, not as a country's tag");
					reported = true;
				}
				return null;
			}
		}
		if (context.Names.TryTag(key, out CCountryTag tag))
		{
			if (key.Length > CCountryTag.Length)
			{
				context.Problems.Add(EScriptProblem.Quirk, node.Line,
					$"'{key} = {{ }}': the original reads the tag {tag} from its first three letters");
			}
			return new CScriptArgument(key, node.KeyQuoted) { Tag = tag };
		}
		return null;
	}

	/// <summary>Reports a key nothing takes, saying where it is a name in another case.</summary>
	public static void ReportUnknown(CScriptNode node, CScriptContext context, string what, EScriptName[] lists)
	{
		foreach (EScriptName list in lists)
		{
			if (context.Names.InOtherCase(list, node.Key) is { } other)
			{
				context.Problems.Add(EScriptProblem.OtherCase, node.Line,
					$"'{node.Key}' is no {what}: '{other}' is, and the original matches case");
				return;
			}
		}
		context.Problems.Add(EScriptProblem.UnknownKey, node.Line, $"'{node.Key}' is no {what}");
	}
}
