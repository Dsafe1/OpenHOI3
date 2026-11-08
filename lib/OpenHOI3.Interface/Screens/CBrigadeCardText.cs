using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the card of one brigade, ship or wing says, what its list of sunk ships says, and the words of the pride of the
/// fleet's confirmation. See <c>docs/mechanics/brigadecard.md</c>.
/// </summary>
public static class CBrigadeCardText
{
	public const string WindowName = "brigade_view";
	public const string RowName = "brigade_detail";
	public const string SunkWindowName = "sunk_ships_view";
	public const string SunkRowName = "sunk_ship";

	private const string ModelsFolder = "gfx/models/";

	private static readonly (string Key, string Statistic)[] ShipRows =
	{
		("SURFACE_DETECTION", "surface_detection"), ("AIR_DETECTION", "air_detection"), ("SUB_DETECTION", "sub_detection"),
		("VISIBILITY", "visibility"), ("SEA_DEFENCE", "sea_defence"), ("AIR_DEFENCE", "air_defence"), ("AIR_ATTACK", "air_attack"),
		("CONVOY_ATTACK", "convoy_attack"), ("SEA_ATTACK", "sea_attack"), ("SUB_ATTACK", "sub_attack"),
		("SHORE_BOMBARDMENT", "shore_bombardment"), ("HULL", "hull"), ("POSITIONING", "positioning"),
		("TRANSPORT_CAPABILITY", "transport_capability"), ("AMPH_INVASION_SPEED_BONUS", "amphibious_invasion_speed"),
		("AMPH_INVASION_DEFENCE_BONUS", "amphibious_invasion_defence"), ("CARRIER_CAPACITY", "carrier_size"),
	};

	private static readonly (string Key, string Statistic)[] AirRows =
	{
		("SURFACE_DEFENCE", "surface_defence"), ("AIR_DEFENCE", "air_defence"), ("STRATEGIC_ATTACK", "strategic_attack"),
		("SOFT_ATTACK", "soft_attack"), ("HARD_ATTACK", "hard_attack"), ("TRANSPORT_CAPABILITY", "transport_capability"),
		("AIR_ATTACK", "air_attack"), ("SEA_ATTACK", "sea_attack"),
	};

	private static readonly (string Key, string Statistic)[] LandRows =
	{
		("AIR_DEFENCE", "air_defence"), ("SUPPRESSION", "suppression"), ("SOFT_ATTACK", "soft_attack"), ("HARD_ATTACK", "hard_attack"),
		("AIR_ATTACK", "air_attack"), ("COMBAT_WIDTH", CSubUnitType.CombatWidth), ("AP_ATTACK", "ap_attack"), ("ARMOR_VALUE", "armor_value"),
	};

	private static readonly (string Adjuster, string Label)[] Circumstances =
	{
		("night", "NIGHT_ADJUSTER"), ("fort", "FORT_ADJUSTER"), ("river", "RIVER_ADJUSTER"), ("amphibious", "AMPH_ADJUSTER"),
	};

	/// <summary>What a brigade has with the pride's bonus, not below nothing.</summary>
	public static CFixedPoint Experience(CGameState state, CSubUnit subUnit)
	{
		long pride = subUnit.IsPride ? state.Defines[CManpower.MilitarySection, CReinforcement.PrideBonus].Raw : 0;
		return new CFixedPoint(Math.Max(0, subUnit.Experience.Raw) + pride);
	}

	/// <summary>Which of the three star icons shows: <c>unit_experience_land</c>, <c>_air</c> or <c>_naval</c>.</summary>
	public static string ExperienceIcon(CSubUnit subUnit)
	{
		return subUnit.Type.Branch switch
		{
			EUnitBranch.Land => "unit_experience_land",
			EUnitBranch.Air => "unit_experience_air",
			_ => "unit_experience_naval",
		};
	}

	/// <summary>The line under the picture, the model in brackets.</summary>
	public static string Label(Func<string, string> localise, Func<string, string?> find, CCountry owner, CSubUnit subUnit)
	{
		return "(§Y" + CBrigadeTooltips.ModelName(localise, find, owner, subUnit) + "§W)\n";
	}

