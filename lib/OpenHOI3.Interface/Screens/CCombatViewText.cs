using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the original shows of a land battle: its name, the share of the two sides'
/// strength that is the attacker's - which is the bar, the number on the map's marker
/// and the marker's colour - the width, whose tactic counters whose, the modifiers in
/// play, and each side's labels. See <c>docs/mechanics/combatview.md</c>.
/// </summary>
public static class CCombatViewText
{
	public const string BattleOfKey = "BATTLE_OF";
	public const string BombingOfKey = "BOMBING_OF";
	public const string CommittedKey = "COMMITED_LABEL";
	public const string ReservesKey = "RESERVES_LABEL";
	public const string NoCommanderKey = "NO_COMMANDER";

	/// <summary>What a modifier's icon is called: this and the modifier's key.</summary>
	public const string ModifierSprite = "CMOD_";

	/// <summary>What a terrain's picture is called: this and the terrain's name.</summary>
	public const string TerrainSprite = "GFX_terrainimg_";

	/// <summary>The sprite whose shape a province's own picture is drawn in.</summary>
	public const string ProvincePictureShape = "GFX_terrainimg_plains";

	/// <summary>The sprite whose shape a tactic's picture is drawn in.</summary>
	public const string TacticPictureShape = "GFX_empty_tactics";

	/// <summary>The frames of the arrow between the two tactics.</summary>
	public const int NobodyCountered = 1;
	public const int DefenderCountered = 2;
	public const int AttackerCountered = 3;

	/// <summary>The frames of the marker on the map: how the battle goes for whoever looks.</summary>
	public const int MarkerGood = 0;
	public const int MarkerEven = 1;
	public const int MarkerBad = 2;

	/// <summary>The shares, in thousandths, above which the marker changes colour.</summary>
	public const long HighShare = 660;
	public const long LowShare = 330;

	private const long Scale = CFixedPoint.Scale;

	/// <summary>
	/// The modifiers in the order their icons stand in, each with the key the original
	/// knows it by. The ones nothing here works out yet are left out.
	/// </summary>
	private static readonly (ECombatModifier Kind, string Key)[] ModifierKeys =
	{
		(ECombatModifier.Dissent, "BM_DISSENT"),
		(ECombatModifier.Envelopment, "BM_ENVELOPMENT_PENALTY"),
		(ECombatModifier.Stacking, "BM_DIVISION_PENALTY"),
		(ECombatModifier.Experience, "BM_EXPERIENCE"),
		(ECombatModifier.MissionEfficiency, "BM_MISSION_EFFICIENCY"),
		(ECombatModifier.TerritorialPride, "BM_TERRITORIAL_PRIDE"),
		(ECombatModifier.Radio, "BM_RADIO"),
		(ECombatModifier.DugIn, "BM_DUGIN_MODIFIER"),
		(ECombatModifier.Amphibious, "BM_AMPH_PENALTY"),
		(ECombatModifier.Fort, "BM_FORT_MODIFIER"),
		(ECombatModifier.River, "BM_RIVER_PENALTY"),
		(ECombatModifier.LackOfSupplies, "BM_LACK_OF_SUPPLIES"),
		(ECombatModifier.MultipleCombats, "BM_MULTIPLE"),
		(ECombatModifier.CombinedArms, "BM_COMBINED_ARMS"),
		(ECombatModifier.BaseProximity, "BM_BASE_PROXIMITY"),
		(ECombatModifier.PoorScreens, "BM_POOR_SCREEN_PENALTY"),
		(ECombatModifier.Radar, "BM_RADAR_STATION"),
		(ECombatModifier.Intercept, "BM_INTERCEPT"),
		(ECombatModifier.AirCombat, "BM_AIRCOMBAT"),
		(ECombatModifier.Terrain, "BM_TERRAIN"),
		(ECombatModifier.Weather, "BM_WEATHER"),
		(ECombatModifier.Night, "BM_NIGHT_MODIFIER"),
		(ECombatModifier.Surprise, "BM_SURPRISE_BONUS"),
	};

	// ---- the battle

	/// <summary>
	/// A battle's name: "Battle of", a space, and the name of its province - "Bombing
	/// of" for a bombing.
	/// </summary>
	public static string Name(Func<string, string> localise, CGameState state, CCombat combat)
	{
		return localise(CBombing.IsBombing(combat.Kind) ? BombingOfKey : BattleOfKey) + " "
			+ state.Province(combat.Province).Name;
	}

