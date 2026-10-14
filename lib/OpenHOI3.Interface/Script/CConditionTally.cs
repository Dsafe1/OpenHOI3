using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Interface.Script;

/// <summary>
/// How many of a block's conditions are met, out of how many, as the original counts them for the Strategic Effects
/// window: a plain condition counts one; "all of" and a block over a set count their own conditions in the scope they
/// were given; "one of" counts one, met where any holds; "none of" counts its own and turns the met round; a scope counts
/// its own in the scope it names, or in nobody's where it names nothing. See <c>docs/mechanics/strategiceffects.md</c>,
/// section 4.
/// </summary>
public static class CConditionTally
{
	/// <summary>The block's conditions, each counted as the table says.</summary>
	public static (int Met, int Counted) Of(CGameState state, CTrigger block, CEventScope scope)
	{
		int met = 0;
		int counted = 0;
		foreach (CTrigger child in block.Children)
		{
			Count(state, child, scope, ref met, ref counted);
		}
		return (met, counted);
	}

	private static void Count(CGameState state, CTrigger trigger, CEventScope scope, ref int met, ref int counted)
	{
		switch (trigger.Kind)
		{
			case ETriggerKind.Keyword:
				switch (trigger.Keyword)
				{
					case "and":
					case "any_owned_province":
					case "any_neighbor_province":
					case "any_neighbor_country":
					case "any_core":
					case "enemy":
						// The scope is handed on as it is: a block over a set does not move it.
						(int inMet, int inCounted) = Of(state, trigger, scope);
						met += inMet;
						counted += inCounted;
						return;
					case "or":
						counted++;
						foreach (CTrigger child in trigger.Children)
						{
							if (CConditions.Holds(state, child, scope))
							{
								met++;
								break;
							}
						}
						return;
					case "not":
						(int notMet, int notCounted) = Of(state, trigger, scope);
						met += notCounted - notMet;
						counted += notCounted;
						return;
				}
				break;
			case ETriggerKind.Region:
				(int regionMet, int regionCounted) = Of(state, trigger, scope);
				met += regionMet;
				counted += regionCounted;
				return;
			case ETriggerKind.Scope:
				// Counted even where the scope names nothing, which asking whether it holds would refuse.
				CEventScope named = CScopeSwitch.Narrow(state, scope, trigger.Argument, null) ?? CEventScope.Start(null);
				(int scopeMet, int scopeCounted) = Of(state, trigger, named);
				met += scopeMet;
				counted += scopeCounted;
				return;
		}
		counted++;
		if (CConditions.Holds(state, trigger, scope))
		{
			met++;
		}
	}
}
