using System;
using System.Text;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Script;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Espionage;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the province view's elements say, by name, of the province the view shows. See
/// <c>docs/mechanics/provinceview.md</c>, sections 4 to 7.
/// </summary>
public static class CProvinceViewTooltips
{
	private const string MilitarySection = "military";

	/// <summary>
	/// An element of the view and of its <c>underground</c> window, by name; nothing for one that says nothing. The flags,
	/// the rows and the covert operations have their own.
	/// </summary>
	public static CTooltip Element(Func<string, string> localise, CGameState state, CCountry player, CProvince province, string name)
	{
		foreach ((EGoods goods, string text, string icon, string key) in CProvinceViewText.Resources)
		{
			if (name == icon)
			{
				return new CTooltip { Text = localise(key) };
			}
			if (name == text)
			{
				return new CTooltip { Text = Resource(localise, state, province, goods) };
			}
		}
		switch (name)
		{
			case "terrain":
				return province.Terrain is { } terrain
					? new CTooltip { Text = localise(terrain.Name), Delayed = CProvinceTooltip.TerrainText(localise, terrain).TrimEnd('\n') }
					: CTooltip.Empty;
			case "strategic_resource":
				return province.StrategicResource.Length == 0 ? CTooltip.Empty : new CTooltip
				{
					Text = localise(province.StrategicResource),
					Delayed = state.FindStrategicResource(province.StrategicResource) is { } modifier
						? CModifierText.AllValues(localise, modifier)
						: "",
				};
			case "weather_icon":
				return new CTooltip { Text = localise(CProvinceViewText.WeatherKey(CProvinceViewText.WeatherFrame(state, province.Weather))) };
			case "ground_status_icon":
				return new CTooltip { Text = localise(CProvinceViewText.GroundKey(province.Weather)) };
			case "wind_icon":
			case "wind_status":
				return new CTooltip
				{
					Text = CTextTemplate.Fill(localise("WEATHER_WIND"), ("SPEED", CNumberText.Format(province.Weather.WindSpeed, 1))),
				};
			case "temperature_txt":
				return new CTooltip { Text = localise("WEATHER_TEMPERATURE") };
			case "espionage":
			case "text_espionage":
				return new CTooltip { Text = IntelText(localise, CProvinceIntel.Level(state, province, player.Tag)) };
			case "icon_manpower":
			case "text_manpower":
				return new CTooltip { Text = localise("PV_MANPOWER") };
			case "icon_leadership":
			case "text_leadership":
				return new CTooltip { Text = localise("PV_LEADERSHIP") };
			case "partisans_icon":
				return new CTooltip { Text = localise("PV_RR") };
			case "text_partisans":
				return new CTooltip { Text = CRevoltRiskTooltip.Text(localise, state, province, player.Tag) };
			case "supression_icon":
				return new CTooltip { Text = localise("SUPRESSION") };
			case "underground_status":
				return new CTooltip
				{
					Text = CTextTemplate.Fill(localise("UNDERGROUND_STATUS"),
						("CURRENT", CProvinceUndergroundText.Figure(CUndergrounds.Strength(state, province))),
						("MAX", CProvinceUndergroundText.Figure(Most(state, province)))),
				};
			case "spawn_partisan":
				return Spawn(localise, state, player, province, "SPAWN_PARTISAN", "MOBILIZE_PARTISANS", "SPAWN_PARTISAN_LIMIT");
			case "spawn_underground":
				return Spawn(localise, state, player, province, "SPREAD_UNDERGROUND", "MOBILIZE_UNDERGROUND", "SPAWN_UNDERGROUND_LIMIT");
			case "underground_mission":
				return Mission(localise, state, player, province);
			default:
				return CTooltip.Empty;
		}
	}

	/// <summary>
	/// <c>espionage</c>'s text by what is known: 0 <c>INTEL_NONE</c>, 1 <c>INTEL_STRUCTURES</c>, 2 <c>INTEL_UNITS</c>, 3 to 8
	/// <c>INTEL_2</c> to <c>INTEL_7</c> - one out - and 9 <c>MAX_INTEL_LEVEL</c>.
	/// </summary>
	public static string IntelText(Func<string, string> localise, int level) => level switch
	{
		0 => localise("INTEL_NONE"),
		1 => localise("INTEL_STRUCTURES"),
		2 => localise("INTEL_UNITS"),
		>= 3 and <= 8 => localise("INTEL_" + (level - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)),
		9 => localise("MAX_INTEL_LEVEL"),
		_ => "",
	};

