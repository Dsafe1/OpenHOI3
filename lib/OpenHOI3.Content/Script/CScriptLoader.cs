using System;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Content.Script;

/// <summary>
/// Reads a game's events and decisions into it, and the rest of its script - casus belli, war goals, on_actions,
/// victory conditions, the laws' and occupation policies' allow blocks, covert operations and triggered modifiers - after everything they name has been loaded, and
/// returns the problems found: <c>docs/mechanics/script.md</c>.
/// </summary>
public static class CScriptLoader
{
	public static CScriptProblems Load(CDirectorySettings directories, CGameState state, Action<string> warn)
	{
		var context = new CScriptContext(CScriptNamesReader.Read(directories, state, warn));
		state.CasusBelliTypes = CCasusBelliTypeReader.Read(directories, context);
		History.CWarReader.ReadGoals(directories, state);
		state.Events = CEventReader.ReadAll(directories, context);
		state.Decisions = CDecisionReader.ReadAll(directories, context);
		state.OnActions = COnActionReader.Read(directories, context, state.Events);
		state.VictoryConditions = CFactionAimsReader.Read(directories, state, context);
		CLawAllowReader.Read(directories, state, context);
		COccupationPolicyAllowReader.Read(directories, state, context);
		state.CovertOperations.AddRange(CCovertOpsReader.Read(directories, context));
		state.TriggeredModifiers.AddRange(CTriggeredModifierReader.Read(directories, context));
		CheckAcrossFiles(state.Events, state.Decisions, context);
		return context.Problems;
	}

	/// <summary>
	/// What only the whole set can say: two events of one id or two decisions of one name, of
	/// which the later is the one found, and an event's id that no event has.
	/// </summary>
	public static void CheckAcrossFiles(IReadOnlyList<CEvent> events, IReadOnlyList<CDecision> decisions,
		CScriptContext context)
	{
		CScriptProblems problems = context.Problems;
		var byId = new Dictionary<int, CEvent>();
		foreach (CEvent read in events)
		{
			if (byId.TryGetValue(read.Id, out CEvent? earlier))
			{
				problems.File = read.File;
				problems.Add(EScriptProblem.Quirk, read.Line,
					$"event {read.Id} is also at {earlier.File}:{earlier.Line}; the original keeps both and finds this one by its id");
			}
			byId[read.Id] = read;
		}

		var byName = new Dictionary<string, CDecision>(StringComparer.Ordinal);
		foreach (CDecision read in decisions)
		{
			if (byName.TryGetValue(read.Name, out CDecision? earlier))
			{
				problems.File = read.File;
				problems.Add(EScriptProblem.Quirk, read.Line,
					$"decision {read.Name} is also at {earlier.File}:{earlier.Line}; the original keeps both and finds this one by its name");
			}
			byName[read.Name] = read;
		}

		foreach ((string file, int line, int id) in context.EventReferences)
		{
			if (!byId.ContainsKey(id))
			{
				problems.File = file;
				problems.Add(EScriptProblem.UnknownName, line, $"no event has the id {id}");
			}
		}
	}
}
