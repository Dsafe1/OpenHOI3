using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A save's <c>combat</c> block: the battles and bombings being fought, each made blank, its two sides read and then
/// put to work as the original's load does it, and <c>history</c>, the battles finished in the past week. See
/// <c>docs/mechanics/savegame.md</c>, section 7.
/// </summary>
public static class CSaveCombats
{
	/// <summary>The keys a battle is kept under, by kind.</summary>
	public static readonly IReadOnlyDictionary<string, ECombatKind> Kinds = new Dictionary<string, ECombatKind>(StringComparer.Ordinal)
	{
		["land_combat"] = ECombatKind.Land,
		["naval_combat"] = ECombatKind.Naval,
		["air_combat"] = ECombatKind.Air,
		["ground_bombing"] = ECombatKind.GroundBombing,
		["land_bombing"] = ECombatKind.LandBombing,
		["naval_bombing"] = ECombatKind.NavalBombing,
	};

	/// <summary>Reads the block into the state, its units found by their ids. The ships of <c>sunk_ships</c> must be read first.</summary>
	public static void Read(CGameState state, ClausewitzNode block, IReadOnlyDictionary<int, CUnit> units, CSaveIds ids,
		CSaveReport report)
	{
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is not { } key)
			{
				continue;
			}
			if (Kinds.TryGetValue(key, out ECombatKind kind) && entry.IsBlock)
			{
				ReadCombat(state, kind, key, entry, units, ids, report);
			}
			else if (key == "history" && entry.IsBlock)
			{
				ReadHistory(state, entry, report);
			}
			else
			{
				report.Unread("combat", key);
			}
		}
	}

	private static void ReadCombat(CGameState state, ECombatKind kind, string keyword, ClausewitzNode block,
		IReadOnlyDictionary<int, CUnit> units, CSaveIds ids, CSaveReport report)
	{
		int province = (int)CSaveValues.Whole(block, "location");
		if (province <= 0 || province >= state.Provinces.Count)
		{
			report.Unread(keyword, $"a battle in no province, {province}, left out");
			return;
		}
		var combat = new CCombat(province, kind)
		{
			Hour = block.Get("day") is { } day ? (int)CSaveValues.Whole(day.Value) : CCombat.NotStarted,
			Duration = (int)CSaveValues.Whole(block, "duration"),
		};
		// A bombing goes at the front of the list and any other battle at the back, as in play - so the bombings of a
		// save come back in the opposite order.
		if (CBombing.IsBombing(kind))
		{
			state.Combats.Insert(0, combat);
		}
		else
		{
			state.Combats.Add(combat);
		}

		foreach (ClausewitzNode entry in block.Children)
		{
			switch (entry.Key)
			{
				case "location":
				case "day":
				case "duration":
					break;
				case "attacker" when entry.IsBlock:
					ReadSide(state, combat, combat.Attacker, keyword, entry, units, ids, report);
					break;
				case "defender" when entry.IsBlock:
					ReadSide(state, combat, combat.Defender, keyword, entry, units, ids, report);
					break;
				case "terrain":
					// The province's own terrain is what a battle here is fought on; a name nothing knows is said.
					string terrain = entry.Value?.Trim('"') ?? "";
					if (!state.Terrains.Any(known => string.Equals(known.Name, terrain, StringComparison.Ordinal)))
					{
						report.Unread(keyword, $"a terrain of no name known, {terrain}");
					}
					break;
				default:
					report.Unread(keyword, entry.Key ?? "");
					break;
			}
		}

		// The block's end: the battle has begun, so its first hour is an ordinary one; a land battle sets up its
		// attacker's front where it is not - asked twice, as the original does - and never its defender's, and its
		// tactics are summed again. A ground bombing's side with nobody on it is the province's holder's.
		combat.HasBegun = true;
		combat.HasEnded = false;
		if (kind == ECombatKind.Land)
		{
			for (int ask = 0; ask < 2; ask++)
			{
				if (!combat.Attacker.IsFrontSetUp)
				{
					CFrontLine.SetUp(state, combat, combat.Attacker);
				}
			}
			CCombatTactics.SumEffects(combat);
		}
		else if (kind == ECombatKind.GroundBombing && state.Province(province).Controller is { } holder)
		{
			combat.Defender.AddCountry(holder);
		}
	}

	private static void ReadSide(CGameState state, CCombat combat, CCombatSide side, string keyword, ClausewitzNode block,
		IReadOnlyDictionary<int, CUnit> units, CSaveIds ids, CSaveReport report)
	{
		string where = keyword + " side";
		// The units first, each taken on as a unit joining in play is: its owner on the side, its brigades' targets
		// forgotten, its men or ships or wings counted, a fleet's ships put at their distances again. Nobody is placed
		// in reserve: the original loads a save before any game is on screen.
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key != "unit")
			{
				continue;
			}
			if (Unit(entry, units, ids) is not { } unit)
			{
				report.Unread(where, "a unit of no id known, left out");
				continue;
			}
			if (CBombing.IsBombing(combat.Kind))
			{
				CBombing.Join(side, unit);
			}
			else if (combat.Kind == ECombatKind.Naval)
			{
				CNavalBattle.Join(state, combat, side, unit);
			}
			else if (combat.Kind == ECombatKind.Air)
			{
				CAirBattle.Join(side, unit);
			}
			else
			{
				side.Add(unit);
			}
		}

		bool listed = false;
		foreach (ClausewitzNode entry in block.Children)
		{
			switch (entry.Key)
			{
				case "unit":
					break;
				case "dice":
					side.Dice = (int)CSaveValues.Whole(entry.Value);
					break;
				case "losses":
					side.Losses = CSaveValues.Fixed(entry.Value);
					break;
				case "sunk_ship":
					if (CSunkShips.Find(state, ids.Of(entry)) is { } ship)
					{
						side.Sunk.Add(ship);
					}
					else
					{
						report.Unread(where, "a sunk ship not among sunk_ships, left out");
					}
					break;
				case "size" when entry.IsBlock:
					ReadSize(state, side, entry, report, where);
					break;
				case SizeByTypeKey when entry.IsBlock:
					ReadSizeByType(state, side, entry, report, where);
					break;
				case "tactic":
					int number = (int)CSaveValues.Whole(entry.Value);
					if (number >= 1 && number <= state.Tactics.Count)
					{
						side.Tactic = state.Tactics[number - 1];
					}
					else
					{
						// The original holds its empty tactic here, which counts as one held; there is none here.
						report.Unread(where, $"a tactic of no number known, {number}");
					}
					break;
				case "front" when combat.Kind == ECombatKind.Land:
					listed = true;
					Append(side.Front, entry, units, ids, report, where);
					break;
				case "reserves" when combat.Kind == ECombatKind.Land:
					listed = true;
					Append(side.Reserves, entry, units, ids, report, where);
					break;
				case "positioning" when combat.Kind == ECombatKind.Naval:
					// Worked out again as each fleet joined, without the roll; the save's figure stands.
					side.Positioning = CSaveValues.Fixed(entry.Value);
					break;
				case "strength" when side.IsAttacker && CBombing.IsBombing(combat.Kind):
					side.StrengthDealt = CSaveValues.Fixed(entry.Value);
					break;
				case "building" when side.IsAttacker && CBombing.IsBombing(combat.Kind) && entry.IsBlock:
					ReadBuildings(state, side, entry);
					break;
				case "pool" when side.IsAttacker && CBombing.IsBombing(combat.Kind) && entry.IsBlock:
					ReadGoods(side.StockpileDestroyed, entry);
					break;
				case "current_producing" when side.IsAttacker && CBombing.IsBombing(combat.Kind) && entry.IsBlock:
					ReadGoods(side.ProductionDestroyed, entry);
					break;
				default:
					// `retreat` among them: a unit falling back leaves its side at once here.
					report.Unread(where, entry.Key ?? "");
					break;
			}
		}
		// Any key of a land side marks its front set up, unless it has units and none of them is on a list - a side
		// saved before its battle's first hour.
		if (combat.Kind == ECombatKind.Land)
		{
			side.IsFrontSetUp = listed || side.Units.Count == 0;
		}
	}

	/// <summary>
	/// A side's <c>size</c>, one figure for each type in the original's database order, the empty type first: each
	/// figure overwrites what the joins counted, and a figure past the last type is dropped.
	/// </summary>
	private static void ReadSize(CGameState state, CCombatSide side, ClausewitzNode block, CSaveReport report, string where)
	{
		var byIndex = new Dictionary<int, string>();
		foreach (CSubUnitType type in state.SubUnitTypes.Values)
		{
			byIndex[type.DatabaseIndex] = type.Key;
		}
		byIndex[0] = EmptyType;
		int index = 0;
		foreach (ClausewitzNode figure in block.Children)
		{
			if (figure.Key is not null)
			{
				continue;
			}
			if (!byIndex.TryGetValue(index, out string? key))
			{
				report.Unread(where, "a size past the last unit type");
				break;
			}
			CFixedPoint value = CSaveValues.Fixed(figure.Value);
			if (value.Raw != 0 || side.Size.ContainsKey(key))
			{
				side.Size[key] = value;
			}
			index++;
		}
	}

	/// <summary>
	/// An enhanced save's size of a side (<see cref="SizeByTypeKey"/>): the types the side has any of, each by its
	/// name. It stands as the original's whole list does: what the joins counted of a type it does not name is
	/// nought, as a nought in the list would make it. A name that is no unit type's is reported and left out.
	/// </summary>
	private static void ReadSizeByType(CGameState state, CCombatSide side, ClausewitzNode block, CSaveReport report, string where)
	{
		foreach (string counted in side.Size.Keys.ToList())
		{
			side.Size[counted] = CFixedPoint.Zero;
		}
		foreach (ClausewitzNode figure in block.Children)
		{
			if (figure.Key is not { } key || figure.IsBlock)
			{
				continue;
			}
			if (!state.SubUnitTypes.ContainsKey(key))
			{
				report.Unread(where, $"a size of no unit type known, {key}");
				continue;
			}
			side.Size[key] = CSaveValues.Fixed(figure.Value);
		}
	}

	/// <summary>
	/// The key an enhanced save writes a side's size under, in the place of the original's <c>size</c>: a block of
	/// <c>type = figure</c> for the types the side has any of. The original's list has a figure for every type of the
	/// database, nearly all nought - 58 in Their Finest Hour, some 1,650 under BlackICE, about ten kilobytes a side.
	/// </summary>
	public const string SizeByTypeKey = "openhoi_size";

	/// <summary>What a side's <c>size</c> calls the original's empty type, the first of its database.</summary>
	public const string EmptyType = "---";

	/// <summary>A bomber's <c>building</c>: the empty building first, then the buildings in their file's order.</summary>
	private static void ReadBuildings(CGameState state, CCombatSide side, ClausewitzNode block)
	{
		int index = 0;
		foreach (ClausewitzNode figure in block.Children)
		{
			if (figure.Key is not null)
			{
				continue;
			}
			if (index > 0 && index - 1 < state.Buildings.Count)
			{
				CFixedPoint value = CSaveValues.Fixed(figure.Value);
				string key = state.Buildings[index - 1].Key;
				if (value.Raw != 0 || side.BuildingsDestroyed.ContainsKey(key))
				{
					side.BuildingsDestroyed[key] = value;
				}
			}
			index++;
		}
	}

	/// <summary>Seven figures in the goods' order: supplies, fuel, money, crude oil, metal, energy, rare materials.</summary>
	private static void ReadGoods(CGoodsPool pool, ClausewitzNode block)
	{
		int index = 0;
		foreach (ClausewitzNode figure in block.Children)
		{
			if (figure.Key is not null)
			{
				continue;
			}
			if (index < CGoodsPool.Count)
			{
				pool[(EGoods)index] = CSaveValues.Fixed(figure.Value);
			}
			index++;
		}
	}

	private static void Append(List<CUnit> list, ClausewitzNode entry, IReadOnlyDictionary<int, CUnit> units, CSaveIds ids,
		CSaveReport report, string where)
	{
		if (Unit(entry, units, ids) is { } unit)
		{
			list.Add(unit);
		}
		else
		{
			report.Unread(where, $"a {entry.Key} unit of no id known, left out");
		}
	}

	private static CUnit? Unit(ClausewitzNode entry, IReadOnlyDictionary<int, CUnit> units, CSaveIds ids)
	{
		return units.TryGetValue(ids.Of(entry), out CUnit? unit) && !unit.IsRemoved ? unit : null;
	}

	/// <summary>
	/// The finished battles: each entry put at the end of the list as it stands, in the file's order. The first hour
	/// after the load prunes them.
	/// </summary>
	private static void ReadHistory(CGameState state, ClausewitzNode block, CSaveReport report)
	{
		foreach (ClausewitzNode inner in block.Children)
		{
			if (inner.Key != "history" || !inner.IsBlock)
			{
				report.Unread("combat history", inner.Key ?? "");
				continue;
			}
			foreach (ClausewitzNode entry in inner.Children)
			{
				if (entry.Key is not null || !entry.IsBlock)
				{
					continue;
				}
				long kind = CSaveValues.Whole(entry, "type");
				if (kind < 1 || kind > 6 || !CSaveValues.TryHours(entry.Get("end_date")?.Value?.Trim('"'), out long hour))
				{
					report.Unread("combat history", "an entry of no kind or end known, left out");
					continue;
				}
				state.CombatHistory.Add(new CFinishedCombat(hour, (ECombatKind)(kind - 1), (int)CSaveValues.Whole(entry, "location"),
					Tag(entry, "attacker"), Tag(entry, "defender"), entry.Get("convoy")?.Value == "yes"));
				foreach (ClausewitzNode key in entry.Children)
				{
					if (key.Key is not ("location" or "attacker" or "defender" or "end_date" or "convoy" or "type"))
					{
						report.Unread("combat history", key.Key ?? "");
					}
				}
			}
		}
	}

	/// <summary>
	/// <c>combat</c>: every battle being fought in the list's order, each under its kind's key, then <c>history</c>, the
	/// finished battles - written as the original's writers do (<c>savegame.md</c>, section 10).
	/// </summary>
	public static void Write(CSaveTextWriter writer, CGameState state)
	{
		writer.Begin("combat");
		foreach (CCombat combat in state.Combats)
		{
			WriteCombat(writer, state, combat);
		}
		// The history's own key holds the entries, and is left out where there are none. Each entry is an empty key
		// with its block, and a text save writes a one-space token after each: so the first stands after a line of
		// indentation, the rest after the token's line, and the closing brace follows the last token.
		writer.Begin("history");
		if (state.CombatHistory.Entries.Count > 0)
		{
			writer.Begin("history");
			for (int i = 0; i < state.CombatHistory.Entries.Count; i++)
			{
				CFinishedCombat finished = state.CombatHistory.Entries[i];
				if (i == 0)
				{
					writer.IndentationLine();
				}
				else
				{
					writer.Raw("\r\n");
				}
				writer.BeginAnonymous();
				writer.Int("location", finished.Province);
				writer.Quoted("attacker", finished.Attacker?.Value ?? None);
				writer.Quoted("defender", finished.Defender?.Value ?? None);
				writer.Date("end_date", finished.Hour);
				if (finished.IsConvoy)
				{
					writer.Bool("convoy", true);
				}
				writer.Int("type", (int)finished.Kind + 1);
				writer.End();
				writer.Raw(" ");
			}
			writer.End();
		}
		writer.End();
		writer.End();
	}

	/// <summary>A battle: its province, its two counters, its sides, and the terrain it is fought on.</summary>
	private static void WriteCombat(CSaveTextWriter writer, CGameState state, CCombat combat)
	{
		writer.Begin(Kinds.First(pair => pair.Value == combat.Kind).Key);
		writer.Int("location", combat.Province);
		writer.Int("day", combat.Hour);
		writer.Int("duration", combat.Duration);
		WriteSide(writer, state, combat, combat.Attacker, "attacker");
		WriteSide(writer, state, combat, combat.Defender, "defender");
		if (state.Province(combat.Province).Terrain is { } terrain)
		{
			writer.Quoted("terrain", terrain.Name);
		}
		writer.End();
	}

	/// <summary>
	/// A side: its die, its units in the order they joined, the ships it has lost, its losses, its size, its tactic where
	/// it holds one; then a land side's front and reserves, a naval side's positioning, or a bomber's tallies.
	/// </summary>
	private static void WriteSide(CSaveTextWriter writer, CGameState state, CCombat combat, CCombatSide side, string key)
	{
		writer.Begin(key);
		writer.Int("dice", side.Dice);
		foreach (CUnit unit in side.Units)
		{
			writer.IdPair("unit", CSaveUnitWriter.Pair(state, unit.Id));
		}
		foreach (CSubUnit ship in side.Sunk)
		{
			writer.IdPair("sunk_ship", CSaveUnitWriter.Pair(state, ship.Id));
		}
		writer.Fixed("losses", side.Losses);
		WriteSize(writer, state, side);
		if (side.Tactic is { } tactic)
		{
			for (int i = 0; i < state.Tactics.Count; i++)
			{
				if (state.Tactics[i] == tactic)
				{
					writer.Int("tactic", i + 1);
					break;
				}
			}
		}
		if (combat.Kind == ECombatKind.Land)
		{
			foreach (CUnit unit in side.Front)
			{
				writer.IdPair("front", CSaveUnitWriter.Pair(state, unit.Id));
			}
			foreach (CUnit unit in side.Reserves)
			{
				writer.IdPair("reserves", CSaveUnitWriter.Pair(state, unit.Id));
			}
		}
		else if (combat.Kind == ECombatKind.Naval)
		{
			writer.Fixed("positioning", side.Positioning);
		}
		else if (side.IsAttacker && CBombing.IsBombing(combat.Kind))
		{
			writer.Fixed("strength", side.StrengthDealt);
			// The empty building first, then the buildings in their file's order.
			writer.Figures("building", new[] { CFixedPoint.Zero }
				.Concat(state.Buildings.Select(building => side.BuildingsDestroyed.GetValueOrDefault(building.Key))));
			writer.Figures("pool", Goods(side.StockpileDestroyed));
			writer.Figures("current_producing", Goods(side.ProductionDestroyed));
		}
		writer.End();
	}

	/// <summary>
	/// A side's size: a figure for every type of the database, the empty type first, in the database's order - the
	/// original's, which a faithful game's save has to be. An enhanced game's, which the original never reads, names
	/// the types the side has any of and no others, in the database's order (<see cref="SizeByTypeKey"/>); a side with
	/// none writes the block empty, so that a load still sets what its joins counted to nought. Every figure is as
	/// it was: nothing is added up.
	/// </summary>
	private static void WriteSize(CSaveTextWriter writer, CGameState state, CCombatSide side)
	{
		if (state.Rules.Enhanced)
		{
			writer.Begin(SizeByTypeKey);
			foreach (CSubUnitType type in state.SubUnitTypes.Values.OrderBy(type => type.DatabaseIndex))
			{
				CFixedPoint figure = side.Size.GetValueOrDefault(type.Key);
				if (figure.Raw != 0)
				{
					writer.Fixed(type.Key, figure);
				}
			}
			writer.End();
			return;
		}
		var figures = new CFixedPoint[state.SubUnitTypes.Values.Max(type => type.DatabaseIndex) + 1];
		figures[0] = side.Size.GetValueOrDefault(EmptyType);
		foreach (CSubUnitType type in state.SubUnitTypes.Values)
		{
			figures[type.DatabaseIndex] = side.Size.GetValueOrDefault(type.Key);
		}
		writer.Figures("size", figures);
	}

	/// <summary>Seven figures in the goods' order.</summary>
	private static IEnumerable<CFixedPoint> Goods(CGoodsPool pool)
	{
		return Enumerable.Range(0, CGoodsPool.Count).Select(index => pool[(EGoods)index]);
	}

	private const string None = "---";

	/// <summary>A side's country, or none for <c>"---"</c>.</summary>
	private static CCountryTag? Tag(ClausewitzNode entry, string key)
	{
		return CCountryTag.TryParse(entry.Get(key)?.Value?.Trim('"'), out CCountryTag tag) ? tag : null;
	}
}
