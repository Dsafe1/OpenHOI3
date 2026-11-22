using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// An enhanced game's record of every battle that has ended (<c>docs/mechanics/combatreports.md</c>): ours, in the
/// place of the files BlackICE's DLL keeps beside the original, which holds a week of finished battles with one side
/// named and no losses (<see cref="CCombatHistory"/>). A report is added as a battle ends and none is ever taken
/// off: the record is the game's, so a game loaded from an earlier save has that save's record and no other.
///
/// Nothing in the game reads it - it is for a page to show - so it is in neither the state's hash nor the play
/// checksum; it is in an enhanced save. A faithful game keeps none.
/// </summary>
public sealed class CCombatReports
{
	private readonly List<CCombatReport> _entries = new();

	/// <summary>The reports in the order the battles ended.</summary>
	public IReadOnlyList<CCombatReport> Entries => _entries;

	/// <summary>
	/// A battle that has just ended, written down where the game is an enhanced one. A side is named by the first
	/// country it fought as, which a beaten side keeps; it won where it alone still has a country in the fight.
	/// </summary>
	public void Record(CGameState state, CCombat combat)
	{
		if (!state.Rules.Enhanced)
		{
			return;
		}
		bool attackerStands = combat.Attacker.Standing.Count > 0;
		bool defenderStands = combat.Defender.Standing.Count > 0;
		ECombatWinner winner = attackerStands == defenderStands ? ECombatWinner.Nobody
			: attackerStands ? ECombatWinner.Attacker : ECombatWinner.Defender;
		_entries.Add(new CCombatReport(state.TotalHours, combat.Kind, combat.Province,
			First(combat.Attacker), First(combat.Defender),
			combat.Attacker.Losses.Raw, Men(combat.Attacker), combat.Defender.Losses.Raw, Men(combat.Defender), winner));
	}

	/// <summary>A report as a save has it.</summary>
	public void Add(CCombatReport report)
	{
		_entries.Add(report);
	}

	public void Clear()
	{
		_entries.Clear();
	}

	/// <summary>
	/// A country's battles that ended from one hour to another, both among them, counted: of one kind, or of every
	/// kind where none is named.
	/// </summary>
	public CCombatTally Tally(CCountryTag country, long fromHour, long toHour, ECombatKind? kind = null)
	{
		var tally = new CCombatTally();
		foreach (CCombatReport report in _entries)
		{
			if (report.Hour < fromHour || report.Hour > toHour || (kind is { } wanted && report.Kind != wanted) || !report.Involves(country))
			{
				continue;
			}
			bool attacked = report.Attacker == country;
			tally.Combats++;
			if (attacked)
			{
				tally.AsAttacker++;
			}
			else
			{
				tally.AsDefender++;
			}
			if (report.Winner != ECombatWinner.Nobody)
			{
				if (report.WonBy(country))
				{
					tally.Won++;
				}
				else
				{
					tally.Lost++;
				}
			}
			tally.Losses += attacked ? report.AttackerLosses : report.DefenderLosses;
			tally.Kills += attacked ? report.DefenderLosses : report.AttackerLosses;
		}
		return tally;
	}

	/// <summary>A country's last battles, the newest first, at the most so many.</summary>
	public List<CCombatReport> Last(CCountryTag country, int count)
	{
		var last = new List<CCombatReport>();
		for (int i = _entries.Count - 1; i >= 0 && last.Count < count; i--)
		{
			if (_entries[i].Involves(country))
			{
				last.Add(_entries[i]);
			}
		}
		return last;
	}

	private static CCountryTag? First(CCombatSide side)
	{
		return side.Countries.Count > 0 ? side.Countries[0] : null;
	}

	/// <summary>
	/// How many a side had in the fight, as its battle's message counts them: each type's tally cut to whole ones and
	/// those added, a type at nought or below left out.
	/// </summary>
	private static long Men(CCombatSide side)
	{
		long men = 0;
		foreach (CFixedPoint size in side.Size.Values)
		{
			if (size.Raw > 0)
			{
				men += size.Raw / CFixedPoint.Scale;
			}
		}
		return men;
	}
}
