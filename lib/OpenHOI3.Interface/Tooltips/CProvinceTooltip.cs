using System;
using System.Text;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// The tooltip of the province under the mouse, as the original puts it together: what a
/// selected army would meet there, the province's name and owner, the map mode's part, and
/// how far it is from what is selected. See <c>docs/mechanics/tooltips.md</c>, section 7.
/// </summary>
public static class CProvinceTooltip
{
	/// <summary>The brigade whose army counts as a headquarters, whatever else is in it.</summary>
	public const string HeadquartersBrigade = "hq_brigade";

	private static readonly string Yellow = CColouredText.In(CColouredText.Yellow);
	private static readonly string WhiteLine = CColouredText.In('W') + "\n";

	/// <summary>The terrain mode's four figures, in their order, and whether each is said when it is nothing.</summary>
	private static readonly (string Key, Func<CTerrain, CFixedPoint> Figure, bool Always)[] TerrainFigures =
	{
		("TERRAIN_MOVEMENT_COST", terrain => terrain.MovementCost, true),
		("TERRAIN_ATTACK", terrain => terrain.Attack, false),
		("TERRAIN_DEFENCE", terrain => terrain.Defence, false),
		("TERRAIN_ATTRITION", terrain => terrain.Attrition, false),
	};

	/// <param name="selectedUnit">The unit selected on the map, which comes before a selected province.</param>
	/// <param name="selectedProvince">The province selected, or 0.</param>
	/// <param name="network">The supply network, asked for only in the supply mode.</param>
	public static CTooltip Build(Func<string, string> localise, CGameState state, CProvince province, EMapTooltipMode mode,
		CCountryTag? player, CUnit? selectedUnit, int selectedProvince, Func<CSupplyNetwork> network, bool continentPainting = false)
	{
		var text = new StringBuilder();
		if (selectedUnit is not null && IsLand(selectedUnit) && state.Country(selectedUnit.Owner) is { } owner)
		{
			text.Append(Head(localise, state, owner, selectedUnit, province));
		}

		text.Append(province.Name);
		if (province.Owner is { } holder)
		{
			text.Append(" (").Append(state.Country(holder)?.Name ?? holder.Value).Append(')');
		}

		switch (mode)
		{
			case EMapTooltipMode.Terrain:
			case EMapTooltipMode.SimplifiedTerrain:
				if (!province.IsSea)
				{
					text.Append(Flags(province));
				}
				text.Append('\n').Append(TerrainName(localise, province));
				text.Append('\n').Append(TerrainText(localise, province.Terrain));
				break;
			case EMapTooltipMode.Political:
				if (!province.IsSea)
				{
					text.Append(Flags(province));
					text.Append('\n').Append(TerrainName(localise, province));
				}
				break;
			case EMapTooltipMode.Supply:
				text.Append(CSupplyMapText.Text(localise, state, network(), province, player));
				break;
			case EMapTooltipMode.Air:
				foreach (string line in CAirMapText.Lines(localise, state, province, player))
				{
					text.Append('\n').Append(line);
				}
				break;
			case EMapTooltipMode.Weather:
				// Put in place of all of the above, the name and owner with it (weather.md, section 6).
				text.Clear().Append(CWeatherMapText.Text(localise, province));
				break;
			case EMapTooltipMode.Infrastructure:
				text.Append(CMapModeText.Infrastructure(state, province, player));
				break;
			case EMapTooltipMode.VictoryPoints:
				text.Append(CMapModeText.VictoryPoints(localise, province));
				break;
			case EMapTooltipMode.Resources:
				text.Append(CMapModeText.Resources(localise, state, province, player));
				break;
			case EMapTooltipMode.Region:
				text.Append(CMapModeText.Region(localise, state, province, continentPainting));
				break;
			case EMapTooltipMode.Diplomatic:
				text.Append(CMapModeText.Diplomatic(localise, state, province, player));
				break;
			case EMapTooltipMode.Intel:
				text.Append(CMapModeText.Intel(localise, state, province, player));
				break;
			case EMapTooltipMode.Theatre:
				text.Append(CMapModeText.Theatre(state, province, player));
				break;
			case EMapTooltipMode.Naval:
				text.Append(CMapModeText.Naval(localise, state, province, player));
				break;
		}

		text.Append(Tail(localise, state, province, selectedUnit, selectedProvince));
		return new CTooltip { Text = text.ToString() };
	}

	/// <summary>
	/// What a selected army would meet in the province, as one line between newlines, or
	/// nothing. The first of the tests that holds says it.
	/// </summary>
	public static string Head(Func<string, string> localise, CGameState state, CCountry owner, CUnit unit, CProvince province)
	{
		string? key = HeadKey(state, owner, unit, province);
		return key is null ? "" : "\n" + localise(key) + "\n";
	}

