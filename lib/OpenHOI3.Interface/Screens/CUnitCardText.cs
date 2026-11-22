using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the card of a whole unit says - an enhanced game's, opened from the ribbon over a unit's brigades: the brigade
/// card's window and rows, each figure the unit's as the game makes it from its brigades, and what the card of one
/// brigade does not say of what the unit cost. See <c>docs/mechanics/brigadecard.md</c>, section 7.
/// </summary>
public static class CUnitCardText
{
	/// <summary>The line under the picture, where a brigade's card names its model.</summary>
	public const string EntireUnit = "OPENHOI_UNITCARD_ENTIRE";

	/// <summary>The heading over what the unit cost.</summary>
	public const string Cost = "OPENHOI_UNITCARD_COST";

	public const string Officers = "OPENHOI_UNITCARD_OFFICERS";
	public const string Manpower = "OPENHOI_UNITCARD_MANPOWER";
	public const string BuildCost = "OPENHOI_UNITCARD_IC";
	public const string BuildTime = "OPENHOI_UNITCARD_TIME";

	/// <summary>What the ribbon over the brigades says under the mouse.</summary>
	public const string RibbonTip = "OPENHOI_UNITCARD_TIP";

	private static readonly (string Adjuster, string Label)[] Circumstances =
	{
		("night", "NIGHT_ADJUSTER"), ("fort", "FORT_ADJUSTER"), ("river", "RIVER_ADJUSTER"), ("amphibious", "AMPH_ADJUSTER"),
	};

	private static readonly (string Kind, string Word, int Scale)[] AdjusterKinds =
	{
		("attack", "UA_ATTACK", 100), ("defence", "UA_DEFENCE", 100), ("movement", "UA_MOVEMENT", 100), ("attrition", "UA_ATTRITION", 1),
	};

	/// <summary>How a figure of the unit's is made from its brigades'.</summary>
	private enum EHow
	{
		Sum,
		Best,
		Least,
		Mean,
	}

	private static readonly (string Key, string Statistic, EHow How)[] ShipRows =
	{
		("SURFACE_DETECTION", "surface_detection", EHow.Best), ("AIR_DETECTION", "air_detection", EHow.Best),
		("SUB_DETECTION", "sub_detection", EHow.Best), ("VISIBILITY", "visibility", EHow.Best),
		("SEA_DEFENCE", "sea_defence", EHow.Sum), ("AIR_DEFENCE", "air_defence", EHow.Sum), ("AIR_ATTACK", "air_attack", EHow.Sum),
		("CONVOY_ATTACK", "convoy_attack", EHow.Sum), ("SEA_ATTACK", "sea_attack", EHow.Sum), ("SUB_ATTACK", "sub_attack", EHow.Sum),
		("SHORE_BOMBARDMENT", "shore_bombardment", EHow.Sum), ("HULL", "hull", EHow.Sum), ("POSITIONING", "positioning", EHow.Mean),
		("TRANSPORT_CAPABILITY", "transport_capability", EHow.Sum), ("AMPH_INVASION_SPEED_BONUS", "amphibious_invasion_speed", EHow.Best),
		("AMPH_INVASION_DEFENCE_BONUS", "amphibious_invasion_defence", EHow.Best), ("CARRIER_CAPACITY", "carrier_size", EHow.Sum),
	};

	private static readonly (string Key, string Statistic, EHow How)[] AirRows =
	{
		("SURFACE_DEFENCE", "surface_defence", EHow.Sum), ("AIR_DEFENCE", "air_defence", EHow.Sum),
		("STRATEGIC_ATTACK", "strategic_attack", EHow.Sum), ("SOFT_ATTACK", "soft_attack", EHow.Sum),
		("HARD_ATTACK", "hard_attack", EHow.Sum), ("TRANSPORT_CAPABILITY", "transport_capability", EHow.Sum),
		("AIR_ATTACK", "air_attack", EHow.Sum), ("SEA_ATTACK", "sea_attack", EHow.Sum),
	};

	private static readonly (string Key, string Statistic)[] LandSums =
	{
		("AIR_DEFENCE", "air_defence"), ("SUPPRESSION", "suppression"), ("SOFT_ATTACK", "soft_attack"), ("HARD_ATTACK", "hard_attack"),
		("AIR_ATTACK", "air_attack"),
	};

	/// <summary>
	/// The brigade whose picture and stars the card shows: the unit's first, which is the one of the highest priority -
	/// a division's headquarters brigade where it has one (<c>docs/mechanics/units.md</c>). None for a unit with none.
	/// </summary>
	public static CSubUnit? Shown(CUnit unit) => unit.SubUnits.Count > 0 ? unit.SubUnits[0] : null;

	/// <summary>Whether a unit has a card: one with brigades, ships or wings of its own.</summary>
	public static bool HasCard(CUnit unit) => unit.SubUnits.Count > 0;

	/// <summary>The card's list, as it stands when the card opens: label and value.</summary>
	public static List<(string Label, string Value)> Rows(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit)
	{
		var rows = new List<(string Label, string Value)> { (localise("UNIT_STATS"), "") };
		if (unit.SubUnits.Count == 0)
		{
			return rows;
		}
		EUnitBranch branch = unit.SubUnits[0].Type.Branch;
		bool isLand = branch == EUnitBranch.Land;
		int count = unit.SubUnits.Count;
		void Row(string key, string value) => rows.Add((localise(key), value));
		string Fig(CFixedPoint value, int places) => CNumberText.Format(value, places);
		CFixedPoint Made(string statistic, EHow how)
		{
			long sum = 0;
			long best = long.MinValue;
			long least = long.MaxValue;
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				long raw = subUnit.Stats[statistic].Raw;
				sum += raw;
				best = Math.Max(best, raw);
				least = Math.Min(least, raw);
			}
			return new CFixedPoint(how switch { EHow.Sum => sum, EHow.Best => best, EHow.Least => least, _ => sum / count });
		}

