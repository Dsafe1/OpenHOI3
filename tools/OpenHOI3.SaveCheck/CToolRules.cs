using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Rules;

/// <summary>
/// The rules a trial plays its game by, where they are to be other than the folders': <c>enhanced</c> among a
/// command's arguments plays a game of folders with no rules file - Their Finest Hour's own - as an enhanced one, every
/// fix a game asks of its rules on, so that a fix can be tried at the base game's later starts; and
/// <c>without=NAME</c>, as often as wanted, plays an enhanced game without that fix, whatever its folders or its save
/// say - to set a game with a fix beside the same game without it (<c>docs/enhanced.md</c>). A fix that acts as the
/// files are read is the folders' either way.
/// </summary>
internal static class CToolRules
{
	public const string EnhancedSwitch = "enhanced";

	public const string WithoutSwitch = "without=";

	/// <summary>Sets the game's rules by the arguments, and says in a line what they then are where it changed them.</summary>
	public static void Apply(CGameState state, IReadOnlyList<string> args)
	{
		bool changed = false;
		if (args.Contains(EnhancedSwitch) && !state.Rules.Enhanced)
		{
			state.Rules.CopyFrom(new CRules(Array.Empty<EFix>()));
			changed = true;
		}
		List<EFix> off = args.Where(a => a.StartsWith(WithoutSwitch, StringComparison.Ordinal))
			.Select(a => CFixes.Named(a[WithoutSwitch.Length..])).Where(fix => fix is not null).Select(fix => fix!.Value).ToList();
		foreach (string unknown in args.Where(a => a.StartsWith(WithoutSwitch, StringComparison.Ordinal)
			&& CFixes.Named(a[WithoutSwitch.Length..]) is null))
		{
			Console.WriteLine($"'{unknown}' names no fix and is left aside");
		}
		if (state.Rules.Enhanced && off.Count > 0)
		{
			state.Rules.CopyFrom(new CRules(state.Rules.Without.Concat(off)));
			changed = true;
		}
		if (changed)
		{
			Console.WriteLine($"the rules: {state.Rules.Text}");
		}
	}
}
