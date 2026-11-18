using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Wars;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// The top level's blocks around the provinces and the countries, both ways: the next rebel faction's and theatre's
/// numbers, the game's flags, the victory conditions, the events that fire once and have fired, and the weather's fronts.
/// See <c>docs/mechanics/savegame.md</c>, section 14.
/// </summary>
public static class CSaveTop
{
	/// <summary>The date the victory conditions write for none: the original's null tick, which prints so.</summary>
	public const string NoVictoryDate = "0.1.0.0";

	/// <summary>The type an event's id has (<c>FINDINGS-savegame.md</c>, B.3).</summary>
	public const int EventIdType = 40;

	/// <summary>Reads a top-level key this holds; false for any other.</summary>
	public static bool Read(CGameState state, ClausewitzNode node, CSaveReport report)
	{
		switch (node.Key)
		{
			// The next numbers, which the original's counters hold.
			case "rebel" when !node.IsBlock:
				state.LastRebelId = (int)CSaveValues.Whole(node.Value) - 1;
				return true;
			case "theatre" when !node.IsBlock:
				state.NextTheatreId = (int)CSaveValues.Whole(node.Value);
				return true;
			case "strategic_warfare" when node.IsBlock:
				CSaveWarfare.Read(state, node);
				return true;
			// The three graph histories (statistics.md, section 5): the nation size read whole; the other two the original
			// never fills, and one that holds anything is reported.
			case "nation_size_statistics" when node.IsBlock:
				ReadSeries(state.NationSizeStatistics, node);
				return true;
			case "income_statistics" or "inflation_statistics" when node.IsBlock:
				if (node.Children.Count > 0)
				{
					report.Unread("top", node.Key + ", not empty");
				}
				return true;
			case "flags" when node.IsBlock:
				foreach (ClausewitzNode flag in node.Children)
				{
					if (flag.Key is { } name && flag.Value == "yes")
					{
						state.Flags.Set(name);
					}
				}
				return true;
			case "victory_conditions" when node.IsBlock:
				ReadVictoryConditions(state, node, report);
				return true;
			case "fired_events" when node.IsBlock:
				ReadFiredEvents(state, node, report);
				return true;
			case "weather" when node.IsBlock:
				ReadFronts(state, node);
				return true;
			// The player's automation and the game's difficulty and mode (frontend.md, section 4).
			case "ai" when node.IsBlock:
				ReadAutomation(state, node);
				return true;
			case "automate_sliders":
				state.Automation.Sliders = (int)CSaveValues.Whole(node.Value);
				return true;
			case "automate_tech_sliders":
				state.Automation.Technology = (int)CSaveValues.Whole(node.Value);
				return true;
			case "gameplaysettings" when node.IsBlock:
				List<ClausewitzNode> options = node.Get("setgameplayoptions")?.Children ?? new List<ClausewitzNode>();
				state.GamePlay.Difficulty = options.Count > 0 ? (int)CSaveValues.Whole(options[0].Value) : Sim.Settings.CGamePlaySettings.NormalDifficulty;
				state.GamePlay.GameMode = options.Count > 1 ? (int)CSaveValues.Whole(options[1].Value) : 0;
				return true;
			default:
				return false;
		}
	}

	/// <summary>
	/// <c>faction = { name date ... faction }</c> a faction, then <c>game_finished</c>: each condition named chosen, with
	/// its date, and met where it has one, as the original's loader sets them; a faction's others not chosen. A name no
	/// condition of the faction has is left out, as the original drops it, and reported.
	/// </summary>
	private static void ReadVictoryConditions(CGameState state, ClausewitzNode node, CSaveReport report)
	{
		foreach ((string _, List<CVictoryCondition> conditions) in state.VictoryConditions)
		{
			foreach (CVictoryCondition condition in conditions)
			{
				condition.Chosen = false;
				condition.Met = false;
				condition.Date = null;
			}
		}
		foreach (ClausewitzNode faction in node.All("faction"))
		{
			List<string> tokens = faction.Children.Select(token => token.Value ?? "").ToList();
			if (tokens.Count == 0)
			{
				continue;
			}
			string name = tokens[^1];
			List<CVictoryCondition>? conditions = state.VictoryConditions.FirstOrDefault(entry => entry.Faction == name).Conditions;
			if (conditions is null)
			{
				report.Unread("victory_conditions", $"a faction of no name known, {name}");
				continue;
			}
			for (int i = 0; i + 1 < tokens.Count; i += 2)
			{
				if (conditions.FirstOrDefault(condition => condition.Name == tokens[i]) is not { } condition)
				{
					report.Unread("victory_conditions", $"a condition of no name known, {tokens[i]}");
					continue;
				}
				condition.Chosen = true;
				condition.Date = tokens[i + 1] != NoVictoryDate && CSaveValues.TryHours(tokens[i + 1], out long hours) ? hours : null;
				condition.Met = condition.Date is not null;
			}
		}
		state.GameFinished = node.GetBool("game_finished");
	}

