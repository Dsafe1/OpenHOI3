using System;
using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads a battle's condition block - a tactic's <c>trigger</c> or a gainable trait's - into
/// a <see cref="CCombatCondition"/>. A key that is not understood is added to
/// <c>unknown</c> and never holds, so that nothing is handed out on the strength of a test
/// nothing made.
/// </summary>
public static class CCombatConditionReader
{
	public static CCombatCondition ReadGroup(ClausewitzNode block, ECombatCondition kind, SortedSet<string> unknown)
	{
		var group = new CCombatCondition(kind);
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is null)
			{
				continue;
			}
			string key = entry.Key.ToLowerInvariant();
			if (entry.IsBlock)
			{
				switch (key)
				{
					case "or":
						group.Children.Add(ReadGroup(entry, ECombatCondition.Any, unknown));
						break;
					case "and":
						group.Children.Add(ReadGroup(entry, ECombatCondition.All, unknown));
						break;
					case "not":
						group.Children.Add(ReadGroup(entry, ECombatCondition.None, unknown));
						break;
					default:
						unknown.Add(entry.Key);
						group.Children.Add(new CCombatCondition(ECombatCondition.Never));
						break;
				}
				continue;
			}

			string value = entry.Value ?? "";
			bool wanted = value.Equals("yes", StringComparison.OrdinalIgnoreCase);
			bool isYesNo = wanted || value.Equals("no", StringComparison.OrdinalIgnoreCase);
			bool isNumber = CFixedPoint.TryParse(value, out CFixedPoint amount);
			switch (key)
			{
				case "is_attacker" when isYesNo:
					group.Children.Add(new CCombatCondition(ECombatCondition.IsAttacker, wanted));
					break;
				case "frontage_full" when isYesNo:
					group.Children.Add(new CCombatCondition(ECombatCondition.FrontageFull, wanted));
					break;
				case "reserves" when isYesNo:
					group.Children.Add(new CCombatCondition(ECombatCondition.Reserves, wanted));
					break;
				case "has_armour_unit" when isYesNo:
					group.Children.Add(new CCombatCondition(ECombatCondition.HasArmourUnit, wanted));
					break;
				case "is_convoy" when isYesNo:
					group.Children.Add(new CCombatCondition(ECombatCondition.IsConvoy, wanted));
					break;
				case "is_winner" when isYesNo:
					group.Children.Add(new CCombatCondition(ECombatCondition.IsWinner, wanted));
					break;
				case "has_combined_arms":
					// The original never reads what follows the key.
					group.Children.Add(new CCombatCondition(ECombatCondition.HasCombinedArms));
					break;
				case "skill_advantage" when isNumber:
					group.Children.Add(new CCombatCondition(ECombatCondition.SkillAdvantage, amount: (int)amount.ToInt()));
					break;
				case "skill" when isNumber:
					group.Children.Add(new CCombatCondition(ECombatCondition.Skill, amount: (int)amount.ToInt()));
					break;
				case "province_temperature" when isNumber:
					group.Children.Add(new CCombatCondition(ECombatCondition.ProvinceTemperature, amount: (int)amount.ToInt()));
					break;
				case "trait" when value.Length > 0:
					group.Children.Add(new CCombatCondition(ECombatCondition.Trait, name: value));
					break;
				case "terrain" when value.Length > 0:
					group.Children.Add(new CCombatCondition(ECombatCondition.Terrain, name: value));
					break;
				case "has_combat_modifier" when CCombatModifierKeys.IsKnown(value):
					group.Children.Add(new CCombatCondition(ECombatCondition.HasCombatModifier, name: value));
					break;
				default:
					unknown.Add(value.Length > 0 ? $"{entry.Key} = {value}" : entry.Key);
					group.Children.Add(new CCombatCondition(ECombatCondition.Never));
					break;
			}
		}
		return group;
	}
}
