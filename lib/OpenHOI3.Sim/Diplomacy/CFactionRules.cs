using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Diplomacy;

/// <summary>
/// The rules a faction's <c>rule</c> block may set, in the original's order, each with the key of its
/// name's text. The original has a fourth before them, <c>NONE</c>, that no file key is known to set.
/// What a rule does is the factions' piece; the diplomacy screen names them
/// (<c>docs/mechanics/diplomacyscreen.md</c>, section 6.1).
/// </summary>
public static class CFactionRules
{
	public static IReadOnlyList<(string Key, string TextKey)> All { get; } = new[]
	{
		("limited_war", "RULE_LIMITED_WAR"),
		("allience_wide_guarantee", "RULE_ALLIANCE_GUARANTEE"),
		("free_resource_gifts", "RULE_FREE_RESOURCE_GIFTS"),
	};

	public static bool IsRule(string key)
	{
		foreach ((string rule, string _) in All)
		{
			if (rule.Equals(key, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}
