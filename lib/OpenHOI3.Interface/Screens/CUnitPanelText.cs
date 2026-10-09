using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the panel of a selected unit says: the figures, the texts and the frames of its
/// rows, each as the original works it out. See <c>docs/mechanics/unitpanel.md</c>.
///
/// An icon's frames are counted from one, as the original counts them.
/// </summary>
public static class CUnitPanelText
{
	public const string NoLeaderKey = "NO_LEADER";
	public const string SuppliedFromKey = "SUPPLIED_FROM";
	public const string SpeedUnitKey = "KPH";
	public const string MoveOrderKey = "MOVE_ORDER_D";
	public const string NoOrderKey = "NO_ORDERS";
	public const string RebaseKey = "ORDER_REBASE";
	public const string AirRebaseKey = "ORDER_AIR_REBASE_SHORT_DESC";
	public const string LandCountKey = "LAND_COUNT";
	public const string NavalCountKey = "NAVAL_COUNT";
	public const string AirCountKey = "AIR_COUNT";

	/// <summary>The header's activity strip: nothing, and what it shows for a unit on the march and for one that cannot act.</summary>
	public const int ActivityNone = 1;
	public const int ActivityMoving = 5;
	public const int ActivityUnfit = 6;

	/// <summary>What a subordinate's level is called in the count over the list of them, by the kind of unit.</summary>
	private static readonly (EUnitKind Kind, string Key)[] LevelKeys =
	{
		(EUnitKind.ArmyGroup, "NUM_GROUPS"), (EUnitKind.Army, "NUM_ARMIES"),
		(EUnitKind.Corps, "NUM_CORPS"), (EUnitKind.Division, "NUM_DIVISONS"),
	};

	private const long Scale = CFixedPoint.Scale;

	// ---- the header

	/// <summary>
	/// The frame of the activity strip. The original also has a white arrow for a
	/// retreat, a star and a red arrow for the two sides of a battle, and gives the
	/// cross to a unit held by an attack delay; none of those is built.
	/// </summary>
	public static int ActivityFrame(CUnit unit)
	{
		if (unit.IsMoving)
		{
			return ActivityMoving;
		}
		return unit.SubUnits.Count == 0 ? ActivityUnfit : ActivityNone;
	}

