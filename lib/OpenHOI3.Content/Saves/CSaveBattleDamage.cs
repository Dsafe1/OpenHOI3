using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// The damage units have dealt and taken in the land battles being fought, in an enhanced game's save, both ways
/// (<c>docs/mechanics/savegame.md</c>, section 21; <see cref="CBattleDamage"/>): one list of ours at the top, ten whole
/// numbers a unit - its number; the hour its last-hour figures are of; and the eight figures, the battle's four and
/// the hour's four, each as strength dealt, strength taken, organisation dealt, organisation taken. The battles' own
/// block stays the original's. A game with nothing to keep writes no key, and a faithful game none.
/// </summary>
public static class CSaveBattleDamage
{
	public const string Key = "openhoi_battle_damage";

	private const int Each = 10;

	public static void Write(CSaveTextWriter writer, CGameState state)
	{
		if (!state.Rules.Enhanced)
		{
			return;
		}
		var values = new List<long>();
		foreach (CCombat combat in state.Combats)
		{
			foreach (CUnit unit in combat.Attacker.Units.Concat(combat.Defender.Units))
			{
				if (unit.BattleDamage is not { IsEmpty: false } damage || !ReferenceEquals(damage.Combat, combat))
				{
					continue;
				}
				values.AddRange(new[]
				{
					unit.Id, damage.Hour, damage.StrengthDealt, damage.StrengthTaken, damage.OrganisationDealt,
					damage.OrganisationTaken, damage.HourStrengthDealt, damage.HourStrengthTaken,
					damage.HourOrganisationDealt, damage.HourOrganisationTaken,
				});
			}
		}
		if (values.Count != 0)
		{
			writer.Numbers(Key, values);
		}
	}

	/// <summary>
	/// The list read back, once the units and the battles are: each unit found by its number and given its figures for
	/// the land battle it is in. A unit that is gone or in no land battle is passed over.
	/// </summary>
	public static void Read(ClausewitzNode node, IReadOnlyDictionary<int, CUnit> units)
	{
		List<long> values = node.Children.Select(value => CSaveValues.Whole(value.Value)).ToList();
		for (int at = 0; at + Each <= values.Count; at += Each)
		{
			if (!units.TryGetValue((int)values[at], out CUnit? unit)
				|| unit.Combats.FirstOrDefault(combat => combat.Kind == ECombatKind.Land) is not { } combat)
			{
				continue;
			}
			unit.BattleDamage = new CBattleDamage
			{
				Combat = combat, Hour = values[at + 1], StrengthDealt = values[at + 2], StrengthTaken = values[at + 3],
				OrganisationDealt = values[at + 4], OrganisationTaken = values[at + 5], HourStrengthDealt = values[at + 6],
				HourStrengthTaken = values[at + 7], HourOrganisationDealt = values[at + 8], HourOrganisationTaken = values[at + 9],
			};
		}
	}
}