	/// <summary>
	/// The pictures a brigade may have, in the order they are tried: its country's of its model, the game's of its model,
	/// and the type's first. With no model the original tries a path it has put together wrongly, which is never found.
	/// </summary>
	public static IEnumerable<string> Pictures(CCountryTag owner, CSubUnit subUnit)
	{
		return Pictures(owner, subUnit.Type.Key, subUnit.HistoricalModel);
	}

	/// <summary>The same for a numbered model of a type, <see cref="CSubUnit.NoModel"/> for none.</summary>
	public static IEnumerable<string> Pictures(CCountryTag owner, string type, int number)
	{
		if (number != CSubUnit.NoModel)
		{
			string model = number.ToString(CultureInfo.InvariantCulture);
			yield return ModelsFolder + owner.Value + "_" + type + "_" + model + ".tga";
			yield return ModelsFolder + type + "_" + model + ".tga";
		}
		yield return ModelsFolder + type + "_0.tga";
	}

	/// <summary>The card's list, as it stands when the card opens: label and value.</summary>
	public static List<(string Label, string Value)> Rows(Func<string, string> localise, Func<string, string?> find, CGameState state,
		CCountry owner, CSubUnit subUnit)
	{
		var rows = new List<(string Label, string Value)> { (localise("UNIT_STATS"), "") };
		CSubUnitStats stats = subUnit.Stats;
		bool isLand = subUnit.Type.Branch == EUnitBranch.Land;
		string Fig(string statistic, int places) => CNumberText.Format(stats[statistic], places);
		string Times100(string statistic, int places) => CNumberText.Format(new CFixedPoint(stats[statistic].Raw * 100), places) + "%";
		void Row(string key, string value) => rows.Add((localise(key), value));

		Row("BV_STRENGTH", isLand ? CBuilderText.Men(subUnit.Strength) : CNumberText.Format(subUnit.Strength, 0) + "%");
		Row("BV_EXPERIENCE", CNumberText.Format(Experience(state, subUnit), 1));
		if (isLand)
		{
			Row("MAX_STRENGTH", CBuilderText.Men(stats[CSubUnitType.MaxStrength]));
		}
		Row("DEFAULT_ORG", Fig(CSubUnitType.DefaultOrganisation, 1));
		Row("DEFAULT_MORALE", Times100("default_morale", 1));
		Row("RADIO_STRENGTH", Fig("radio_strength", 2));
		Row("MAXIMUM_SPEED", Fig(CMovementSpeed.MaximumSpeed, 2) + localise("KPH"));
		Row("SUPPLY_CONSUMPTION", CNumberText.Format(CUnitSupply.SubUnitSupply(state, owner, subUnit), 2));
		Row("FUEL_CONSUMPTION", CNumberText.Format(CUnitSupply.SubUnitFuel(state, owner, subUnit), 2));

		switch (subUnit.Type.Branch)
		{
			case EUnitBranch.Land:
				Row("TRANSPORT_WEIGHT", Fig(CSubUnitType.TransportWeight, 2));
				Row("DEFENSIVENESS", Fig("defensiveness", 2));
				Row("TOUGHNESS", Fig(CSubUnitStats.Toughness, 2));
				Row("SOFTNESS", Times100("softness", 0));
				foreach ((string key, string statistic) in LandRows)
				{
					Row(key, Fig(statistic, 2));
				}
				break;
			case EUnitBranch.Naval:
				Row("RANGE", Fig("range", 0) + localise("KM"));
				Row("DISTANCE", Fig("distance", 1) + localise("KM"));
				foreach ((string key, string statistic) in ShipRows)
				{
					Row(key, Fig(statistic, 2));
				}
				break;
			case EUnitBranch.Air:
				foreach ((string key, string statistic) in AirRows)
				{
					Row(key, Fig(statistic, 2));
				}
				break;
		}

		rows.Add(("", ""));
		foreach ((string adjuster, string label) in Circumstances)
		{
			AddAdjuster(localise, rows, localise(label), stats, adjuster);
		}
		foreach (OpenHOI3.Sim.Provinces.CTerrain terrain in state.Terrains)
		{
			AddAdjuster(localise, rows, localise(terrain.Name), stats, terrain.Name);
		}

		rows.Add(("", ""));
		Row("UNIT_MODELS", "");
		foreach ((string key, int level) in subUnit.TechnologyLevels)
		{
			if (state.Technologies.TryGetValue(key, out CTechnology? technology))
			{
				rows.Add((localise(technology.Name), TechnologyValue(localise, find, owner, subUnit, technology, level)));
			}
		}
		return rows;
	}

