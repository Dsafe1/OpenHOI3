using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// What script met that is not built or not read - a condition that answered false without being
/// asked, an effect that did nothing - by key, with why and how often. See
/// <c>docs/mechanics/triggers.md</c>, section 8, and <c>docs/mechanics/effects.md</c>.
/// </summary>
public sealed class CScriptReport
{
	public SortedDictionary<string, (EScriptGap Why, int Times)> Gaps { get; } = new(StringComparer.Ordinal);

	/// <summary>A gap met: from several threads at once while the AI's scripts run side by side, so one at a time.</summary>
	public void Add(string key, EScriptGap why)
	{
		string known = key.ToLowerInvariant();
		lock (Gaps)
		{
			Gaps[known] = (why, Gaps.GetValueOrDefault(known).Times + 1);
		}
	}
}
