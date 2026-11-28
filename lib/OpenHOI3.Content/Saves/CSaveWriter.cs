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
	/// <summary>The key of the original's built-in null technology, which no file declares.</summary>
	public const string NullTechnology = "notech";

	/// <summary>What every save holds as a country's <c>last_mission_cancel</c>: the null date.</summary>
	public const string NullMissionCancel = "1.1.1.0";
	/// <summary>A province's goods in the order a save writes them, which is not the game's own.</summary>
	private static readonly EGoods[] PoolOrder =
	{
		EGoods.Supplies, EGoods.Money, EGoods.CrudeOil, EGoods.Fuel, EGoods.Metal, EGoods.Energy, EGoods.RareMaterials,
	};

	/// <summary>
	/// The game as a save's text. A war made in play is written under its name written out, which needs the
	/// localisation; with none given, its text's key stands for it.
	/// </summary>
	public static string Write(CGameState state, Func<string, string>? localise = null)
	{
		localise ??= key => key;
		var writer = new CSaveTextWriter();
		writer.OpenRoot();
		writer.Date("date", state.TotalHours);
		if (state.Played.FirstOrDefault() is { } player && state.Played.Count > 0)
		{
			writer.Quoted("player", player.Value);
			// The local player's automation (savegame.md, section 17).
			CSaveTop.WriteAutomation(writer, state, player);
		}
		// The next numbers of the four counters, the game's flags and the victory conditions (section 14).
		writer.Int("rebel", state.LastRebelId + 1);
		writer.Int("convoy", state.LastConvoyId + 1);
		writer.Int("theatre", state.NextTheatreId);
		writer.Int("unit", state.HistoryUnitNext);
		CSaveTop.WriteFlags(writer, state);
		CSaveTop.WriteGamePlay(writer, state);
		CSaveTop.WriteRules(writer, state);
		CSaveCombatReports.Write(writer, state);
		CSaveBattleDamage.Write(writer, state);
		CSaveStatistics.Write(writer, state);
		CSaveTop.WriteVictoryConditions(writer, state);
		writer.Int("seed", unchecked((int)state.Seed));
		writer.Int("count", unchecked((int)state.Random.Draws));
		// The last serial minted in play, after which the original goes on numbering: the game's own count, where the
		// original gathers the highest of what it holds (savegame.md, section 9) - the rebels' numbers, which are on their own
		// counter, passed over.
		int last = state.LastUnitId;
		while (last >= state.HistoryUnitNext && state.RebelSerials.ContainsKey(last))
		{
			last--;
		}
		if (last >= state.HistoryUnitNext)
		{
			writer.IdPair("id", CSaveUnitWriter.Pair(state, last));
		}
		CSaveRebelFactions.Write(writer, state);
		CSaveTop.WriteFiredEvents(writer, state);
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
		// Every ship that has left the game, under its owner's tag, written always (savegame.md, section 10).
		writer.Begin("sunk_ships");
		foreach (Sim.Units.CSunkShip sunk in state.SunkShips)
		{
			CSaveUnitWriter.WriteSubUnit(writer, state, sunk.Owner?.Value ?? "---", sunk.Ship, fighting: false);
		}
		writer.End();
		CSaveTop.WriteFronts(writer, state);
		WriteFactions(writer, state);
		// The pacts, the battles and the wars (savegame.md, section 10).
		CSaveDiplomacy.Write(writer, state);
		CSaveCombats.Write(writer, state);
		foreach (Sim.Wars.CWar war in state.Wars)
		{
			CSaveWars.Write(writer, "active_war", war, localise);
		}
		foreach (Sim.Wars.CWar war in state.FinishedWars)
		{
			CSaveWars.Write(writer, "previous_war", war, localise);
		}
		foreach (Sim.Wars.CUndeclaredWar undeclared in state.UndeclaredWars)
		{
			writer.Begin("undeclared_war");
			foreach (CCountryTag attacker in undeclared.Attackers)
			{
				writer.Quoted("attacker", attacker.Value);
			}
			foreach (CCountryTag defender in undeclared.Defenders)
			{
				writer.Quoted("defender", defender.Value);
			}
			foreach (string region in undeclared.Regions)
			{
				writer.Quoted("region", region);
			}
			writer.End();
		}
		CSaveTop.WriteStatistics(writer, state);
		CSaveWarfare.Write(writer, state);
		if (state.BattlePlansToSave is { } plans)
		{
			foreach (Sim.Plans.CBattlePlan plan in plans())
			{
				writer.Begin("battle_plan");
				writer.Raw(BattlePlans.CBattlePlanFile.Write(plan, visibility: true));
				writer.End();
			}
		}
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
		if (province.UndergroundOwner is { } underground)
		{
			writer.Quoted("underground_owner", underground.Value);
		}
		// Left out where it is to build up strength, whether or not an underground stands here.
		if (province.UndergroundAction != Sim.Revolts.CUndergrounds.BuildUp)
		{
			writer.Int("underground_action", province.UndergroundAction);
		}
		foreach (CCountryTag core in province.Cores)
		{
			writer.Quoted("core", core.Value);
		}
		// Its controller's capital or beside it, which passes supplies without limit: the original's day works it out and
		// its save writes it (supply.md, section 7).
		if (Sim.Supply.CSupplyFigures.IsBesideCapital(state, province))
		{
			writer.Bool("capital", true);
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
		// The changes made in play, a dated block each, after the buildings (savegame.md, section 4).
		if (province.History.Count > 0)
		{
			writer.Begin("history");
			foreach (Sim.Provinces.CProvinceChange change in province.History)
			{
				writer.Begin(CSaveValues.HoursText(change.Hour));
				if (change.Country is { } country)
				{
					writer.Quoted(change.Key, country.Value);
				}
				else
				{
					writer.Fixed(change.Key, change.Amount);
				}
				writer.End();
			}
			writer.End();
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
		if (province.LastConvoyAttackHour != CProvince.NoConvoyAttack)
		{
			writer.Date("last_convoy_attack", province.LastConvoyAttackHour);
		}
		// After the weather and the last convoy attack, where it is above nought.
		if (province.OutOfSupplyDays > 0)
		{
			writer.Int("out_of_supply_days", province.OutOfSupplyDays);
		}
		WriteEventModifiers(writer, province.EventModifiers);
		writer.End();
	}

	/// <summary>Each modifier an event gave, its name and the hour it lasts to, in the order given.</summary>
	private static void WriteEventModifiers(CSaveTextWriter writer, List<Sim.Modifiers.CTimedModifier> given)
	{
		foreach (Sim.Modifiers.CTimedModifier modifier in given)
		{
			writer.Begin("modifier");
			writer.Quoted("modifier", modifier.Modifier.Name);
			writer.Date("date", modifier.UntilHour);
			writer.End();
		}
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

	/// <summary>A pool's block, written whatever it holds, each good that is not nought in a save's order.</summary>
	internal static void WritePool(CSaveTextWriter writer, string key, CGoodsPool pool)
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
		// The original's tree of variables walked in order - by name, the case folded down - and a variable at nought left
		// out, which a condition asking for one it does not have makes (reversing/findings/FINDINGS-negatives.md, section 7;
		// savegame.md, section 8).
		writer.Begin("variables");
		foreach ((string name, CFixedPoint value) in country.Variables
			.Where(variable => variable.Value != CFixedPoint.Zero)
			.OrderBy(variable => variable.Key.ToLowerInvariant(), StringComparer.Ordinal))
		{
			writer.Fixed(name, value);
		}
		writer.End();
		CSaveConversions.Write(writer, state, country);
		CSaveEventCandidates.Write(writer, state, country);
		CSaveManpowerRecord.Write(writer, state, country);
		CSaveQuietDecisions.Write(writer, state, country);
		writer.Int("capital", country.Capital);
		writer.Int("acting_capital", country.ActingCapital);
		// Whether it is in exile, its strategic warfare, its pools of goods and its battles fought (savegame.md, section 15).
		if (country.InExile)
		{
			writer.Bool("government_in_exile", true);
		}
		CSaveStrategicWarfare.Write(writer, country);
		CSaveLedgers.Write(writer, country);
		if (country.LandBattlesFought > 0)
		{
			writer.Int("land_battles_fought", country.LandBattlesFought);
		}
		if (country.AirBattlesFought > 0)
		{
			writer.Int("air_battles_fought", country.AirBattlesFought);
		}
		if (country.NavalBattlesFought > 0)
		{
			writer.Int("naval_battles_fought", country.NavalBattlesFought);
		}

		writer.Begin("technology");
		// The original's built-in null technology stands first in its database.
		writer.Key(NullTechnology);
		writer.Line("{" + country.NullTechnologyLevel.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 0.00000 0.000}");
		// Every declaration, the earlier of a name declared twice at its place under the name and at nought - no look by name
		// reaches it (research.md, section 1).
		foreach (Sim.Research.CTechnology declaration in state.TechnologyDeclarations)
		{
			string technology = declaration.Name;
			int level = declaration.IsShadowed ? 0 : country.TechnologyLevels.GetValueOrDefault(technology);
			CFixed15 progress = declaration.IsShadowed ? default : country.ResearchProgress.GetValueOrDefault(technology);
			// The third figure is the leadership put into it, which the game does not hold.
			writer.Key(technology);
			writer.Line("{" + level.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + CSaveValues.FiveText(progress) + " 0.000}");
		}
		writer.End();
		// Every ability, the null category's first (research.md, section 4).
		writer.Fixed(Sim.Research.CResearch.NullCategory, country.Abilities.GetValueOrDefault(Sim.Research.CResearch.NullCategory));
		foreach (Sim.Research.CTechnologyCategory category in state.TechnologyCategories)
		{
			writer.Fixed(category.Name, country.Abilities.GetValueOrDefault(category.Name));
		}
		// The research queue, the dates and the politics (savegame.md, section 12).
		CSavePolitics.WriteResearch(writer, country);
		CSavePolitics.WriteDates(writer, country);
		writer.Fixed("war_exhaustion", country.WarExhaustion);
		writer.Fixed("diplo_influence", country.DiplomaticInfluence);
		writer.Int("spiescaught", country.SpiesCaught);
		WriteShares(writer, "distribution", country.ProductionShares);
		WriteShares(writer, "leadership", country.LeadershipShares);
		CSavePolitics.WriteParties(writer, country);
		writer.Fixed("neutrality", country.Neutrality);
		writer.Fixed("officers", country.Officers);
		CSavePolitics.WriteGovernment(writer, country);
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
		WriteEventModifiers(writer, country.EventModifiers);
		writer.Fixed("manpower", country.Manpower);
		writer.Fixed("nukes", country.Nukes);
		// The military part (savegame.md, section 9): the queue, the four switches, the leaders in service, the units
		// at the top of its tree and what waits to be placed.
		CSaveLines.WriteQueue(writer, state, country);
		writer.Bool("auto_maintain_convoy", country.AutoMaintainConvoys);
		writer.Bool("auto_supply_convoy", country.AutoSupplyConvoys);
		writer.Bool("auto_resource_convoy", country.AutoResourceConvoys);
		writer.Bool("auto_assign_leaders", country.AutoAssignLeaders);
		CSaveLeaders.Write(writer, country);
		foreach (Sim.Units.CUnit unit in country.Units)
		{
			if (CSaveUnitWriter.IsWrittenInPlace(unit))
			{
				CSaveUnitWriter.Write(writer, state, unit);
			}
		}
		CSaveLines.WriteDeployments(writer, state, country);
		// What it holds of every country that owns land, itself among them.
		foreach (CCountry other in state.Countries)
		{
			if (!state.Provinces.Any(province => province.Owner == other.Tag))
			{
				continue;
			}
			CSaveStatus.Write(writer, state, CGameState.Status(country, other), other.Tag.Value);
		}
		// The offers on its list, each under its kind's key (savegame.md, section 19).
		CSaveOffers.Write(writer, state, country);
		CSavePolitics.WriteMinisters(writer, country);
		CSaveLines.WriteTemplates(writer, country);
		// The active missions are not built, and every save holds the null date here (savegame.md, section 13).
		writer.Quoted("last_mission_cancel", NullMissionCancel);
		// Its AI's strategies and theatres (savegame.md, section 16).
		CSaveAiStrategy.Write(writer, state, country);
		// Its historical friends where it has any, straight before mobilize as the original writes them.
		if (country.HistoricalFriends.Count > 0)
		{
			writer.Words("historical_friends", country.HistoricalFriends.Select(tag => tag.Value));
		}
		if (country.Mobilised)
		{
			writer.Bool("mobilize", true);
		}
		// The production screen's slider locks, for the country the local player plays alone (sliders.md, section 3).
		if (state.Played.Count > 0 && state.Played.First() == country.Tag)
		{
			writer.Numbers("slider_locks", country.SliderLocks.Select(locked => (long)locked));
		}
		// Where it stands on the alignment triangle, an empty line after it (section 15).
		writer.Begin("alignment");
		writer.Begin("position");
		writer.Fixed("x", new CFixedPoint(country.AlignmentX));
		writer.Fixed("y", new CFixedPoint(country.AlignmentY));
		writer.End();
		writer.BlankLine();
		writer.End();
		writer.Fixed("dissent", country.Dissent);
		writer.Fixed("national_unity", country.NationalUnity);
		// Its pool of transports and escorts and its convoys (savegame.md, section 10).
		writer.Int("escorts", country.Escorts);
		writer.Int("convoys", country.Transports);
		foreach (Sim.Convoys.CConvoy convoy in country.Convoys)
		{
			CSaveConvoys.Write(writer, convoy);
		}
		CSaveEspionage.Write(writer, state, country);
		CSavePolitics.WriteCabinet(writer, country);
		// The victory points of the provinces it controls - 107 of 107 in two BlackICE saves - and off the map's, nought in
		// every save, not held (savegame.md, section 15).
		writer.Fixed("prov_vp", CFixedPoint.FromInt(state.Provinces.Where(province => province.Controller == country.Tag).Sum(province => province.Points)));
		writer.Fixed("offmap_vp", CFixedPoint.Zero);
		CSaveLendLease.Write(writer, state, country);
		writer.Fixed("effective_neutrality", country.EffectiveNeutrality);
		writer.Quoted("highest_threat", country.MostThreatening?.Value ?? "---");
		writer.End();
	}

	/// <summary>A list of shares with five places, indented and closing on its own line's end.</summary>
	private static void WriteShares(CSaveTextWriter writer, string key, CFixed15[] shares)
	{
		writer.IndentedList(key, shares.Select(CSaveValues.FiveText));
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
			// Its progress, the victory points its members hold, in thousandths (savegame.md, section 14).
			writer.Fixed("progress", new CFixedPoint(state.FactionProgress.GetValueOrDefault(faction)));
			foreach (CCountry member in state.Countries.Where(country => country.Faction == faction))
			{
				writer.Quoted("country", member.Tag.Value);
			}
			writer.End();
		}
		writer.Raw("}\r\n");
	}
}
