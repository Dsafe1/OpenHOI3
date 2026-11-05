using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// The list of finished battles: an entry for every battle that ends, of every kind, kept
/// for a week. The air map mode counts its icons out of it. See
/// <c>docs/mechanics/airmap.md</c>, section 2.
/// </summary>
public sealed class CCombatHistory
{
	/// <summary>How long an entry is kept, in hours after the battle ended.</summary>
	public const int KeptHours = 168;

	private readonly List<CFinishedCombat> _entries = new();

	/// <summary>The battles that ended in the past week, in the order they ended.</summary>
	public IReadOnlyList<CFinishedCombat> Entries => _entries;

	/// <summary>Puts a battle that has just ended on the list, naming each side by its first country still in the fight.</summary>
	public void Record(CCombat combat, long hour)
	{
		_entries.Add(new CFinishedCombat(hour, combat.Kind, combat.Province,
			First(combat.Attacker.Standing), First(combat.Defender.Standing), combat.IsConvoyBattle));
	}

	/// <summary>
	/// Puts an entry a save holds at the end of the list, as it stands: a load neither sorts nor prunes, and the first
	/// hour after it prunes. See <c>docs/mechanics/savegame.md</c>, section 7.
	/// </summary>
	public void Add(CFinishedCombat entry)
	{
		_entries.Add(entry);
	}

	/// <summary>Empties the list, for a game loaded in its place.</summary>
	public void Clear()
	{
		_entries.Clear();
	}

	/// <summary>
	/// Takes off the front of the list what is not to be kept at this hour: first any entry
	/// that ended after it, then every one that ended more than a week before it.
	/// </summary>
	public void Prune(long hour)
	{
		while (_entries.Count > 0 && _entries[0].Hour > hour)
		{
			_entries.RemoveAt(0);
		}
		while (_entries.Count > 0 && _entries[0].Hour < hour - KeptHours)
		{
			_entries.RemoveAt(0);
		}
	}

	private static CCountryTag? First(List<CCountryTag> countries)
	{
		return countries.Count > 0 ? countries[0] : null;
	}
}