		CFixedPoint strength = CUnitTotals.Strength(unit);
		Row("BV_STRENGTH", isLand ? CBuilderText.Men(strength) : Fig(new CFixedPoint(strength.Raw / count), 0) + "%");
		Row("BV_EXPERIENCE", Fig(CUnitTotals.AverageExperienceWithPride(state, unit), 1));
		if (isLand)
		{
			Row("MAX_STRENGTH", CBuilderText.Men(CUnitTotals.FullStrength(unit)));
		}
		Row("DEFAULT_ORG", Fig(CUnitTotals.Mean(unit, CSubUnitType.DefaultOrganisation), 1));
		Row("DEFAULT_MORALE", Fig(new CFixedPoint(CUnitTotals.Mean(unit, "default_morale").Raw * 100), 1) + "%");
		// A headquarters reaches by its first brigade's radio (leaders.md, section 8).
		Row("RADIO_STRENGTH", Fig(unit.SubUnits[0].Stats["radio_strength"], 2));
		Row("MAXIMUM_SPEED", Fig(CMovementSpeed.SlowestBrigade(unit), 2) + localise("KPH"));
		Row("SUPPLY_CONSUMPTION", Fig(CUnitSupply.SupplyConsumption(state, owner, unit), 2));
		Row("FUEL_CONSUMPTION", Fig(CUnitSupply.FuelConsumption(state, owner, unit), 2));

		switch (branch)
		{
			case EUnitBranch.Land:
				Row("TRANSPORT_WEIGHT", Fig(Made(CSubUnitType.TransportWeight, EHow.Sum), 2));
				Row("DEFENSIVENESS", Fig(CUnitTotals.Sum(unit, CUnitTotals.Defensiveness), 2));
				// As a battle counts it, which is twice the brigades' (landcombat.md).
				Row("TOUGHNESS", Fig(CUnitTotals.Sum(unit, CSubUnitStats.Toughness), 2));
				Row("SOFTNESS", Fig(new CFixedPoint(CUnitTotals.AverageSoftness(unit).Raw * 100), 0) + "%");
				foreach ((string key, string statistic) in LandSums)
				{
					Row(key, Fig(CUnitTotals.Sum(unit, statistic), 2));
				}
				Row("COMBAT_WIDTH", Fig(CUnitTotals.Width(owner, unit), 2));
				Row("AP_ATTACK", Fig(CUnitTotals.Best(unit, CUnitTotals.Piercing), 2));
				Row("ARMOR_VALUE", Fig(CUnitTotals.Best(unit, CUnitTotals.Armour), 2));
				break;
			case EUnitBranch.Naval:
				Row("RANGE", Fig(Made("range", EHow.Least), 0) + localise("KM"));
				Row("DISTANCE", Fig(Made("distance", EHow.Best), 1) + localise("KM"));
				foreach ((string key, string statistic, EHow how) in ShipRows)
				{
					Row(key, Fig(Made(statistic, how), 2));
				}
				break;
			case EUnitBranch.Air:
				Row("RANGE", Fig(Made("range", EHow.Least), 0) + localise("KM"));
				foreach ((string key, string statistic, EHow how) in AirRows)
				{
					Row(key, Fig(Made(statistic, how), 2));
				}
				break;
		}

		// What the unit cost, which no brigade's card says.
		rows.Add(("", ""));
		Row(Cost, "");
		Row(Officers, Fig(Made(CSubUnitType.Officers, EHow.Sum), 0));
		Row(Manpower, Fig(Made(CSubUnitStats.BuildCostManpower, EHow.Sum), 2));
		Row(BuildCost, Fig(Made(CSubUnitStats.BuildCostIc, EHow.Sum), 2));
		Row(BuildTime, Fig(Made(CSubUnitStats.BuildTime, EHow.Best), 0));

		rows.Add(("", ""));
		foreach ((string adjuster, string label) in Circumstances)
		{
			AddAdjuster(localise, rows, localise(label), unit, adjuster);
		}
		foreach (OpenHOI3.Sim.Provinces.CTerrain terrain in state.Terrains)
		{
			AddAdjuster(localise, rows, localise(terrain.Name), unit, terrain.Name);
		}
		return rows;
	}

	/// <summary>
	/// Up to four rows of a circumstance, each the unit's as a battle and a march take it: its brigades' over how many
	/// it has. Attrition is the figure itself where the other three are a hundred times it.
	/// </summary>
	private static void AddAdjuster(Func<string, string> localise, List<(string Label, string Value)> rows, string name, CUnit unit, string adjuster)
	{
		foreach ((string kind, string word, int scale) in AdjusterKinds)
		{
			long raw = CUnitTotals.Adjuster(unit, adjuster, kind);
			if (raw != 0)
			{
				rows.Add((name + " " + localise(word), (raw > 0 ? "+" : "") + CNumberText.Format(new CFixedPoint(raw * scale), 2) + "%"));
			}
		}
	}
}
