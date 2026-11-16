using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

/// <summary>
/// Runs every effect of every event and decision once, on one new game, and says what did nothing
/// for want of what is not built:
///
///   OpenHOI3.SaveCheck effects &lt;install folder&gt; [tag]
///
/// A look and not a check: the effects run one after another on the same game, each event's
/// immediate block and all its options, each decision's effects, in the scope of the given country
/// (Germany by default) and, for a province event, its capital. What the game looks like after is
/// meaningless; that nothing in the files trips the runtime, and the list of what is unbuilt, are
/// the point. See <c>docs/mechanics/effects.md</c>.
/// </summary>
internal static class CEffectsTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck effects <install folder> [tag]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add,
			fill: false, firstDays: false);
		var tag = new CCountryTag(args.Length > 2 ? args[2].ToUpperInvariant() : "GER");
		if (state.Country(tag) is not { } country)
		{
			Console.Error.WriteLine($"no country is {tag}");
			return 2;
		}

		var report = new CScriptReport();
		var clock = System.Diagnostics.Stopwatch.StartNew();
		int blocks = 0;
		int effects = 0;
		void RunBlock(IReadOnlyList<CEffect> block, CEventScope scope)
		{
			blocks++;
			effects += Count(block);
			CEffects.Run(state, block, scope, report);
		}
		foreach (CEvent read in state.Events)
		{
			CEventScope scope = CEventScope.Start(tag, read.IsProvinceEvent ? country.Capital : 0, randomState: (uint)read.Id);
			RunBlock(read.Immediate.Effects, scope);
			foreach (CEventOption option in read.Options)
			{
				RunBlock(option.Effects, scope);
			}
		}
		foreach (CDecision read in state.Decisions)
		{
			RunBlock(read.Effects, CEventScope.Start(tag, randomState: (uint)read.Line));
		}

		Console.WriteLine($"ran {blocks} blocks, {effects} effects counted inside them, in {clock.ElapsedMilliseconds} ms, in {tag}'s scope");
		Console.WriteLine($"afterwards: {country.Flags.All.Count} flags on {tag}, {state.Flags.All.Count} global, "
			+ $"{country.EventModifiers.Count} modifiers given it, {country.Variables.Count} variables");
		Console.WriteLine();
		Console.WriteLine("did nothing, and why - with the conditions their limits met:");
		foreach ((string key, (EScriptGap why, int times)) in report.Gaps.OrderBy(p => p.Value.Why).ThenByDescending(p => p.Value.Times))
		{
			Console.WriteLine($"   {why,-10} {key,-34} {times}");
		}
		return 0;
	}

	private static int Count(IReadOnlyList<CEffect> block)
	{
		int count = block.Count;
		foreach (CEffect effect in block)
		{
			count += Count(effect.Children);
		}
		return count;
	}
}
