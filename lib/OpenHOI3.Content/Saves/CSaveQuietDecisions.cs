using System;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// The decisions a country has taken out of its "decisions available" alert, in an enhanced game's save, both ways
/// (<c>docs/mechanics/savegame.md</c>, section 24): one key of ours in the country's block, the decisions' names in
/// their order. A country with none writes nothing, and a faithful game never writes the key. A name the game no
/// longer has a decision for - a mod's newer version having dropped it - is left out and reported.
/// </summary>
public static class CSaveQuietDecisions
{
	public const string Key = "openhoi_quiet_decisions";

	public static void Write(CSaveTextWriter writer, CGameState state, CCountry country)
	{
		if (state.Rules.Enhanced && country.QuietDecisions.Count > 0)
		{
			writer.Words(Key, country.QuietDecisions);
		}
	}

	public static void Read(CGameState state, CCountry country, ClausewitzNode node, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		country.QuietDecisions.Clear();
		if (node.Get(Key) is not { } list)
		{
			return;
		}
		foreach (ClausewitzNode entry in list.Children)
		{
			string? name = entry.Value ?? entry.Key;
			if (string.IsNullOrEmpty(name))
			{
				continue;
			}
			if (CDecisions.Find(state, name) is null)
			{
				warn($"save: {country.Tag}'s '{Key}' names '{name}', which is no decision; left out");
				continue;
			}
			country.QuietDecisions.Add(name);
		}
	}
}