	/// <summary>
	/// A resource's figure's tooltip, at any intelligence: the base, the holder's technology for metal, energy and rare
	/// materials, the province's sources of the good's own and the general local modifier, a blank line, and the holder's
	/// of the global ones.
	/// </summary>
	public static string Resource(Func<string, string> localise, CGameState state, CProvince province, EGoods goods)
	{
		var text = new StringBuilder();
		text.Append(localise("BASE")).Append(" §Y").Append(CNumberText.Format(province.CurrentProducing[goods], 2)).Append("§W\n");
		CCountry? holder = state.Country(province.Controller);
		string technology = CResources.TechnologyKey(goods);
		if (holder is not null && technology.Length > 0 && holder.Technology[technology].Raw != 0)
		{
			text.Append(localise("FROM_TECHNOLOGY")).Append(": §G+")
				.Append(CNumberText.Format(new CFixedPoint(holder.Technology[technology].Raw * 100), 2)).Append("%§W\n");
		}
		string good = GoodName(goods);
		Lines(text, CModifierText.Sources(localise, province.ModifierSources, "local_" + good));
		Lines(text, CModifierText.Sources(localise, province.ModifierSources, "local_resources"));
		text.Append('\n');
		if (holder is not null)
		{
			Lines(text, CModifierText.Sources(localise, holder.ModifierSources, "global_" + good));
			Lines(text, CModifierText.Sources(localise, holder.ModifierSources, "global_resources"));
		}
		return text.ToString();
	}

	/// <summary>An <c>adj</c> row's tooltip, by the way's kind; an impassable way says nothing.</summary>
	public static CTooltip Way(Func<string, string> localise, CGameState state, CProvinceEdge edge)
	{
		string key = edge.Kind switch
		{
			EProvinceEdgeKind.Crossing => "PV_STRAIT",
			EProvinceEdgeKind.River => "PV_RIVER",
			EProvinceEdgeKind.Impassable => "",
			_ => state.Province(edge.To).IsSea ? "PV_OCEAN" : "PV_PASSABLE",
		};
		return key.Length == 0 ? CTooltip.Empty : new CTooltip { Text = localise(key), Delayed = localise(key + "_DESC") };
	}

	/// <summary>A flag's tooltip: the owner's, the occupier's, or that of any other flag - "has a claim".</summary>
	public static CTooltip Flag(Func<string, string> localise, CCountry country, string element)
	{
		string key = element switch
		{
			"owner_flag" => "PV_OWNER",
			"occupier_flag" => "PV_OCC",
			_ => "PV_HAS_CLAIM",
		};
		return new CTooltip { Text = CTextTemplate.Fill(localise(key), ("NAME", country.Name)) };
	}

	/// <summary>
	/// A building row's <c>building_icon</c>: below intelligence 6 the building's name alone; otherwise its label and,
	/// under it, the first reason that applies - not yet available, not held, full, or the coastal line where the province
	/// has a port.
	/// </summary>
	public static CTooltip Building(Func<string, string> localise, CGameState state, CCountry player, CProvince province,
		CBuilding building, string label)
	{
		if (CProvinceIntel.Level(state, province, player.Tag) < CProvinceBuildingRows.LevelsIntel)
		{
			return new CTooltip { Text = localise(building.Key) };
		}
		string delayed = "";
		if (!CBuildingLines.IsAvailable(state, player, building))
		{
			delayed = localise("PV_NB_TECH");
			if (UnlockedBy(state, building) is { } technology)
			{
				delayed += "\n (§R*) " + localise(technology);
			}
		}
		else if (province.Controller != player.Tag)
		{
			delayed = localise("PV_NB_HOSTILE_TERR");
		}
		else if (province.BuildingMaxLevel(building).Raw / CFixedPoint.Scale + CBuildingLines.Queued(player, building, province.Id)
			>= building.MaxLevel)
		{
			delayed = localise("PV_NB_MAX_SIZE");
		}
		else if (CBuildingLines.HasPort(province) && building.IsPort)
		{
			delayed = localise("PV_BUILD_ONLY_IN_PORT");
		}
		return new CTooltip { Text = label, Delayed = delayed };
	}

	/// <summary>
	/// A pip's tooltip: not yet built, built up to its level, or damaged with its working level; and where the holder
	/// has a line of it in the province, that more is being built. Below intelligence 6, the building's name.
	/// </summary>
	public static CTooltip Pip(Func<string, string> localise, CGameState state, CCountry player, CProvince province, CBuilding building)
	{
		var text = new StringBuilder();
		if (CProvinceIntel.Level(state, province, player.Tag) < CProvinceBuildingRows.LevelsIntel)
		{
			return new CTooltip { Text = localise(building.Key) };
		}
		CFixedPoint most = province.BuildingMaxLevel(building);
		CFixedPoint working = province.BuildingLevel(building);
		string max = Rounded(most, 0);
		if (most == CFixedPoint.Zero)
		{
			text.Append(localise("PV_BUILD_NO_YET"));
		}
		else if (working == most)
		{
			text.Append(CTextTemplate.Fill(localise("PV_BUILD_W_DESC"), ("MAX", max)));
		}
		else
		{
			text.Append(CTextTemplate.Fill(localise("PV_BUILD_DESC"), ("CURR", Rounded(working, 3)), ("MAX", max)));
		}
		if (state.Country(province.Controller) is { } holder && CBuildingLines.Queued(holder, building, province.Id) > 0)
		{
			text.Append('\n').Append(localise("PV_CURR_CONS_MORE"));
		}
		return new CTooltip { Text = text.ToString() };
	}