	/// <summary>
	/// <c>ai = { 0 0 0 0 0 7 0 }</c>: the seven flags, each set only where it is 0 or 1, as the original's setter refuses
	/// anything else - and the sixth entry, which the original writes in its flag's place, the Armies level.
	/// </summary>
	private static void ReadAutomation(CGameState state, ClausewitzNode node)
	{
		Sim.Settings.CPlayerAutomation automation = state.Automation;
		for (int i = 0; i < node.Children.Count && i < Sim.Settings.CPlayerAutomation.FlagCount; i++)
		{
			int value = (int)CSaveValues.Whole(node.Children[i].Value);
			if (i == (int)Sim.Settings.EAutomation.Armies)
			{
				automation.ArmiesLevel = value;
			}
			else if (value is 0 or 1)
			{
				automation.Flags[i] = value;
			}
		}
	}

	/// <summary>
	/// The local player's four keys after <c>player</c>: the seven flags with the Armies level in the sixth's place, Automate
	/// Trade, the production sliders' level and the Technology setting.
	/// </summary>
	public static void WriteAutomation(CSaveTextWriter writer, CGameState state, Sim.Countries.CCountryTag player)
	{
		Sim.Settings.CPlayerAutomation automation = state.Automation;
		var flags = new long[Sim.Settings.CPlayerAutomation.FlagCount];
		for (int i = 0; i < flags.Length; i++)
		{
			flags[i] = i == (int)Sim.Settings.EAutomation.Armies ? automation.ArmiesLevel : automation.Flags[i];
		}
		writer.Numbers("ai", flags);
		writer.Bool("automate_trade", state.AutomateTrade.Contains(player));
		writer.Int("automate_sliders", automation.Sliders);
		writer.Int("automate_tech_sliders", automation.Technology);
	}

	/// <summary><c>gameplaysettings = { setgameplayoptions = { difficulty mode } }</c>.</summary>
	public static void WriteGamePlay(CSaveTextWriter writer, CGameState state)
	{
		writer.Begin("gameplaysettings");
		writer.Numbers("setgameplayoptions", new long[] { state.GamePlay.Difficulty, state.GamePlay.GameMode });
		writer.End();
	}

	/// <summary>Each <c>id = { id = N type = 40 }</c>: an event that fires once and has fired, found by its number.</summary>
	private static void ReadFiredEvents(CGameState state, ClausewitzNode node, CSaveReport report)
	{
		state.FiredOnce.Clear();
		foreach (ClausewitzNode id in node.All("id"))
		{
			int number = CSaveValues.Id(id);
			if (state.Events.FirstOrDefault(candidate => candidate.Id == number) is { } fired)
			{
				state.FiredOnce.Add(fired);
			}
			else
			{
				report.Unread("fired_events", $"an event of no number known, {number}");
			}
		}
	}

	/// <summary>
	/// Every front in its order: direction and rotation in thousandths, the hours to its next move, its home and its
	/// provinces. No province is marked as under one: that is not saved, and the next hour marks them again.
	/// </summary>
	private static void ReadFronts(CGameState state, ClausewitzNode node)
	{
		state.Weather.Fronts.Clear();
		foreach (ClausewitzNode entry in node.All("front"))
		{
			var front = new CWeatherFront((int)CSaveValues.Whole(entry, "home"), (int)CSaveValues.Fixed(entry, "direction").Raw,
				(int)CSaveValues.Fixed(entry, "rotation").Raw)
			{
				MoveCountdown = (int)CSaveValues.Whole(entry, "speed"),
			};
			front.Provinces.Clear();
			foreach (ClausewitzNode province in entry.Get("provinces")?.Children ?? new List<ClausewitzNode>())
			{
				front.Provinces.Add((int)CSaveValues.Whole(province.Value));
			}
			state.Weather.Fronts.Add(front);
		}
	}

