using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Reads a <c>mean_time_to_happen</c>, an <c>ai_chance</c> or an <c>ai_will_do</c>:
/// <c>docs/mechanics/script.md</c>, section 4.
/// </summary>
public static class CMeanTimeToHappenReader
{
	/// <param name="aiChance">
	/// Whether it is an option's <c>ai_chance</c>, whose own <c>factor</c> keeps its decimals;
	/// elsewhere a top-level <c>factor</c> is days.
	/// </param>
	public static void Read(CScriptNode node, CMeanTimeToHappen into, CScriptContext context, bool aiChance)
	{
		CScriptProblems problems = context.Problems;
		if (!node.IsBlock)
		{
			problems.Add(EScriptProblem.WrongShape, node.Line, $"'{node.Key}' takes a block and is given '{node.Value}'");
			return;
		}
		foreach (CScriptNode entry in node.Children)
		{
			if (entry.KeyIs("modifier"))
			{
				ReadModifier(entry, into, context);
				continue;
			}
			int scale = 0;
			if (entry.KeyIs("days") || (entry.KeyIs("factor") && !aiChance))
			{
				scale = 1;
			}
			else if (entry.KeyIs("months"))
			{
				scale = 30;
			}
			else if (entry.KeyIs("years"))
			{
				scale = 365;
			}
			else if (entry.KeyIs("factor"))
			{
				if (Value(entry, problems) is { } factor)
				{
					into.AiFactor = CArgumentReader.Number(entry, factor, problems);
				}
				continue;
			}
			if (scale == 0)
			{
				problems.Add(EScriptProblem.UnknownKey, entry.Line,
					$"'{entry.Key}' is not a key of '{node.Key}'; the original logs it and goes on");
				continue;
			}
			if (Value(entry, problems) is { } text)
			{
				into.Days = CArgumentReader.Whole(entry, text, problems) * scale;
			}
		}
	}

	private static void ReadModifier(CScriptNode node, CMeanTimeToHappen into, CScriptContext context)
	{
		if (!node.IsBlock)
		{
			context.Problems.Add(EScriptProblem.WrongShape, node.Line, $"'modifier' takes a block and is given '{node.Value}'");
			return;
		}
		CFixedPoint factor = CFixedPoint.One;
		CTrigger condition = CTrigger.All();
		foreach (CScriptNode entry in node.Children)
		{
			if (entry.KeyIs("factor"))
			{
				if (Value(entry, context.Problems) is { } text)
				{
					factor = CArgumentReader.Number(entry, text, context.Problems);
				}
				continue;
			}
			if (CTriggerReader.Read(entry, context) is { } trigger)
			{
				condition.Children.Add(trigger);
			}
		}
		into.Modifiers.Add(new CMeanTimeToHappenModifier(factor, condition));
	}

	private static string? Value(CScriptNode entry, CScriptProblems problems)
	{
		if (entry.IsBlock)
		{
			problems.Add(EScriptProblem.WrongShape, entry.Line, $"'{entry.Key}' takes a number and is given a block");
			return null;
		}
		return entry.Value;
	}
}