	/// <summary>A land unit's men: a hundred to each point of each brigade's strength, each brigade cut to a whole man.</summary>
	public static long Men(CUnit unit)
	{
		long men = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			men += subUnit.Strength.Raw * 100 / Scale;
		}
		return men;
	}

	/// <summary>
	/// The header's strength: a land unit's men, whole under a thousand and with a comma
	/// before the last three digits above. Nothing for a fleet or an air unit, whose
	/// figure was not read.
	/// </summary>
	public static string Strength(CUnit unit)
	{
		if (!CMovementSpeed.IsLand(unit))
		{
			return "";
		}
		long men = Men(unit);
		if (men < 1000)
		{
			return men.ToString(CultureInfo.InvariantCulture);
		}
		return (men / 1000).ToString(CultureInfo.InvariantCulture) + ","
			+ (men % 1000).ToString(CultureInfo.InvariantCulture).PadLeft(3, '0');
	}

	/// <summary>The leader's line. Leaders are not built, so every unit has none.</summary>
	public static string Leader(Func<string, string> localise)
	{
		return localise(NoLeaderKey);
	}

	/// <summary>
	/// The most organisation a unit's bar is drawn against: its brigades' own figures
	/// averaged, more or less by the country's modifier for its branch. Officers have no
	/// part in this one.
	/// </summary>
	public static CFixedPoint MaxOrganisation(CCountry owner, CUnit unit)
	{
		if (unit.SubUnits.Count == 0)
		{
			return CFixedPoint.Zero;
		}
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			sum += subUnit.Stats[CSubUnitType.DefaultOrganisation].Raw;
		}
		long average = sum / unit.SubUnits.Count;
		string branch = unit.Kind switch
		{
			EUnitKind.Navy => COrganisation.NavalOrganisation,
			EUnitKind.Air => COrganisation.AirOrganisation,
			_ => COrganisation.LandOrganisation,
		};
		return new CFixedPoint(average * (Scale + owner.Modifier[branch].Raw) / Scale);
	}

	/// <summary>The header's organisation bar, in whole hundredths: the brigades' average over the most, never past the full bar.</summary>
	public static int OrganisationBar(CCountry owner, CUnit unit)
	{
		if (unit.SubUnits.Count == 0)
		{
			return 0;
		}
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			sum += subUnit.Organisation.Raw;
		}
		return Share(sum / unit.SubUnits.Count, MaxOrganisation(owner, unit).Raw);
	}

	/// <summary>The header's strength bar: the brigades' strength over what their types are made with.</summary>
	public static int StrengthBar(CUnit unit)
	{
		long strength = 0;
		long most = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			strength += subUnit.Strength.Raw;
			most += subUnit.Stats[CSubUnitType.MaxStrength].Raw;
		}
		return Share(strength, most);
	}

	/// <summary>A part of a whole as a bar's hundredths, cut, with the whole raised to the part where it is the smaller.</summary>
	public static int Share(long part, long whole)
	{
		if (part <= 0)
		{
			return 0;
		}
		if (whole < part)
		{
			whole = part;
		}
		return (int)(part * 100 / whole);
	}

	// ---- the status

	/// <summary>Where it is: the name of its province.</summary>
	public static string Location(CGameState state, CUnit unit)
	{
		return unit.Location > 0 && unit.Location < state.Provinces.Count ? state.Province(unit.Location).Name : "";
	}

	/// <summary>
	/// What its province wears it down by. Attrition is not built: a unit loses nothing,
	/// and one with no brigades has no figure, as in the original.
	/// </summary>
	public static string Attrition(CUnit unit)
	{
		return unit.SubUnits.Count == 0 ? "?%" : "0%";
	}

	/// <summary>
	/// Where a land unit's supplies come from: its country's capital, which is where
	/// every province is supplied from until there is a supply network - or question
	/// marks where somebody who is no friend holds it.
	/// </summary>
	public static string SuppliedFrom(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit)
	{
		string where = " ???";
		if (!CMovementSpeed.IsLand(unit))
		{
			// A fleet and an air unit: the base, and how far from it the unit reaches.
			if (unit.Base > 0 && unit.Base < state.Provinces.Count)
			{
				where = " " + state.Province(unit.Base).Name + "("
					+ (CUnitOrders.RangeOf(unit).Raw / Scale).ToString(CultureInfo.InvariantCulture) + "km)";
			}
			return localise(SuppliedFromKey) + where;
		}
		if (owner.Capital > 0 && owner.Capital < state.Provinces.Count)
		{
			CProvince capital = state.Province(owner.Capital);
			if (CMovementRules.IsFriendly(state, owner, capital.Controller))
			{
				where = " " + capital.Name;
			}
		}
		return localise(SuppliedFromKey) + where;
	}

	/// <summary>
	/// The speed the panel shows: the unit's top speed times everything that scales it,
	/// in the figure the unit files are written in - twenty times what an hour of the
	/// march adds, in the base game.
	/// </summary>
	public static CFixedPoint ShownSpeed(CGameState state, CCountry owner, CUnit unit)
	{
		return new CFixedPoint(CMovementSpeed.SlowestBrigade(unit).Raw * CMovementSpeed.Modifier(state, owner, unit) / Scale);
	}

	/// <summary>That speed as text, with the unit it is measured in straight after it.</summary>
	public static string Speed(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit)
	{
		return CBuilderText.ExactSpeed(ShownSpeed(state, owner, unit)) + localise(SpeedUnitKey);
	}

	/// <summary>A unit's width in a battle: its brigades' added up.</summary>
	public static CFixedPoint Width(CUnit unit)
	{
		return Sum(unit, CSubUnitType.CombatWidth);
	}

	/// <summary>The frame of the width strip: one for under 2, and one more for each 2 up to 8 and beyond.</summary>
	public static int WidthFrame(CFixedPoint width)
	{
		long raw = width.Raw;
		return raw < 2 * Scale ? 1 : raw < 4 * Scale ? 2 : raw < 6 * Scale ? 3 : raw < 8 * Scale ? 4 : 5;
	}

	/// <summary>What a land unit weighs aboard a ship, whole; nothing for the others.</summary>
	public static string Weight(CUnit unit)
	{
		return CMovementSpeed.IsLand(unit)
			? (Sum(unit, CSubUnitType.TransportWeight).Raw / Scale).ToString(CultureInfo.InvariantCulture)
			: "";
	}

	/// <summary>
	/// The supply and the fuel bars, in hundredths. In the original they measure what
	/// lies in the unit's province against what the units there need for some days; a
	/// province's need is not kept here, and with no need the original's bar is full.
	/// </summary>
	public static int SupplyBar => 100;

	// ---- the order

	/// <summary>
	/// What the unit is doing: on the march, the province it will be in next - not the
	/// one at the end of its way. The original has more to say of a unit in a battle,
	/// in retreat, short of men, fuel or supplies; none of that is built, and what it
	/// calls a unit with nothing to do was not read.
	/// </summary>
	public static string Order(Func<string, string> localise, CGameState state, CUnit unit)
	{
		// An order says for itself what it is, and names the province it is for.
		if (unit.Order != EUnitOrder.None && unit.OrderProvince > 0 && unit.OrderProvince < state.Provinces.Count)
		{
			string where = state.Province(unit.OrderProvince).Name;
			switch (unit.Order)
			{
				case EUnitOrder.Move:
					return CTextTemplate.Fill(localise(MoveOrderKey), ("WHERE", where));
				case EUnitOrder.Rebase:
					return localise(RebaseKey) + " :" + where;
				case EUnitOrder.RebaseAir:
					return CTextTemplate.Fill(localise(AirRebaseKey), ("PROV", where));
			}
		}
		// Ours for a fleet sent straight somewhere, which has no order: as a land unit.
		if (unit.IsMoving)
		{
			return CTextTemplate.Fill(localise(MoveOrderKey), ("WHERE", state.Province(unit.Path[0]).Name));
		}
		return localise(NoOrderKey);
	}

	// ---- the brigades

	/// <summary>
	/// The heading over a unit's brigades: how many a land unit has; a fleet's capital
	/// ships, screens and others; an air unit's fighters and bombers. Which ship counts
	/// as which was not read - ours: a capital ship is one its type calls <c>capital</c>,
	/// the others are submarines and transports, and the rest are screens; a bomber is
	/// a wing its type calls <c>is_bomber</c>.
	/// </summary>
	public static string BrigadesHeader(Func<string, string> localise, CUnit unit)
	{
		string Count(Func<CSubUnit, bool> which)
		{
			int count = 0;
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				count += which(subUnit) ? 1 : 0;
			}
			return count.ToString(CultureInfo.InvariantCulture);
		}

		switch (unit.Kind)
		{
			case EUnitKind.Navy:
				return CTextTemplate.Fill(localise(NavalCountKey),
					("CAPITAL", Count(IsCapital)),
					("ESCORTS", Count(ship => !IsCapital(ship) && !IsOtherShip(ship))),
					("OTHER", Count(ship => !IsCapital(ship) && IsOtherShip(ship))));
			case EUnitKind.Air:
				return CTextTemplate.Fill(localise(AirCountKey),
					("FIGHTER", Count(wing => !wing.Type.Flag("is_bomber"))),
					("BOMBER", Count(wing => wing.Type.Flag("is_bomber"))));
			default:
				return CTextTemplate.Fill(localise(LandCountKey), ("NUM", unit.SubUnits.Count.ToString(CultureInfo.InvariantCulture)));
		}
	}

	private static bool IsCapital(CSubUnit ship)
	{
		return ship.Type.Flag("capital");
	}

	private static bool IsOtherShip(CSubUnit ship)
	{
		return ship.Type.Flag("is_sub") || ship.Stats["transport_capability"] > CFixedPoint.Zero;
	}

	/// <summary>The sprite of a brigade's picture, or an empty string.</summary>
	public static string BrigadeSprite(CSubUnit subUnit)
	{
		return CCounterText.ListSprite + subUnit.Type.Key;
	}

	/// <summary>Which model of its type a brigade is, as a Roman numeral: I for the first and for none.</summary>
	public static string BrigadeLevel(CSubUnit subUnit)
	{
		return CRomanNumerals.Format(Math.Max(subUnit.HistoricalModel, 0) + 1);
	}

	/// <summary>A brigade's men.</summary>
	public static string BrigadeMen(CSubUnit subUnit)
	{
		return CBuilderText.Men(subUnit.Strength);
	}

	/// <summary>
	/// The frame of a brigade's stars: none under an experience of one, and otherwise
	/// two more than its experience over ten rounded to the nearest whole - to the even
	/// one from a half - up to the eleventh.
	/// </summary>
	public static int ExperienceFrame(CFixedPoint experience)
	{
		if (experience.Raw < Scale)
		{
			return 1;
		}
		long tenths = experience.Raw / 10;                    // experience over ten, in thousandths
		long whole = tenths / Scale;
		long rest = tenths % Scale;
		if (rest > Scale / 2 || (rest == Scale / 2 && whole % 2 == 1))
		{
			whole++;
		}
		return (int)Math.Min(whole + 2, 11);
	}

	/// <summary>A brigade's organisation bar: what it has over the most it may have, officers and all.</summary>
	public static int BrigadeOrganisationBar(CGameState state, CCountry owner, CSubUnit subUnit)
	{
		return Share(subUnit.Organisation.Raw, COrganisation.Ceiling(state, owner, subUnit).Raw);
	}

	/// <summary>A brigade's strength bar: what it has over what its type is made with.</summary>
	public static int BrigadeStrengthBar(CSubUnit subUnit)
	{
		return Share(subUnit.Strength.Raw, subUnit.Stats[CSubUnitType.MaxStrength].Raw);
	}

	/// <summary>How high a window of rows is: its label, and its rows as far as the window's own height has room for.</summary>
	public static int RowsHeight(int label, int rows, int rowHeight, int windowHeight)
	{
		return label + Math.Max(0, Math.Min(rows * rowHeight, windowHeight - label));
	}

	// ---- the units under it

	/// <summary>The units directly under one, in the order it holds them.</summary>
	public static IReadOnlyList<CUnit> Children(CUnit unit)
	{
		return unit.Children;
	}

	/// <summary>
	/// The heading over them: how many there are at each level, the highest first -
	/// "2 Corps, 1 Division(s)".
	/// </summary>
	public static string ChildrenHeader(Func<string, string> localise, CUnit unit)
	{
		var parts = new List<string>();
		foreach ((EUnitKind kind, string key) in LevelKeys)
		{
			int count = 0;
			foreach (CUnit child in unit.Children)
			{
				if (child.Kind == kind)
				{
					count++;
				}
			}
			if (count > 0)
			{
				parts.Add(count.ToString(CultureInfo.InvariantCulture) + " " + localise(key));
			}
		}
		return string.Join(", ", parts);
	}

	/// <summary>The men of every land unit under one, at any depth; nothing for a unit that is not a land unit.</summary>
	public static string ChildrenMen(CUnit unit)
	{
		return CMovementSpeed.IsLand(unit) ? CBuilderText.Men(new CFixedPoint(StrengthBelow(unit))) : "";
	}

	private static long StrengthBelow(CUnit unit)
	{
		long strength = 0;
		foreach (CUnit child in unit.Children)
		{
			if (CMovementSpeed.IsLand(child))
			{
				foreach (CSubUnit subUnit in child.SubUnits)
				{
					strength += subUnit.Strength.Raw;
				}
			}
			strength += StrengthBelow(child);
		}
		return strength;
	}

	/// <summary>A subordinate's own men, without what is under it; nothing for a unit that is not a land unit.</summary>
	public static string ChildMen(CUnit unit)
	{
		if (!CMovementSpeed.IsLand(unit))
		{
			return "";
		}
		long strength = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			strength += subUnit.Strength.Raw;
		}
		return CBuilderText.Men(new CFixedPoint(strength));
	}

	// ---- the buttons

	/// <summary>
	/// The one button of five a unit's level shows: a division makes a corps, a corps an
	/// army, an army an army group, an army group a theatre, and a theatre is edited.
	/// Empty for a fleet or an air unit.
	/// </summary>
	public static string LevelButton(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Division => "create_corps",
			EUnitKind.Corps => "create_army",
			EUnitKind.Army => "create_armygroup",
			EUnitKind.ArmyGroup => "create_theatre",
			EUnitKind.Theatre => "edit_theatre",
			_ => "",
		};
	}

	/// <summary>The five, for hiding the four that are not it.</summary>
	public static readonly IReadOnlyList<string> LevelButtons = new[]
	{
		"create_corps", "create_army", "create_armygroup", "create_theatre", "edit_theatre",
	};

	private static CFixedPoint Sum(CUnit unit, string statistic)
	{
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			sum += subUnit.Stats[statistic].Raw;
		}
		return new CFixedPoint(sum);
	}
}