	private static string? HeadKey(CGameState state, CCountry owner, CUnit unit, CProvince province)
	{
		if (!province.IsSea && !CMovementRules.MayStandIn(state, owner, unit, province))
		{
			return CMovementRules.MayEnter(state, owner, province) ? "UTM_NO_INFRA" : "UTM_NO_ACCESS";
		}
		bool enemy = CMovementRules.IsEnemy(state, province.Controller, unit.Owner);
		if (unit.IsFighting && !CCombatRetreat.MayRetreatTo(state, owner, unit, province))
		{
			return "UTM_NOT_LEGAL_RETREAT";
		}
		if (IsLand(unit) && enemy && CCombatDamage.StrengthShare(unit) < CCombatDamage.ShatterThreshold(state, owner, unit))
		{
			return "UTM_MAY_SHATTER";
		}
		// A unit aboard a fleet is not told it lacks supplies (transport.md, section 12).
		if (IsLand(unit) && enemy && unit.Transport is null && unit.SuppliesReceived == CFixedPoint.Zero
			&& CUnitSupply.SupplyConsumption(state, owner, unit) > CFixedPoint.Zero)
		{
			return "UTM_LACK_OF_SUPPLIES";
		}
		if (!CUnitTotals.CanFight(unit) && enemy)
		{
			// A headquarters too weak to fight is told nothing, and nothing more is asked.
			return IsLand(unit) && IsHeadquarters(unit) ? null : "UTM_TOO_WEAK_TO_FIGHT";
		}
		if (CUnitSupply.FuelConsumption(state, owner, unit) > CFixedPoint.Zero && enemy
			&& unit.FuelReceived == CFixedPoint.Zero)
		{
			return "UTM_LACK_OF_FUEL";
		}
		return null;
	}

	/// <summary>
	/// The flags after the name: the owner's alone where it holds the province, else the
	/// controller's and then the owner's. Nothing where nobody owns or holds it.
	/// </summary>
	public static string Flags(CProvince province)
	{
		if (province.Owner == province.Controller)
		{
			return province.Owner is { } owner ? " - ( @" + owner.Value + ")" : "";
		}
		return " - ( @" + (province.Controller?.Value ?? "") + "/@" + (province.Owner?.Value ?? "") + ")";
	}

	/// <summary>The terrain mode's figures: each key, the figure in yellow to two places, and a newline.</summary>
	public static string TerrainText(Func<string, string> localise, CTerrain? terrain)
	{
		if (terrain is null)
		{
			return "";
		}
		var text = new StringBuilder();
		foreach ((string key, Func<CTerrain, CFixedPoint> figure, bool always) in TerrainFigures)
		{
			CFixedPoint value = figure(terrain);
			if (always || value != CFixedPoint.Zero)
			{
				text.Append(localise(key)).Append(Yellow).Append(CNumberText.Format(value, 2)).Append(WhiteLine);
			}
		}
		return text.ToString();
	}

	/// <summary>
	/// How far the province is from what is selected: an air unit's or a fleet's base, with
	/// its range, or another province.
	/// </summary>
	private static string Tail(Func<string, string> localise, CGameState state, CProvince province, CUnit? unit,
		int selectedProvince)
	{
		var text = new StringBuilder();
		if (unit is { Kind: EUnitKind.Air or EUnitKind.Navy, Base: > 0 })
		{
			CFixedPoint distance = CUnitOrders.DistanceFromBase(state, unit, province);
			text.Append('\n').Append(CNumberText.Format(distance, 0)).Append("km (")
				.Append(CNumberText.Format(CUnitOrders.RangeOf(unit), 0)).Append("km)");
		}
		if (unit is null && selectedProvince > 0 && selectedProvince != province.Id
			&& selectedProvince < state.Provinces.Count)
		{
			CProvince from = state.Province(selectedProvince);
			long distance = CMapDistance.Between(province, from, state.MapWidth);
			text.Append('\n').Append(CTextTemplate.Fill(localise("PROV_DISTANCE_FROM"),
				("DIST", distance.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("FROM", from.Name)));
		}
		return text.ToString();
	}

	private static string TerrainName(Func<string, string> localise, CProvince province)
	{
		return province.Terrain is null ? "" : localise(province.Terrain.Name);
	}

	private static bool IsLand(CUnit unit) => unit.Kind is not (EUnitKind.Navy or EUnitKind.Air);

	/// <summary>Whether an army has a headquarters brigade, which is what makes the original's role for it nought.</summary>
	public static bool IsHeadquarters(CUnit unit)
	{
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (subUnit.Type.Key == HeadquartersBrigade)
			{
				return true;
			}
		}
		return false;
	}
}