	/// <summary>
	/// Whether a battle has a marker on the map: one on land, from its fourth hour. A
	/// battle at sea, one in the air and a bombing have none.
	/// </summary>
	public static bool HasMarker(CCombat combat)
	{
		return combat.Kind == ECombatKind.Land && !combat.IsConvoyBattle && combat.Duration >= CCombats.LeastHours;
	}

	/// <summary>
	/// What a side is worth to the eye, sub unit by sub unit of every unit on it, front
	/// or reserve. A brigade counts by its organisation by its strength, by both of what
	/// the hour's circumstances make of its unit, and by a tenth of its two attacks and
	/// its toughness - its defensiveness for a defender - where that is more than one. A
	/// ship and a wing count by their organisation by their strength and by their own
	/// circumstances, and by nothing of their type.
	/// </summary>
	public static long Worth(CCombatSide side)
	{
		long sum = 0;
		foreach (CUnit unit in side.Units)
		{
			long circumstances = unit.CombatAttack.Raw * unit.CombatDefence.Raw / Scale;
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				long body = subUnit.Organisation.Raw * subUnit.Strength.Raw / Scale;
				if (subUnit.Type.Branch != EUnitBranch.Land)
				{
					sum += body * (subUnit.CombatDefence.Raw * subUnit.CombatAttack.Raw / Scale) / Scale;
					continue;
				}
				long arms = (subUnit.Stats[CUnitTotals.SoftAttack].Raw + subUnit.Stats[CUnitTotals.HardAttack].Raw
					+ subUnit.Stats[side.IsAttacker ? CSubUnitStats.Toughness : CUnitTotals.Defensiveness].Raw)
					* Scale / (10 * Scale);
				long factor = arms > Scale ? circumstances * arms / Scale : circumstances;
				sum += body * factor / Scale;
			}
		}
		return sum;
	}

	/// <summary>The attacker's share of the two sides' worth, in thousandths. Neither side counts for less than one.</summary>
	public static long Share(CCombat combat)
	{
		long attacker = Math.Max(Worth(combat.Attacker), 1);
		long defender = Math.Max(Worth(combat.Defender), 1);
		return attacker * Scale / (attacker + defender);
	}

	/// <summary>How full the bar is, in whole hundredths: the attacker's share.</summary>
	public static int Progress(long share)
	{
		return (int)(share * 100 / Scale);
	}

	/// <summary>The number on the map's marker: the attacker's share as a percentage with nothing after the point.</summary>
	public static string MarkerText(long share)
	{
		return CNumberText.Format(new CFixedPoint(share * 100), 0);
	}

	/// <summary>
	/// The marker's frame: good, even or bad for the one looking. A big share for the
	/// attacker is good for him and bad for a defender.
	/// </summary>
	public static int MarkerFrame(long share, bool viewerDefends)
	{
		if (share > HighShare)
		{
			return viewerDefends ? MarkerBad : MarkerGood;
		}
		if (share > LowShare)
		{
			return MarkerEven;
		}
		return viewerDefends ? MarkerGood : MarkerBad;
	}

	/// <summary>Whether a country fights on the defender's side of a battle.</summary>
	public static bool Defends(CCombat combat, CCountryTag country)
	{
		return combat.Defender.Countries.Contains(country);
	}

	/// <summary>Whether a country has a part in a battle at all. The marker is only there for one that has.</summary>
	public static bool TakesPart(CCombat combat, CCountryTag country)
	{
		return combat.Attacker.Countries.Contains(country) || combat.Defender.Countries.Contains(country);
	}

	/// <summary>The battle's width as a whole number.</summary>
	public static string Width(CGameState state, CCombat combat)
	{
		return (CFrontLine.Width(state, combat).Raw / Scale).ToString(System.Globalization.CultureInfo.InvariantCulture);
	}

	/// <summary>The frame of the arrow between the two tactics. Where each counters the other the attacker's is the one shown.</summary>
	public static int IndicatorFrame(CCombat combat)
	{
		if (CCombatTactics.Counters(combat.Attacker.Tactic, combat.Defender.Tactic))
		{
			return DefenderCountered;
		}
		return CCombatTactics.Counters(combat.Defender.Tactic, combat.Attacker.Tactic) ? AttackerCountered : NobodyCountered;
	}

	/// <summary>
	/// The sprites of the modifiers in play on either side this hour, each once, in the
	/// order their icons stand in.
	/// </summary>
	public static List<string> Modifiers(CCombat combat)
	{
		var inPlay = new HashSet<ECombatModifier>();
		foreach (CCombatSide side in new[] { combat.Attacker, combat.Defender })
		{
			foreach (CUnit unit in side.Units)
			{
				foreach (CCombatModifier modifier in unit.CombatModifiers)
				{
					inPlay.Add(modifier.Kind);
				}
				// At sea, in the air and under bombs the hour's circumstances are noted on
				// each ship, wing and regiment.
				if (combat.Kind != ECombatKind.Land)
				{
					foreach (CSubUnit ship in unit.SubUnits)
					{
						foreach (CCombatModifier modifier in ship.CombatModifiers)
						{
							inPlay.Add(modifier.Kind);
						}
					}
				}
			}
		}
		var sprites = new List<string>();
		foreach ((ECombatModifier kind, string key) in ModifierKeys)
		{
			if (inPlay.Contains(kind))
			{
				sprites.Add(ModifierSprite + key);
			}
		}
		return sprites;
	}

	/// <summary>The file of a province's own picture, which few provinces have.</summary>
	public static string ProvincePicture(CCombat combat)
	{
		return "gfx/interface/prov_pictures/provimg_" + combat.Province.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".dds";
	}

	/// <summary>The sprite of the battle's terrain, or an empty string where the province has none.</summary>
	public static string TerrainPicture(CGameState state, CCombat combat)
	{
		CProvince province = state.Province(combat.Province);
		return province.Terrain is { } terrain ? TerrainSprite + terrain.Name : "";
	}

	// ---- a side

	/// <summary>The line over a side's front: how many units stand on it and how much of the width they take.</summary>
	public static string CommittedLabel(Func<string, string> localise, CGameState state, CCombatSide side)
	{
		long width = 0;
		foreach (CUnit unit in side.Front)
		{
			width += CFrontLine.UnitWidth(state, unit).Raw;
		}
		return CTextTemplate.Fill(localise(CommittedKey),
			("COUNT", side.Front.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
			("WIDTH", (width / Scale).ToString(System.Globalization.CultureInfo.InvariantCulture)));
	}

	/// <summary>The line over a side's reserves.</summary>
	public static string ReservesLabel(Func<string, string> localise, CCombatSide side)
	{
		return CTextTemplate.Fill(localise(ReservesKey),
			("COUNT", side.Reserves.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
	}

	/// <summary>
	/// The name under a side's portrait. A battle's leader is not built yet, so it is the
	/// original's words for a side with none - and nothing for a side with no country left.
	/// </summary>
	public static string LeaderName(Func<string, string> localise, CCombatSide side)
	{
		return side.Countries.Count > 0 ? localise(NoCommanderKey) : "";
	}

	/// <summary>
	/// Whose flag a side flies: its first country's, or with none the country that
	/// holds the province fought over.
	/// </summary>
	public static CCountryTag? Flag(CGameState state, CCombatSide side)
	{
		return side.Countries.Count > 0 ? side.Countries[0] : state.Province(side.Combat.Province).Controller;
	}

	/// <summary>A side's tactic by its name in the player's language, or an empty string for none.</summary>
	public static string TacticName(Func<string, string> localise, CCombatSide side)
	{
		return side.Tactic is { } tactic ? localise(tactic.Name) : "";
	}

	/// <summary>The file of a side's tactic's picture, or an empty string for none.</summary>
	public static string TacticPicture(CCombatSide side)
	{
		return side.Tactic is { Picture.Length: > 0 } tactic ? "gfx/pictures/tactics/" + tactic.Picture + ".dds" : "";
	}

	// ---- a unit in a list

	/// <summary>The sprite of a unit's picture in a list of the window, or an empty string.</summary>
	public static string UnitSprite(CUnit unit)
	{
		return CCounterText.Sprite(CCounterText.ListSprite, unit);
	}

	/// <summary>The frame of a unit's width strip, by its brigades' widths added up.</summary>
	public static int UnitWidthFrame(CUnit unit)
	{
		return CUnitPanelText.WidthFrame(CUnitPanelText.Width(unit));
	}

	/// <summary>A unit's strength bar, in whole hundredths.</summary>
	public static int StrengthBar(CUnit unit)
	{
		return CUnitPanelText.StrengthBar(unit);
	}

	/// <summary>A unit's organisation bar, in whole hundredths.</summary>
	public static int OrganisationBar(CGameState state, CUnit unit)
	{
		return state.Country(unit.Owner) is { } owner ? CUnitPanelText.OrganisationBar(owner, unit) : 0;
	}

	/// <summary>Where a unit is: the name of its province.</summary>
	public static string Location(CGameState state, CUnit unit)
	{
		return CUnitPanelText.Location(state, unit);
	}

	// ---- a battle at sea

	public const string ShipsKey = "SHIPS_LABEL";

	/// <summary>What a ship's picture in a list is called: this, <c>l_</c> or <c>r_</c> for its side, and its type.</summary>
	public const string ShipSprite = "GFX_navalcombat_";

	/// <summary>The distance at which a ship's picture stands at the far end of its row, and further out no further.</summary>
	private const float FarthestShown = 40f;

	/// <summary>How far past the near end of its row a ship's picture is put, towards the middle of the window.</summary>
	private const int TowardsTheMiddle = 19;

	/// <summary>The line over a side's ships: how many there are, in yellow, and the word for them.</summary>
	public static string ShipsLabel(Func<string, string> localise, CCombatSide side)
	{
		int ships = 0;
		foreach (CUnit fleet in side.Units)
		{
			ships += fleet.SubUnits.Count;
		}
		return "§Y" + ships.ToString(System.Globalization.CultureInfo.InvariantCulture) + "§W "
			+ localise(ShipsKey);
	}

	/// <summary>A side's positioning as a percentage with one figure after the point.</summary>
	public static string Positioning(CCombatSide side)
	{
		return CNumberText.Format(new CFixedPoint(side.Positioning.Raw * 100), 1) + "%";
	}

	/// <summary>The name of the sprite of a ship's picture in a side's list.</summary>
	public static string ShipPicture(CSubUnit ship, bool isAttacker)
	{
		return ShipSprite + (isAttacker ? "l_" : "r_") + ship.Type.Key;
	}

	/// <summary>A ship's strength bar, in whole hundredths of its full strength.</summary>
	public static int ShipStrengthBar(CGameState state, CSubUnit ship)
	{
		long full = state.Country(ship.Unit?.Owner) is { } owner
			? CUnits.MaxStrength(state, owner, ship, false, false).Raw
			: ship.Stats[CSubUnitType.MaxStrength].Raw;
		long share = full == 0 ? -1 : ship.Strength.Raw * Scale / full;
		return (int)(share * 100000 / Scale / Scale);
	}

	/// <summary>A ship's organisation bar, in whole hundredths of the most its type has.</summary>
	public static int ShipOrganisationBar(CSubUnit ship)
	{
		long most = ship.Stats[CSubUnitType.DefaultOrganisation].Raw;
		long share = most == 0 ? -1 : ship.Organisation.Raw * Scale / most;
		return (int)(share * 100000 / Scale / Scale);
	}

	/// <summary>
	/// Which of its picture's two frames a ship shows, counted from one: an attacker's
	/// the first while it has no target and the second while it has one, a defender's
	/// the other way about - the two sides' pictures being drawn in the other order.
	/// </summary>
	public static int ShipFrame(CCombat combat, CSubUnit ship, bool isAttacker)
	{
		return HasTarget(combat, ship) == isAttacker ? 2 : 1;
	}

	/// <summary>Whether a ship has a target still in the battle: an enemy, or a friend it mistook for one.</summary>
	public static bool HasTarget(CCombat combat, CSubUnit ship)
	{
		if (ship.TargetId == 0)
		{
			return false;
		}
		foreach (CCombatSide side in new[] { combat.Attacker, combat.Defender })
		{
			foreach (CUnit fleet in side.Units)
			{
				foreach (CSubUnit other in fleet.SubUnits)
				{
					if (other.Id == ship.TargetId)
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	/// <summary>
	/// Where a ship's picture stands in its row, from the row's left edge. The row is a
	/// scale of the ship's distance: nothing at the end nearer the middle of the window,
	/// forty and more at the far end - so the attacker's ships, on the left, stand further
	/// left the further out they are, and the defender's further right. Both are put
	/// nineteen further towards the middle than that.
	/// </summary>
	public static int ShipPlace(CSubUnit ship, bool isAttacker, int rowWidth, int pictureWidth)
	{
		float travel = rowWidth - pictureWidth;
		float share = Math.Min(ship.Distance.Raw / (float)Scale / FarthestShown, 1f);
		int offset = (int)(share * travel);
		return isAttacker
			? rowWidth - offset - pictureWidth + TowardsTheMiddle
			: offset - TowardsTheMiddle;
	}
}
