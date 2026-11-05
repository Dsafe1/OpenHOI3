using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// Writes a game as the original's <c>.hoi3</c>, block by block in the original's order with its layout. What the game
/// does not hold is not written: the first increment writes the top level's head, every province, each country's
/// figures that the loader's first increment reads, and the factions. See <c>docs/mechanics/savegame.md</c>, section 8.
/// </summary>
public static class CSaveWriter
{
	/// <summary>A province's goods in the order a save writes them, which is not the game's own.</summary>
	private static readonly EGoods[] PoolOrder =
	{
		EGoods.Supplies, EGoods.Money, EGoods.CrudeOil, EGoods.Fuel, EGoods.Metal, EGoods.Energy, EGoods.RareMaterials,
	};

	public static string Write(CGameState state)
	{
		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		writer.Date("date", state.TotalHours);
		if (state.Played.FirstOrDefault() is { } player && state.Played.Count > 0)
		{
			writer.Quoted("player", player.Value);
		}
		writer.Int("convoy", state.LastConvoyId + 1);
		writer.Int("seed", unchecked((int)state.Seed));
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id != 0)
			{
				WriteProvince(writer, state, province);
			}
		}
		foreach (CCountry country in state.Countries)
		{
			if (country.Tag.Value != "---")
			{
				WriteCountry(writer, state, country);
			}
		}
		WriteFactions(writer, state);
		writer.CloseRoot();
		return writer.Text;
	}

	/// <summary>A province, in the order the original writes it, each key where the original writes it.</summary>
	private static void WriteProvince(CSaveTextWriter writer, CGameState state, CProvince province)
	{
		writer.Begin(province.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
		WriteFlags(writer, province.Flags);
		if (province.Owner is { } owner)
		{
			writer.Quoted("owner", owner.Value);
		}
		if (province.Controller is { } controller)
		{
			writer.Quoted("controller", controller.Value);
		}
		foreach (CCountryTag core in province.Cores)
		{
			writer.Quoted("core", core.Value);
		}
		if (province.Manpower.Raw > 0)
		{
			writer.Fixed("manpower", province.Manpower);
		}
		if (province.Leadership.Raw > 0)
		{
			writer.Fixed("leadership", province.Leadership);
		}
		if (province.RevoltRisk.Raw > 0)
		{
			writer.Fixed("revolt_risk", province.RevoltRisk);
		}
		if (province.StrategicResource.Length > 0)
		{
			writer.Quoted("strategic_resource", province.StrategicResource);
		}
		foreach (CBuilding building in state.Buildings)
		{
			CFixedPoint built = province.BuildingMaxLevel(building);
			CFixedPoint working = province.BuildingLevel(building);
			if (built.Raw != 0 || working.Raw != 0)
			{
				writer.Pair(building.Key, working, built);
			}
		}
		writer.Int("points", province.Points);
		if (province.Nationalism.Raw != 0)
		{
			writer.Fixed("nationalism", province.Nationalism);
		}
		WritePoolIfAny(writer, "pool", province.Pool);
		WritePoolIfAny(writer, "throughput", province.SupplyThroughput);
		WritePoolIfAny(writer, "last_throughput", province.LastSupplyThroughput);
		WritePoolIfAny(writer, "drawn", province.SupplyDrawn);
		WritePoolIfAny(writer, "last_drawn", province.LastSupplyDrawn);
		WritePoolIfAny(writer, "current_producing", province.CurrentProducing);
		WritePoolIfAny(writer, "max_producing", province.MaxProducing);
		WriteWeather(writer, state, province.Weather);
		writer.End();
	}

	/// <summary>A pool of a province: left out where every good is nought, and within it each good that is not.</summary>
	private static void WritePoolIfAny(CSaveTextWriter writer, string key, CGoodsPool pool)
	{
		if (PoolOrder.All(goods => pool[goods].Raw == 0))
		{
			return;
		}
		WritePool(writer, key, pool);
	}

	private static void WritePool(CSaveTextWriter writer, string key, CGoodsPool pool)
	{
		writer.Begin(key);
		foreach (EGoods goods in PoolOrder)
		{
			if (pool[goods].Raw != 0)
			{
				writer.Fixed(CGoodsPool.KeyOf(goods), pool[goods]);
			}
		}
		writer.End();
	}

	/// <summary>A province's weather: each figure that is not nought, the pressure where it is not the define's.</summary>
	private static void WriteWeather(CSaveTextWriter writer, CGameState state, CWeather weather)
	{
		writer.Begin("weather");
		void Figure(string key, CFixedPoint value)
		{
			if (value.Raw != 0)
			{
				writer.Fixed(key, value);
			}
		}
		Figure("humidity", weather.Humidity);
		if (weather.Pressure != state.Defines[CProvinceWeather.Section, CProvinceWeather.PressureDefault])
		{
			writer.Fixed("pressure", weather.Pressure);
		}
		Figure("windspeed", weather.WindSpeed);
		Figure("temperature", weather.Temperature);
		Figure("precipitation", weather.Precipitation);
		Figure("muddyness", weather.Muddyness);
		Figure("frozen", weather.Frozen);
		Figure("cloud_coverage", weather.CloudCoverage);
		writer.End();
	}

	/// <summary>A flags block: every flag that is set, in the order it was first set.</summary>
	private static void WriteFlags(CSaveTextWriter writer, Sim.Script.CFlags flags)
	{
		writer.Begin("flags");
		foreach ((string name, bool set) in flags.All)
		{
			if (set)
			{
				writer.Word(name, "yes");
			}
		}
		writer.End();
	}

	/// <summary>A country's figures that the loader's first increment reads, in the original's order.</summary>
	private static void WriteCountry(CSaveTextWriter writer, CGameState state, CCountry country)
	{
		writer.Begin(country.Tag.Value);
		if (state.Played.Contains(country.Tag))
		{
			writer.Word("human", "yes");
		}
		WriteFlags(writer, country.Flags);
		writer.Begin("variables");
		foreach ((string name, CFixedPoint value) in country.Variables)
		{
			writer.Fixed(name, value);
		}
		writer.End();
		writer.Int("capital", country.Capital);
		writer.Int("acting_capital", country.ActingCapital);

		writer.Begin("technology");
		foreach (string technology in state.Technologies.Keys)
		{
			int level = country.TechnologyLevels.GetValueOrDefault(technology);
			CFixed15 progress = country.ResearchProgress.GetValueOrDefault(technology);
			// The third figure is the leadership put into it, which the game does not hold.
			writer.Key(technology);
			writer.Line("{" + level.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Five(progress) + " 0.000}");
		}
		writer.End();
		foreach (Sim.Research.CTechnologyCategory category in state.TechnologyCategories)
		{
			writer.Fixed(category.Name, country.Abilities.GetValueOrDefault(category.Name));
		}
		writer.Fixed("diplo_influence", country.DiplomaticInfluence);
		WriteShares(writer, "distribution", country.ProductionShares);
		WriteShares(writer, "leadership", country.LeadershipShares);
		writer.Fixed("neutrality", country.Neutrality);
		writer.Fixed("officers", country.Officers);
		foreach (CGovernmentPosition position in state.GovernmentPositions)
		{
			if (country.Government.TryGetValue(position.Name, out CMinister? minister))
			{
				writer.Int(position.Name, minister.Id);
			}
		}
		foreach (CLawGroup group in state.LawGroups)
		{
			if (country.Laws.TryGetValue(group.Name, out CLaw? law))
			{
				writer.Word(group.Name, law.Name);
			}
		}
		writer.Quoted("ideology", country.RulingIdeology.Length > 0 ? country.RulingIdeology : "noIdeology");
		writer.Fixed("manpower", country.Manpower);
		writer.Bool("auto_maintain_convoy", country.AutoMaintainConvoys);
		writer.Bool("auto_supply_convoy", country.AutoSupplyConvoys);
		writer.Bool("auto_resource_convoy", country.AutoResourceConvoys);
		writer.Bool("auto_assign_leaders", country.AutoAssignLeaders);
		// What it holds of every country that owns land, itself among them.
		foreach (CCountry other in state.Countries)
		{
			if (!state.Provinces.Any(province => province.Owner == other.Tag))
			{
				continue;
			}
			CDiplomacyStatus status = CGameState.Status(country, other);
			writer.Begin(other.Tag.Value);
			writer.Int("value", status.Relation.Raw / CFixedPoint.Scale);
			writer.Fixed("threat", status.Threat);
			if (status.OccupationPolicy != 0 && status.OccupationPolicy < state.OccupationPolicies.Count)
			{
				writer.Quoted("occupation_policy", state.OccupationPolicies[status.OccupationPolicy].Name);
			}
			writer.End();
		}
		if (country.Mobilised)
		{
			writer.Bool("mobilize", true);
		}
		writer.Fixed("dissent", country.Dissent);
		writer.Fixed("national_unity", country.NationalUnity);
		writer.Fixed("effective_neutrality", country.EffectiveNeutrality);
		writer.Quoted("highest_threat", country.MostThreatening?.Value ?? "---");
		writer.End();
	}

	/// <summary>A list of shares with five places, indented and closing on its own line's end.</summary>
	private static void WriteShares(CSaveTextWriter writer, string key, CFixed15[] shares)
	{
		writer.IndentedList(key, shares.Select(Five));
	}

	/// <summary>A fifteen-binary-place figure with five decimal places, as the original writes a share and a progress.</summary>
	private static string Five(CFixed15 value)
	{
		decimal shown = (decimal)value.Raw / CFixed15.Scale;
		return shown.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// <c>faction={</c> written with no line's end, then each faction with members as a block at column 0, then
	/// <c>}</c>: the original's raw tokens, which is why a save shows <c>faction={axis=</c>.
	/// </summary>
	private static void WriteFactions(CSaveTextWriter writer, CGameState state)
	{
		writer.Raw("faction={");
		foreach (string faction in state.Countries.Select(country => country.Faction).Where(name => name.Length > 0).Distinct())
		{
			writer.Begin(faction);
			foreach (CCountry member in state.Countries.Where(country => country.Faction == faction))
			{
				writer.Quoted("country", member.Tag.Value);
			}
			writer.End();
		}
		writer.Raw("}\r\n");
	}
}
