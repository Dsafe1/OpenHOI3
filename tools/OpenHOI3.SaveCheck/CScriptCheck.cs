using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;

/// <summary>
/// Reads an install's events and decisions and lists every problem found in them:
///
///   OpenHOI3.SaveCheck script &lt;install folder&gt; [kind]
///
/// where a kind - UnknownKey, UnknownName, OtherCase, WrongShape, Structure or Quirk - lists
/// only those. No savegame is needed: what is checked is whether the files say what the
/// original would read, against <c>docs/mechanics/script.md</c>.
/// </summary>
internal static class CScriptCheck
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck script <install folder> [kind]");
			return 2;
		}
		EScriptProblem? only = null;
		if (args.Length > 2)
		{
			if (!Enum.TryParse(args[2], ignoreCase: true, out EScriptProblem kind))
			{
				Console.Error.WriteLine($"no kind of problem is called {args[2]}: {string.Join(", ", Enum.GetNames<EScriptProblem>())}");
				return 2;
			}
			only = kind;
		}

		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add,
			fill: false, firstDays: false);
		var clock = System.Diagnostics.Stopwatch.StartNew();
		CScriptProblems problems = CScriptLoader.Load(directories, state, warnings.Add);
		Console.WriteLine($"read in:   {clock.ElapsedMilliseconds} ms, a second time - a new game reads them once");

		int provinceEvents = state.Events.Count(e => e.IsProvinceEvent);
		int political = state.Decisions.Count(d => d.Group == EDecisionGroup.Political);
		Console.WriteLine($"events:    {state.Events.Count - provinceEvents} country, {provinceEvents} province");
		Console.WriteLine($"decisions: {state.Decisions.Count - political} diplomatic, {political} political");
		var triggers = new SortedDictionary<string, int>(StringComparer.Ordinal);
		var effects = new SortedDictionary<string, int>(StringComparer.Ordinal);
		void CountTrigger(CTrigger trigger)
		{
			string kind = trigger.Kind.ToString();
			triggers[kind] = triggers.GetValueOrDefault(kind) + 1;
			trigger.Children.ForEach(CountTrigger);
		}
		void CountEffect(CEffect effect)
		{
			string kind = effect.Kind.ToString();
			effects[kind] = effects.GetValueOrDefault(kind) + 1;
			effect.Children.ForEach(CountEffect);
			if (effect.Limit is not null)
			{
				CountTrigger(effect.Limit);
			}
		}
		foreach (CEvent read in state.Events)
		{
			CountTrigger(read.Trigger);
			read.MeanTimeToHappen.Modifiers.ForEach(m => CountTrigger(m.Condition));
			read.Immediate.Effects.ForEach(CountEffect);
			foreach (CEventOption option in read.Options)
			{
				option.Effects.ForEach(CountEffect);
				option.AiChance.Modifiers.ForEach(m => CountTrigger(m.Condition));
			}
		}
		foreach (CDecision read in state.Decisions)
		{
			CountTrigger(read.Potential);
			CountTrigger(read.Allow);
			read.Effects.ForEach(CountEffect);
			read.AiWillDo.Modifiers.ForEach(m => CountTrigger(m.Condition));
		}
		Console.WriteLine($"triggers:  {string.Join(", ", triggers.Select(p => $"{p.Key} {p.Value}"))} (each block's own and counted)");
		Console.WriteLine($"effects:   {string.Join(", ", effects.Select(p => $"{p.Key} {p.Value}"))}");
		Console.WriteLine($"problems:  {problems.Count}");
		foreach ((EScriptProblem kind, int count) in problems.CountByKind())
		{
			Console.WriteLine($"   {kind,-12} {count}");
		}
		Console.WriteLine();

		foreach (CScriptProblem problem in problems.All
			.Where(p => only is null || p.Kind == only)
			.OrderBy(p => p.File, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Line))
		{
			Console.WriteLine($"{problem.Kind,-12} {problem}");
		}
		return 0;
	}
}
