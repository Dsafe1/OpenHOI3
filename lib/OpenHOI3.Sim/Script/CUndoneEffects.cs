using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The effect keywords that do nothing and are reported rather than run, and why:
/// <c>docs/mechanics/effects.md</c>, section 8. A keyword leaves this list when what it changes is
/// built.
/// </summary>
public static class CUndoneEffects
{
	private static readonly Dictionary<string, EScriptGap> Reasons = Build();

	/// <summary>Why the keyword is not run, or null where it is.</summary>
	public static EScriptGap? Reason(string keyword)
	{
		return Reasons.TryGetValue(keyword, out EScriptGap why) ? why : null;
	}

	private static Dictionary<string, EScriptGap> Build()
	{
		var reasons = new Dictionary<string, EScriptGap>(StringComparer.OrdinalIgnoreCase);
		void Add(EScriptGap why, params string[] keywords)
		{
			foreach (string keyword in keywords)
			{
				reasons.Add(keyword, why);
			}
		}

		// The event pass.
		Add(EScriptGap.Unbuilt, "country_event", "province_event");
		// Diplomacy.
		Add(EScriptGap.Unbuilt, "undeclared_war", "release", "release_vassal", "surrender_inherit",
			"split_troops");
		Add(EScriptGap.Unbuilt, "join_faction", "leave_faction");
		Add(EScriptGap.Unbuilt, "war_exhaustion");
		// The government and the parties.
		Add(EScriptGap.Unbuilt, "government", "coup_by", "do_election", "remove_minister", "organisation", "popularity");
		// Revolts, espionage, the AI.
		Add(EScriptGap.Unbuilt, "revolt_risk", "create_revolt");
		Add(EScriptGap.Unbuilt, "remove_fow", "local_intel_boost", "modify_spies");
		Add(EScriptGap.Unbuilt, "add_ai_strategy", "fixed_ai_strategy");
		// Units an event gives or takes.
		Add(EScriptGap.Unbuilt, "load_oob", "add_division", "remove_brigade");

		Add(EScriptGap.Unread, "create_alliance");
		return reasons;
	}
}
