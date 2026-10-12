using System;
using System.Globalization;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Reads one value by its key's rule, reporting what does not fit: <c>docs/mechanics/script.md</c>,
/// sections 5 to 7. A value that is read anyway is read as the original reads it - a word where a
/// number goes is nought, a tag nothing has is <c>---</c>.
/// </summary>
public static class CArgumentReader
{
	private static readonly CCountryTag NoTag = new("---");

	/// <summary>
	/// The value of a key that takes one. Null where the key is written with a block, which the
	/// original would have read astray, so the entry is left out.
	/// </summary>
	public static CScriptArgument? Read(CScriptNode node, CArgumentRule rule, CScriptContext context)
	{
		CScriptProblems problems = context.Problems;
		if (node.IsBlock)
		{
			if (rule.Form == EArgumentForm.Ignored)
			{
				return CScriptArgument.None;
			}
			problems.Add(EScriptProblem.WrongShape, node.Line,
				$"'{node.Key}' takes one value and is given a block; the original reads the block's contents as its parent's");
			return null;
		}

		string text = node.Value!;
		bool quoted = node.ValueQuoted;
		var argument = new CScriptArgument(text, quoted);
		if (!quoted && rule.TakesWord(text))
		{
			return argument;
		}

		switch (rule.Form)
		{
			case EArgumentForm.Yes:
				if (!argument.IsYes && !argument.IsWord("no"))
				{
					problems.Add(EScriptProblem.WrongShape, node.Line,
						$"'{node.Key} = {text}': only the word yes is true, so this is false");
				}
				return argument;

			case EArgumentForm.Whole:
				return new CScriptArgument(text, quoted) { Amount = CFixedPoint.FromInt(Whole(node, text, problems)) };

			case EArgumentForm.Number:
				return new CScriptArgument(text, quoted) { Amount = Number(node, text, problems) };

			case EArgumentForm.Text:
			case EArgumentForm.Ignored:
				return argument;

			case EArgumentForm.Date:
				if (!IsDate(text))
				{
					problems.Add(EScriptProblem.WrongShape, node.Line,
						$"'{node.Key} = {text}' is not year.month.day; the original reads a date before the start of time");
				}
				return argument;

			case EArgumentForm.Tag:
				return new CScriptArgument(text, quoted) { Tag = Tag(node, text, context) };

			case EArgumentForm.Province:
			{
				int id = Whole(node, text, problems);
				if (!context.Names.IsProvince(id))
				{
					problems.Add(EScriptProblem.UnknownName, node.Line, $"'{node.Key} = {text}': there is no province {id}");
				}
				return new CScriptArgument(text, quoted) { Province = id, Amount = CFixedPoint.FromInt(id) };
			}

			case EArgumentForm.ProvinceOrTag:
				if (text.Length > 0 && (char.IsAsciiDigit(text[0]) || text[0] == '-'))
				{
					goto case EArgumentForm.Province;
				}
				goto case EArgumentForm.Tag;

			case EArgumentForm.Event:
			{
				int id = Whole(node, text, problems);
				context.EventReferences.Add((problems.File, node.Line, id));
				return new CScriptArgument(text, quoted) { Amount = CFixedPoint.FromInt(id) };
			}

			case EArgumentForm.NameOrWhole:
				if (text.Length > 0 && (char.IsAsciiDigit(text[0]) || text[0] == '-'))
				{
					goto case EArgumentForm.Whole;
				}
				goto case EArgumentForm.Name;

			case EArgumentForm.Name:
				CheckName(node, rule.Names, text, context);
				return argument;

			default:
				problems.Add(EScriptProblem.WrongShape, node.Line, $"'{node.Key}' takes a block and is given '{text}'");
				return null;
		}
	}

	/// <summary>Reports a name its list does not have, or has only in another case.</summary>
	public static void CheckName(CScriptNode node, EScriptName kind, string text, CScriptContext context)
	{
		CScriptNames names = context.Names;
		if (!names.Knows(kind) || names.Has(kind, text))
		{
			return;
		}
		string? other = names.InOtherCase(kind, text);
		if (other is not null)
		{
			// A file is opened by name on a disk that ignores case.
			if (kind != EScriptName.OrderOfBattle)
			{
				context.Problems.Add(EScriptProblem.OtherCase, node.Line,
					$"'{node.Key} = {text}': {Describe(kind)} is written '{other}', and the original matches case");
			}
			return;
		}
		string after = kind == EScriptName.Faction ? ", so the original reads it as none" : "";
		context.Problems.Add(EScriptProblem.UnknownName, node.Line, $"'{node.Key} = {text}': there is no {Describe(kind)} of that name{after}");
	}

	/// <summary>
	/// <c>atoi</c>: a whole number, its decimals cut and a word nought. Both are reported - a word as
	/// the wrong shape, decimals only where they are not all nought.
	/// </summary>
	public static int Whole(CScriptNode node, string text, CScriptProblems problems)
	{
		if (CFixedPoint.TryParse(text, out CFixedPoint value))
		{
			long whole = value.Raw / 1000;
			if (value.Raw % 1000 != 0)
			{
				problems.Add(EScriptProblem.WrongShape, node.Line,
					$"'{node.Key} = {text}' takes a whole number; the original reads {whole}");
			}
			return (int)whole;
		}
		problems.Add(EScriptProblem.WrongShape, node.Line, $"'{node.Key} = {text}' is not a number; the original reads 0");
		return 0;
	}

	public static CFixedPoint Number(CScriptNode node, string text, CScriptProblems problems)
	{
		if (CFixedPoint.TryParse(text, out CFixedPoint value))
		{
			return value;
		}
		problems.Add(EScriptProblem.WrongShape, node.Line, $"'{node.Key} = {text}' is not a number; the original reads 0");
		return CFixedPoint.Zero;
	}

	private static CCountryTag Tag(CScriptNode node, string text, CScriptContext context)
	{
		if (context.Names.TryTag(text, out CCountryTag tag))
		{
			if (text.Length > CCountryTag.Length)
			{
				context.Problems.Add(EScriptProblem.Quirk, node.Line,
					$"'{node.Key} = {text}': the original reads the tag {tag} from its first three letters");
			}
			return tag;
		}
		context.Problems.Add(EScriptProblem.UnknownName, node.Line, $"'{node.Key} = {text}': no country has that tag");
		return NoTag;
	}

	private static bool IsDate(string text)
	{
		string[] parts = text.Split('.');
		if (parts.Length is < 3 or > 4)
		{
			return false;
		}
		foreach (string part in parts)
		{
			if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _))
			{
				return false;
			}
		}
		int month = int.Parse(parts[1], CultureInfo.InvariantCulture);
		int day = int.Parse(parts[2], CultureInfo.InvariantCulture);
		return month is >= 1 and <= 12 && day is >= 1 and <= 31;
	}

	private static string Describe(EScriptName kind)
	{
		return kind switch
		{
			EScriptName.GovernmentPosition => "government position",
			EScriptName.TechnologyCategory => "technology category",
			EScriptName.UnitType => "unit type",
			EScriptName.LawGroup => "law group",
			EScriptName.CombinedArms => "combined-arms group",
			EScriptName.IdeologyGroup => "ideology group",
			EScriptName.RebelType => "rebel type",
			EScriptName.StrategicResource => "strategic resource",
			EScriptName.CasusBelli => "casus belli",
			EScriptName.OrderOfBattle => "order of battle in history/units",
			_ => kind.ToString().ToLowerInvariant(),
		};
	}
}
