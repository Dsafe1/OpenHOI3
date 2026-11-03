using System;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the outliner's rows write: a section's title, a unit's name and its figure, a battle's figure and an allied
/// objective's province, with the original's colours. See <c>docs/mechanics/outliner.md</c>, sections 2 and 5 to 7.
/// </summary>
public static class COutlinerText
{
	/// <summary>A name longer than this is cut to three less and <c>...</c>.</summary>
	public const int NameLength = 21;

	/// <summary>The title of each section, by its number.</summary>
	public static string TitleKey(EOutlinerSection section) => section switch
	{
		EOutlinerSection.LandCombat => "OL_COMBATS",
		EOutlinerSection.NavalCombat => "OL_NAVAL_COMBAT",
		EOutlinerSection.AirCombat => "OL_AIR_COMBAT",
		EOutlinerSection.LandUnits => "ARMIES",
		EOutlinerSection.NavalUnits => "NAVIES",
		EOutlinerSection.AirUnits => "OL_AIR_UNITS",
		EOutlinerSection.AlliedObjectives => "OL_ALLIED_OBJECTIVE",
		EOutlinerSection.OurBombings => "OL_WE_BOMB",
		EOutlinerSection.HostileBombing => "OL_BOMB_ON_US",
		_ => "",
	};

	/// <summary>The switch at the top of the window that turns a section on and off.</summary>
	public static string SwitchName(EOutlinerSection section) => section switch
	{
		EOutlinerSection.LandCombat => "outliner_view_landcombat",
		EOutlinerSection.NavalCombat => "outliner_view_navalcombat",
		EOutlinerSection.AirCombat => "outliner_view_aircombat",
		EOutlinerSection.LandUnits => "outliner_view_landunits",
		EOutlinerSection.NavalUnits => "outliner_view_navalunits",
		EOutlinerSection.AirUnits => "outliner_view_airunits",
		EOutlinerSection.AlliedObjectives => "outliner_view_ao",
		// The two bombing switches the other way about from their names, as the original has them.
		EOutlinerSection.OurBombings => "outliner_view_bombonus",
		EOutlinerSection.HostileBombing => "outliner_view_bombonthem",
		_ => "",
	};

	/// <summary>A name cut to fit: past 21 characters, its first 18 and <c>...</c>.</summary>
	public static string ShortName(string name)
	{
		return name.Length > NameLength ? name[..(NameLength - 3)] + "..." : name;
	}

	/// <summary>
	/// A unit's row name: cut, and where the unit is in one of the player's selection groups, the first such after it as
	/// <c>(§Yn§W) </c> - no space before it, one after.
	/// </summary>
	public static string UnitName(CCountry player, CUnit unit)
	{
		string name = ShortName(unit.Name);
		for (int group = 0; group < player.SelectionGroups.Length; group++)
		{
			if (player.SelectionGroups[group].Contains(unit.Id))
			{
				return name + "(§Y" + group + "§W) ";
			}
		}
		return name;
	}

	/// <summary>
	/// A unit's figure: on a row of the land section, whatever the unit, its strength as a share of the most its brigades
	/// may have, in percent to one decimal - red where <paramref name="made"/> and it is under its shatter threshold; on a
	/// fleet's or an air unit's row, its ships or wings as a whole number.
	/// </summary>
	public static string UnitFigure(CGameState state, CUnit unit, bool landRow, bool made)
	{
		if (!landRow)
		{
			return unit.SubUnits.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}
		string figure = CNumberText.Format(new CFixedPoint(StrengthShare(unit) * 100), 1) + "%";
		if (made && state.Country(unit.Owner) is { } owner
			&& CCombatDamage.StrengthShare(unit) < CCombatDamage.ShatterThreshold(state, owner, unit))
		{
			return "§R" + figure + "§W";
		}
		return figure;
	}

	/// <summary>A land unit's strength in thousandths of the most its brigades may have; a whole where that is nothing.</summary>
	public static long StrengthShare(CUnit unit)
	{
		long most = CUnitTotals.Sum(unit, CSubUnitType.MaxStrength).Raw;
		return most == 0 ? CFixedPoint.Scale : CUnitTotals.Strength(unit).Raw * CFixedPoint.Scale / most;
	}

	/// <summary>Whether the player is among the attackers still fighting: fixed when a battle's row is made.</summary>
	public static bool PlayerAttacks(CCombat combat, CCountryTag player)
	{
		return combat.Attacker.Standing.Contains(player);
	}

	/// <summary>
	/// A battle's figure: a land battle's attacker's share in whole percent, green where the player attacks and negated in
	/// red where it defends; a sea or an air battle's ships or planes, the attacker's first and the player's green; a
	/// bombing's nothing.
	/// </summary>
	public static string CombatFigure(CCombat combat, bool playerAttacks)
	{
		if (CBombing.IsBombing(combat.Kind))
		{
			return "";
		}
		if (combat.Kind == ECombatKind.Land)
		{
			long share = CCombatShares.AttackerShare(combat);
			return playerAttacks
				? "§G" + CNumberText.Format(new CFixedPoint(share * 100), 0) + "%§W"
				: "§R" + CNumberText.Format(new CFixedPoint(-share * 100), 0) + "%§W";
		}
		int attackers = Count(combat.Attacker);
		int defenders = Count(combat.Defender);
		return playerAttacks
			? "§G" + attackers + "§W - §R" + defenders + "§W"
			: "§R" + attackers + "§W - §G" + defenders + "§W";
	}

	/// <summary>A side's ships or planes: its units' brigades, there being nothing else in a sea or an air battle.</summary>
	public static int Count(CCombatSide side)
	{
		int count = 0;
		foreach (CUnit unit in side.Units)
		{
			count += unit.SubUnits.Count;
		}
		return count;
	}

	/// <summary>An allied objective's row: its province, red where it was asked of the player, green where the player asked.</summary>
	public static string ObjectiveText(CGameState state, int province, bool askedOfPlayer)
	{
		return (askedOfPlayer ? "§R" : "§G") + state.Province(province).Name;
	}

	/// <summary>A section's title.</summary>
	public static string Title(Func<string, string> localise, EOutlinerSection section) => localise(TitleKey(section));
}