	/// <summary>
	/// A covert operation's row: its description, and under it the cost - in red against the points where they fall
	/// short, with no line break after - then, while the button is disabled, what <c>is_selectable</c> asks, then the
	/// effect.
	/// </summary>
	public static CTooltip CovertOperation(CScriptWords words, CCountry player, CProvince province, CCovertOperation operation,
		bool disabled)
	{
		CGameState state = words.State;
		var delayed = new StringBuilder();
		long points = CProvinceCovertOpsText.Points(state, player, province.Controller);
		string cost = operation.Cost.ToString(System.Globalization.CultureInfo.InvariantCulture);
		if (operation.Cost > points)
		{
			delayed.Append(CTextTemplate.Fill(words.Localise("COV_OPS_COST_LACKING"), ("COST", cost),
				("CURRENT", points.ToString(System.Globalization.CultureInfo.InvariantCulture))));
		}
		else
		{
			delayed.Append(words.Localise("COV_OPS_COST")).Append("§Y").Append(cost).Append("§!\n");
		}
		CEventScope scope = CEventScope.Start(null, province.Id, player.Tag);
		if (disabled && operation.IsSelectable.Children.Count > 0)
		{
			delayed.Append(CConditionText.For(words, operation.IsSelectable, scope)).Append('\n');
		}
		if (operation.Effects.Count > 0)
		{
			delayed.Append(words.Localise("COV_OPS_EXECUTE")).Append(CEffectText.For(words, operation.Effects, scope)).Append('\n');
		}
		return new CTooltip { Text = words.Localise(operation.Name + "_desc"), Delayed = delayed.ToString() };
	}

	private static CTooltip Spawn(Func<string, string> localise, CGameState state, CCountry player, CProvince province,
		string key, string ready, string limitName)
	{
		if (province.UndergroundOwner != player.Tag)
		{
			return Owned(localise, state, province);
		}
		CFixedPoint strength = CUndergrounds.Strength(state, province);
		CFixedPoint limit = state.Defines[MilitarySection, limitName];
		string delayed = strength >= limit
			? localise(ready)
			: CTextTemplate.Fill(localise("UNDERGROUND_NOT_STRONG_ENOUGH"), ("LIMIT", CProvinceUndergroundText.Figure(limit)),
				("CURRENT", CProvinceUndergroundText.Figure(strength)));
		return new CTooltip { Text = localise(key), Delayed = delayed };
	}

	private static CTooltip Mission(Func<string, string> localise, CGameState state, CCountry player, CProvince province)
	{
		if (province.UndergroundOwner is { } owner && owner != player.Tag)
		{
			return Owned(localise, state, province);
		}
		string key = province.UndergroundAction switch
		{
			CUndergrounds.RaisePartisans => "UNDERGROUND_ACTION_PARTISAN",
			CUndergrounds.Spread => "UNDERGROUND_ACTION_SPREAD",
			CUndergrounds.BuildUp => "UNDERGROUND_ACTION_NONE",
			_ => "",
		};
		return key.Length == 0 ? CTooltip.Empty : new CTooltip { Text = localise(key) };
	}

	private static CTooltip Owned(Func<string, string> localise, CGameState state, CProvince province)
	{
		string name = state.Country(province.UndergroundOwner) is { } owner ? owner.Name : "";
		return new CTooltip { Text = CTextTemplate.Fill(localise("UNDERGROUND_OWNER_DESC"), ("COUNTRY", name)) };
	}

	private static CFixedPoint Most(CGameState state, CProvince province)
	{
		return CUndergrounds.Building(state) is { } underground ? province.BuildingMaxLevel(underground) : CFixedPoint.Zero;
	}

	/// <summary>The technology whose <c>activate_building</c> names the building, or none.</summary>
	private static string? UnlockedBy(CGameState state, CBuilding building)
	{
		foreach (CTechnology technology in state.Technologies.Values)
		{
			if (technology.ActivatesBuilding.Equals(building.Key, StringComparison.OrdinalIgnoreCase))
			{
				return technology.Name;
			}
		}
		return null;
	}

	/// <summary>The good as its modifiers name it: <c>local_metal</c>, <c>global_crude_oil</c>.</summary>
	private static string GoodName(EGoods goods) => goods switch
	{
		EGoods.CrudeOil => "crude_oil",
		EGoods.Metal => "metal",
		EGoods.Energy => "energy",
		EGoods.RareMaterials => "rare_materials",
		_ => "",
	};

	/// <summary>A figure as <c>%.Nf</c> writes it, rounded.</summary>
	private static string Rounded(CFixedPoint value, int places)
	{
		return Math.Round(value.Raw / (double)CFixedPoint.Scale, places, MidpointRounding.AwayFromZero)
			.ToString("F" + places.ToString(System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);
	}

	private static void Lines(StringBuilder text, string lines)
	{
		if (lines.Length > 0)
		{
			text.Append(lines).Append('\n');
		}
	}
}
