using System.Collections.Generic;
using System.IO;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Reads the events of every file in <c>events/</c>, layered: <c>docs/mechanics/script.md</c>,
/// section 3.
/// </summary>
public static class CEventReader
{
	private static readonly CArgumentRule YesRule = new(EArgumentForm.Yes);
	private static readonly CArgumentRule TextRule = new(EArgumentForm.Text);

	public static List<CEvent> ReadAll(CDirectorySettings directories, CScriptContext context)
	{
		var events = new List<CEvent>();
		foreach (string path in directories.ListFiles("events", "*.txt"))
		{
			context.Problems.File = Path.GetRelativePath(directories.GameRoot, path).Replace('\\', '/');
			ReadFile(CScriptParser.ParseFile(path, context.Problems), events, context);
		}
		return events;
	}

	/// <summary>One file's events, appended, with their problems filed under the context's file.</summary>
	public static void ReadFile(CScriptNode root, List<CEvent> events, CScriptContext context)
	{
		foreach (CScriptNode entry in root.Children)
		{
			bool province = entry.KeyIs("province_event");
			if (!province && !entry.KeyIs("country_event"))
			{
				context.Problems.Add(EScriptProblem.UnknownKey, entry.Line,
					$"'{entry.Key}' is no event; the original logs it and goes on");
				continue;
			}
			if (!entry.IsBlock)
			{
				context.Problems.Add(EScriptProblem.WrongShape, entry.Line, $"'{entry.Key}' takes a block");
				continue;
			}
			events.Add(Read(entry, province, context));
		}
	}

	private static CEvent Read(CScriptNode node, bool province, CScriptContext context)
	{
		CScriptProblems problems = context.Problems;
		var read = new CEvent(province, problems.File, node.Line);
		bool hasId = false;
		foreach (CScriptNode entry in node.Children)
		{
			switch (entry.KeyQuoted ? "" : entry.Key.ToLowerInvariant())
			{
				case "id":
					if (Scalar(entry, context) is { } id)
					{
						read.Id = CArgumentReader.Whole(entry, id.Text, problems);
						hasId = true;
					}
					break;
				case "title":
					read.Title = Text(entry, context) ?? read.Title;
					break;
				case "desc":
					read.Description = Text(entry, context) ?? read.Description;
					break;
				case "subtitle":
					read.Subtitle = Text(entry, context) ?? read.Subtitle;
					break;
				case "picture":
					read.Picture = Text(entry, context) ?? read.Picture;
					break;
				case "is_triggered_only":
					read.IsTriggeredOnly = Yes(entry, context);
					break;
				case "major":
					read.Major = Yes(entry, context);
					break;
				case "fire_only_once":
					read.FireOnlyOnce = Yes(entry, context);
					break;
				case "trigger":
					if (Block(entry, context))
					{
						CTriggerReader.ReadInto(read.Trigger, entry, context);
					}
					break;
				case "mean_time_to_happen":
					CMeanTimeToHappenReader.Read(entry, read.MeanTimeToHappen, context, aiChance: false);
					break;
				case "immediate":
					if (Block(entry, context))
					{
						ReadOption(entry, read.Immediate, context);
					}
					break;
				case "option":
					if (Block(entry, context))
					{
						var option = new CEventOption();
						ReadOption(entry, option, context);
						read.Options.Add(option);
					}
					break;
				default:
					problems.Add(EScriptProblem.UnknownKey, entry.Line,
						$"'{entry.Key}' is not a key of an event; the original drops it");
					break;
			}
		}

		string name = hasId ? $"event {read.Id}" : "an event";
		if (!hasId)
		{
			problems.Add(EScriptProblem.Structure, node.Line, "an event has no id");
		}
		if (read.Title.Length == 0)
		{
			problems.Add(EScriptProblem.Structure, node.Line, $"{name} has no title");
		}
		if (read.Description.Length == 0)
		{
			problems.Add(EScriptProblem.Structure, node.Line, $"{name} has no desc");
		}
		if (read.Options.Count == 0)
		{
			problems.Add(EScriptProblem.Structure, node.Line, $"{name} has no options, so it never fires");
		}
		return read;
	}

	/// <summary>An option, or an event's <c>immediate</c>: a name, an AI's chance, and effects.</summary>
	private static void ReadOption(CScriptNode node, CEventOption option, CScriptContext context)
	{
		foreach (CScriptNode entry in node.Children)
		{
			if (entry.KeyIs("name"))
			{
				option.Name = Text(entry, context) ?? option.Name;
				continue;
			}
			if (entry.KeyIs("ai_chance"))
			{
				CMeanTimeToHappenReader.Read(entry, option.AiChance, context, aiChance: true);
				continue;
			}
			if (CEffectReader.Read(entry, context) is { } effect)
			{
				option.Effects.Add(effect);
			}
		}
	}

	internal static CScriptArgument? Scalar(CScriptNode entry, CScriptContext context)
	{
		return CArgumentReader.Read(entry, TextRule, context);
	}

	internal static string? Text(CScriptNode entry, CScriptContext context)
	{
		return Scalar(entry, context)?.Text;
	}

	internal static bool Yes(CScriptNode entry, CScriptContext context)
	{
		return CArgumentReader.Read(entry, YesRule, context) is { IsYes: true };
	}

	internal static bool Block(CScriptNode entry, CScriptContext context)
	{
		if (entry.IsBlock)
		{
			return true;
		}
		context.Problems.Add(EScriptProblem.WrongShape, entry.Line, $"'{entry.Key}' takes a block and is given '{entry.Value}'");
		return false;
	}
}
