using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the row a unit's panel has for one of its battles says in a tooltip: each side's units by
/// name and what they come to, the attacker's and then the defender's, with <c>COMB_TT_VS</c>
/// between. See <c>docs/mechanics/combatview.md</c>, section 12.
/// </summary>
public static class CBattleRowTooltip
{
	public static CTooltip For(Func<string, string> localise, CCombat combat)
	{
		string text = Side(localise, combat.Attacker) + "\n" + localise("COMB_TT_VS") + "\n" + Side(localise, combat.Defender);
		return new CTooltip { Text = text };
	}

	/// <summary>A side: its units by name, then a line of what they come to. Nothing for a side with nobody.</summary>
	public static string Side(Func<string, string> localise, CCombatSide side)
	{
		if (side.Units.Count == 0)
		{
			return "";
		}
		return Names(localise, side) + "\n" + Summary(localise, side);
	}

	/// <summary>
	/// Each unit as <c>@</c> and its country's tag, <c> £</c> and its first brigade's type - none
	/// for an army or above - a space and its name, joined by commas and <c>AND</c> before the
	/// last. A fleet's or an air unit's ships or wings are named each for itself, with the fleet's
	/// name, and the count that places the joins is of units, taken down once a ship - so one
	/// fleet's ships run together. The original's.
	/// </summary>
	public static string Names(Func<string, string> localise, CCombatSide side)
	{
		var text = new StringBuilder();
		int remaining = side.Units.Count - 1;
		void Join()
		{
			remaining--;
			if (remaining > 0)
			{
				text.Append(", ");
			}
			else if (remaining == 0)
			{
				text.Append(' ').Append(localise("AND")).Append(' ');
			}
		}

		foreach (CUnit unit in side.Units)
		{
			if (CMovementSpeed.IsLand(unit))
			{
				string key = CUnitHeaderTooltips.LevelOf(unit) >= 3 && unit.SubUnits.Count > 0 ? unit.SubUnits[0].Type.Key : "";
				text.Append('@').Append(unit.Owner.Value).Append(" £").Append(key).Append(' ').Append(unit.Name);
				Join();
				continue;
			}
			foreach (CSubUnit ship in unit.SubUnits)
			{
				text.Append('@').Append(unit.Owner.Value).Append(" £").Append(ship.Type.Key).Append(' ').Append(unit.Name);
				Join();
			}
		}
		return text.ToString();
	}

	/// <summary>
	/// The countries of a side's units, each once as they come - a rebel unit under its faction's name where it has one -
	/// joined by commas and <c>AND</c> before the last.
	/// </summary>
	public static string CountryNames(Func<string, string> localise, CGameState state, CCombatSide side)
	{
		var names = new List<string>();
		foreach (CUnit unit in side.Units)
		{
			string name = state.Country(unit.Owner)?.Name ?? unit.Owner.Value;
			if (!names.Contains(name))
			{
				names.Add(name);
			}
		}
		var text = new StringBuilder();
		for (int index = 0; index < names.Count; index++)
		{
			text.Append(names[index]);
			if (index + 2 < names.Count)
			{
				text.Append(", ");
			}
			else if (index + 2 == names.Count)
			{
				text.Append(' ').Append(localise("AND")).Append(' ');
			}
		}
		return text.ToString();
	}

	/// <summary>
	/// A side's units one to a line, as a battle's popup lists them: each name cut to twenty letters, and past the limit
	/// the table's <c>AND_MORE</c> in place of the rest.
	/// </summary>
	public static string ShortNames(Func<string, string> localise, CCombatSide side, int limit)
	{
		const int MostLetters = 20;
		var text = new StringBuilder();
		int written = 0;
		foreach (CUnit unit in side.Units)
		{
			bool land = CMovementSpeed.IsLand(unit);
			IEnumerable<string> keys = land
				? new[] { CUnitHeaderTooltips.LevelOf(unit) >= 3 && unit.SubUnits.Count > 0 ? unit.SubUnits[0].Type.Key : "" }
				: unit.SubUnits.Select(each => each.Type.Key);
			foreach (string key in keys)
			{
				if (written >= limit)
				{
					return text.Append(localise("AND_MORE")).ToString();
				}
				string name = unit.Name.Length > MostLetters ? unit.Name[..MostLetters] : unit.Name;
				text.Append('@').Append(unit.Owner.Value).Append(" £").Append(key).Append(' ').Append(name).Append('\n');
				written++;
			}
		}
		return text.ToString();
	}

	/// <summary>
	/// What a side comes to, by its first unit's kind: a fleet's ships by kind, an army's
	/// brigades, units and men, an air force's fighters, bombers and rockets. Every unit on the
	/// side is counted, of whatever kind.
	/// </summary>
	public static string Summary(Func<string, string> localise, CCombatSide side)
	{
		int capital = 0, screens = 0, transports = 0, submarines = 0, fighters = 0, bombers = 0, rockets = 0;
		int units = 0, brigades = 0;
		long strength = 0;
		foreach (CUnit unit in side.Units)
		{
			if (CMovementSpeed.IsLand(unit))
			{
				foreach (CSubUnit brigade in unit.SubUnits)
				{
					strength += brigade.Strength.Raw;
				}
				brigades += unit.SubUnits.Count;
				units++;
				continue;
			}
			foreach (CSubUnit each in unit.SubUnits)
			{
				CSubUnitType type = each.Type;
				if (type.Branch == EUnitBranch.Naval)
				{
					if (type.Flag("capital"))
					{
						capital++;
					}
					else if (type.Flag("is_sub"))
					{
						submarines++;
					}
					else if (each.Stats["transport_capability"] > CFixedPoint.Zero)
					{
						transports++;
					}
					else
					{
						screens++;
					}
				}
				else if (type.Branch == EUnitBranch.Air)
				{
					if (type.Flag("is_rocket"))
					{
						rockets++;
					}
					else if (type.Flag("is_bomber"))
					{
						bombers++;
					}
					else
					{
						fighters++;
					}
				}
			}
		}
		static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
		return side.Units[0].Kind switch
		{
			EUnitKind.Navy => CTextTemplate.Fill(localise("UNIT_ATTACK_INFO_NAVY"),
				("CAP", N(capital)), ("SCR", N(screens)), ("TRA", N(transports)), ("SUB", N(submarines))),
			EUnitKind.Air => CTextTemplate.Fill(localise("UNIT_ATTACK_INFO_AIR"),
				("FIG", N(fighters)), ("BOM", N(bombers)), ("ROC", N(rockets))),
			_ => CTextTemplate.Fill(localise("UNIT_ATTACK_INFO_ARMY"),
				("MEN", CBuilderText.Men(new CFixedPoint(strength))), ("UNIT", N(units)), ("BRIG", N(brigades))),
		};
	}
}