	/// <summary>The game's flags, every one set, in the order first set.</summary>
	public static void WriteFlags(CSaveTextWriter writer, CGameState state)
	{
		writer.Begin("flags");
		foreach ((string name, bool set) in state.Flags.All)
		{
			if (set)
			{
				writer.Word(name, "yes");
			}
		}
		writer.End();
	}

	/// <summary>Each faction's chosen conditions, a name and a date each, then the faction's name; then <c>game_finished</c>.</summary>
	public static void WriteVictoryConditions(CSaveTextWriter writer, CGameState state)
	{
		writer.Begin("victory_conditions");
		foreach ((string faction, List<CVictoryCondition> conditions) in state.VictoryConditions)
		{
			writer.Words("faction", conditions.Where(condition => condition.Chosen)
				.SelectMany(condition => new[]
				{
					condition.Name, condition.Date is { } hours ? CSaveValues.HoursText(hours) : NoVictoryDate,
				})
				.Append(faction));
		}
		writer.Bool("game_finished", state.GameFinished);
		writer.End();
	}

	/// <summary>
	/// The events that fire once and have fired, each an id pair: by number, where the original walks its table's buckets
	/// (*inferred*: no reference save holds more than two).
	/// </summary>
	public static void WriteFiredEvents(CSaveTextWriter writer, CGameState state)
	{
		writer.Begin("fired_events");
		foreach (CEvent fired in state.FiredOnce.OrderBy(fired => fired.Id))
		{
			writer.IdPair("id", fired.Id, EventIdType);
		}
		writer.End();
	}

	/// <summary>
	/// The three graph histories, after the wars: <c>income_statistics</c> and <c>inflation_statistics</c> empty, as the
	/// original never fills them, and <c>nation_size_statistics</c> a <c>ledger_data</c> block a line - its figures, its
	/// years and its name (statistics.md, section 5).
	/// </summary>
	public static void WriteStatistics(CSaveTextWriter writer, CGameState state)
	{
		writer.Begin("income_statistics");
		writer.End();
		writer.Begin("nation_size_statistics");
		foreach (Sim.Countries.CStatisticsSeries series in state.NationSizeStatistics)
		{
			writer.Begin("ledger_data");
			writer.Numbers("ledger_data_y", series.Values);
			writer.Numbers("ledger_data_x", series.Years.Select(year => (long)year));
			writer.Quoted("name", series.Name);
			writer.End();
		}
		writer.End();
		writer.Begin("inflation_statistics");
		writer.End();
	}

	/// <summary>A history's lines, each <c>ledger_data</c> block's name with its years and figures beside each other.</summary>
	private static void ReadSeries(List<Sim.Countries.CStatisticsSeries> into, ClausewitzNode node)
	{
		into.Clear();
		foreach (ClausewitzNode block in node.All("ledger_data"))
		{
			var series = new Sim.Countries.CStatisticsSeries(block.Get("name")?.Value?.Trim('"') ?? "");
			List<ClausewitzNode> years = block.Get("ledger_data_x")?.Children ?? new List<ClausewitzNode>();
			List<ClausewitzNode> values = block.Get("ledger_data_y")?.Children ?? new List<ClausewitzNode>();
			for (int index = 0; index < years.Count && index < values.Count; index++)
			{
				series.Years.Add((int)CSaveValues.Whole(years[index].Value));
				series.Values.Add(CSaveValues.Whole(values[index].Value));
			}
			into.Add(series);
		}
	}

	/// <summary>The weather's fronts, the oldest first.</summary>
	public static void WriteFronts(CSaveTextWriter writer, CGameState state)
	{
		writer.Begin("weather");
		foreach (CWeatherFront front in state.Weather.Fronts)
		{
			writer.Begin("front");
			writer.Fixed("direction", new CFixedPoint(front.Direction));
			writer.Fixed("rotation", new CFixedPoint(front.Rotation));
			writer.Int("speed", front.MoveCountdown);
			writer.Int("home", front.Home);
			writer.Numbers("provinces", front.Provinces.Select(province => (long)province));
			writer.End();
		}
		writer.End();
	}
}