	/// <summary>Up to four rows of a circumstance. Attrition is the figure itself where the other three are a hundred times it.</summary>
	private static void AddAdjuster(Func<string, string> localise, List<(string Label, string Value)> rows, string name, CSubUnitStats stats, string adjuster)
	{
		if (!stats.Adjusters.TryGetValue(adjuster, out CModifier? figures))
		{
			return;
		}
		foreach ((string kind, string word, int scale) in new[] { ("attack", "UA_ATTACK", 100), ("defence", "UA_DEFENCE", 100), ("movement", "UA_MOVEMENT", 100), ("attrition", "UA_ATTRITION", 1) })
		{
			long raw = figures[kind].Raw;
			if (raw != 0)
			{
				rows.Add((name + " " + localise(word), (raw > 0 ? "+" : "") + CNumberText.Format(new CFixedPoint(raw * scale), 2) + "%"));
			}
		}
	}

	private static string TechnologyValue(Func<string, string> localise, Func<string, string?> find, CCountry owner, CSubUnit subUnit,
		CTechnology technology, int level)
	{
		var text = new StringBuilder();
		if (technology.CanUpgrade)
		{
			text.Append("§Y");
		}
		text.Append(technology.IsSingleLevel ? localise(level >= 1 ? "YES" : "NO") : CBrigadeTooltips.LevelName(localise, find, owner.Tag, technology, level));
		long progress = subUnit.TechnologyProgress.GetValueOrDefault(technology.Name).Raw;
		if (progress > 0)
		{
			text.Append(" §W(+").Append(CNumberText.Format(new CFixedPoint(progress * 100), 2)).Append("%)");
		}
		return text.ToString();
	}

	/// <summary>Every ship on the list of those that have left the game that this one sank, in the order they left.</summary>
	public static List<CSunkShip> SunkBy(CGameState state, CSubUnit ship)
	{
		var sunk = new List<CSunkShip>();
		foreach (CSunkShip entry in state.SunkShips)
		{
			if (ship.Id != 0 && entry.Ship.SunkById == ship.Id)
			{
				sunk.Add(entry);
			}
		}
		return sunk;
	}

	/// <summary>Whether <c>sunk</c> may be pressed: this ship has sunk something.</summary>
	public static bool HasSunk(CGameState state, CSubUnit ship)
	{
		foreach (CSunkShip entry in state.SunkShips)
		{
			if (ship.Id != 0 && entry.Ship.SunkById == ship.Id)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The confirmation's title and its text, the effects it lists written in.</summary>
	public static (string Title, string Text) PrideDialog(Func<string, string> localise, CGameState state, CCountry owner, CSubUnit ship)
	{
		var effects = new StringBuilder();
		effects.Append(CTextTemplate.Fill(localise("MAKE_PRIDE_EXP_EFFECT"), ("NAME", ship.Name),
			("EXP", CNumberText.Format(state.Defines[CManpower.MilitarySection, CReinforcement.PrideBonus], 0))));
		effects.Append(CTextTemplate.Fill(localise("MAKE_PRIDE_SUNK_EFFECT"), ("NAME", ship.Name)));
		if (CPride.Find(owner) is { } old)
		{
			effects.Append(CTextTemplate.Fill(localise("MAKE_PRIDE_PREVIOUS_EFFECT"), ("OLD_NAME", old.Name)));
			CFixedPoint dissent = CPride.Dissent(state.Defines, ship, old);
			if (dissent.Raw > 0)
			{
				effects.Append(CTextTemplate.Fill(localise("MAKE_PRIDE_DISSENT_EFFECT"), ("DISSENT", CNumberText.Format(dissent, 2))));
			}
		}
		return (localise("CONFIRM_MAKE_PRIDE_TITLE"), CTextTemplate.Fill(localise("CONFIRM_MAKE_PRIDE_DESC"), ("EFFECTS", effects.ToString())));
	}

	/// <summary>The command a rename on closing posts, or none: the text differs from the name and is not empty.</summary>
	public static CCommand? Rename(CCountryTag owner, CSubUnit subUnit, string typed)
	{
		string name = typed.Trim();
		return name.Length == 0 || name == subUnit.Name ? null : new CRenameSubUnitCommand(owner, subUnit.Id, name);
	}
}
