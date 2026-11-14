using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
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
	/// <param name="player">Whose knowledge decides which land units are drawn by the unknown counter; nobody's knows everything.</param>
	public static CTooltip For(Func<string, string> localise, CGameState state, CCombat combat, CCountryTag? player)
	{
		string text = Side(localise, state, combat.Attacker, player) + "\n" + localise("COMB_TT_VS") + "\n"
			+ Side(localise, state, combat.Defender, player);
		if (CBombing.IsBombing(combat.Kind))
		{
			text += Bombed(localise, state, combat);
		}
		return new CTooltip { Text = text };
	}

	/// <summary>
	/// What of the province a bombing bombs, by the first bomber's mission: a space and each building it takes that the
	/// province has built any of; then a space, <c>BOMB_PROD_OFF</c>, a space and each good whose stockpile it takes; then
	/// the same with <c>BOMB_POOL_OFF</c> for each good whose production it takes that the province can make - the two
	/// keys each over the other's list, as the original writes them. Each list joined with commas, <c>AND</c> before its
	/// last and a full stop after it; nothing for a list with nothing in it.
	/// </summary>
	public static string Bombed(Func<string, string> localise, CGameState state, CCombat combat)
	{
		if (combat.Attacker.Units.Count == 0)
		{
			return "";
		}
		EUnitOrder order = combat.Attacker.Units[0].Order;
		Sim.Provinces.CProvince province = state.Province(combat.Province);
		var buildings = new List<string>();
		foreach (Sim.Provinces.CBuilding building in state.Buildings)
		{
			if (Sim.Missions.CBombingOrders.BombsBuilding(order, building) && province.BuildingMaxLevel(building).Raw > 0)
			{
				buildings.Add(localise(building.Key));
			}
		}
		var stockpiles = new List<string>();
		var production = new List<string>();
		for (int index = 0; index < Sim.Economy.CGoodsPool.Count; index++)
		{
			var goods = (Sim.Economy.EGoods)index;
			if (Sim.Missions.CBombingOrders.BombsStockpile(order, goods))
			{
				stockpiles.Add(localise(CAirCombatText.GoodsKeys[index]));
			}
			if (Sim.Missions.CBombingOrders.BombsProduction(order, goods) && province.MaxProducing[goods].Raw > 0)
			{
				production.Add(localise(CAirCombatText.GoodsKeys[index]));
			}
		}
		var text = new StringBuilder();
		if (buildings.Count > 0)
		{
			text.Append(' ').Append(Joined(localise, buildings));
		}
		if (stockpiles.Count > 0)
		{
			text.Append(' ').Append(localise("BOMB_PROD_OFF")).Append(' ').Append(Joined(localise, stockpiles));
		}
		if (production.Count > 0)
		{
			text.Append(' ').Append(localise("BOMB_POOL_OFF")).Append(' ').Append(Joined(localise, production));
		}
		return text.ToString();
	}

	/// <summary>Names joined: a comma and a space between, a space, <c>AND</c> and a space before the last, a full stop after it.</summary>
	private static string Joined(Func<string, string> localise, List<string> names)
	{
		var text = new StringBuilder();
		for (int index = 0; index < names.Count; index++)
		{
			text.Append(names[index]);
			int left = names.Count - 1 - index;
			text.Append(left > 1 ? ", " : left == 1 ? " " + localise("AND") + " " : ".");
		}
		return text.ToString();
	}

	/// <summary>A side: its units by name, then a line of what they come to. Nothing for a side with nobody.</summary>
	public static string Side(Func<string, string> localise, CGameState state, CCombatSide side, CCountryTag? player)
	{
		if (side.Units.Count == 0)
		{
			return "";
		}
		return Names(localise, state, side, player) + "\n" + Summary(localise, side);
	}

	/// <summary>
	/// Each unit as <c>@</c> and its country's tag, <c> £</c> and its counter's key - a land unit's first brigade's
	/// type, or <c>unknown</c> where the player knows it under intel level 3 (<see cref="CCombatViewText.CounterKey"/>) -
	/// a space and its name, joined by commas and <c>AND</c> before the last. A fleet's or an air unit's ships or wings are
	/// named each for itself, whatever is known, with the fleet's name, and the count that places the joins is of units,
	/// taken down once a ship - so one fleet's ships run together. The original's.
	/// </summary>
	public static string Names(Func<string, string> localise, CGameState state, CCombatSide side, CCountryTag? player)
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
				string key = CCombatViewText.CounterKey(state, unit, player);
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
	public static string ShortNames(Func<string, string> localise, CGameState state, CCombatSide side, int limit, CCountryTag? player)
	{
		const int MostLetters = 20;
		var text = new StringBuilder();
		int written = 0;
		foreach (CUnit unit in side.Units)
		{
			bool land = CMovementSpeed.IsLand(unit);
			IEnumerable<string> keys = land
				? new[] { CCombatViewText.CounterKey(state, unit, player) }
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
