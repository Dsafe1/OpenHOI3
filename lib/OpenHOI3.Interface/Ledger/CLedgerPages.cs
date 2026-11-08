using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Ledger;

/// <summary>
/// The Statistics screen's 26 pages, each made from the world as it stands when it is shown: the first six overview, then economy, a page of
/// politicians, and the rest military - tables, and four pages of pies. See <c>docs/mechanics/statistics.md</c>.
/// </summary>
public sealed class CLedgerPages
{
	public const int Count = 26;
	public const int Overview = 0;
	public const int Economy = 6;
	public const int Misc = 12;
	public const int Military = 13;

	private const int FirstPiePage = 22;
	private const float LeastPie = 0.1f;

	private static readonly CColor LandColour = new(1f, 0f, 0f);
	private static readonly CColor AirColour = new(0f, 1f, 0f);
	private static readonly CColor NavalColour = new(0f, 0f, 1f);

	private static readonly EGoods[] AllGoods = (EGoods[])Enum.GetValues(typeof(EGoods));

	private static readonly string[] TypeColoursHex =
	{
		"e6194b", "3cb44b", "ffe119", "4363d8", "f58231", "911eb4", "46f0f0", "f032e6", "bcf60c", "fabebe", "008080", "e6beff",
		"9a6324", "fffac8", "800000", "aaffc3", "808000", "ffd8b1", "000075", "808080",
	};

	private static readonly (string Icon, string Stat)[] BrigadeStats =
	{
		("ledger_sort_str", CSubUnitType.MaxStrength), ("ledger_sort_cw", CSubUnitType.CombatWidth), ("ledger_sort_sa", "soft_attack"),
		("ledger_sort_ha", "hard_attack"), ("ledger_sort_def", "defensiveness"), ("ledger_sort_tou", CSubUnitStats.Toughness),
		("ledger_sort_ap", "ap_attack"), ("ledger_sort_arm", "armor_value"), ("ledger_sort_sof", "softness"), ("ledger_sort_aa", "air_attack"),
		("ledger_sort_ad", "air_defence"), ("ledger_sort_sup", "suppression"), ("ledger_sort_sc", "supply_consumption"),
		("ledger_sort_fc", "fuel_consumption"), ("ledger_sort_speed", "maximum_speed"),
	};

	private static readonly (string Icon, string Stat)[] ShipStats =
	{
		("ledger_sort_range", "range"), ("ledger_sort_distance", "distance"), ("ledger_sort_sur_det", "surface_detection"),
		("ledger_sort_air_det", "air_detection"), ("ledger_sort_sub_det", "sub_detection"), ("ledger_sort_visibi", "visibility"),
		("ledger_sort_sea_d", "sea_defence"), ("ledger_sort_ad", "air_defence"), ("ledger_sort_air_a", "air_attack"),
		("ledger_sort_con_a", "convoy_attack"), ("ledger_sort_sea_a", "sea_attack"), ("ledger_sort_sub_a", "sub_attack"),
		("ledger_sort_shore", "shore_bombardment"), ("ledger_sort_hull", "hull"), ("ledger_sort_pos", "positioning"),
		("ledger_sort_trans", "transport_capability"),
	};

	private static readonly (string Icon, string Stat)[] PlaneStats =
	{
		("ledger_sort_str", CSubUnitType.MaxStrength), ("ledger_sort_org", CSubUnitType.DefaultOrganisation), ("ledger_sort_speed", "maximum_speed"),
		("ledger_sort_sc", "supply_consumption"), ("ledger_sort_fc", "fuel_consumption"), ("ledger_sort_sea_d", "surface_defence"),
		("ledger_sort_air_a", "air_attack"), ("ledger_sort_air_d", "air_defence"), ("sort_strat_a", "strategic_attack"), ("ledger_sort_sa", "soft_attack"),
		("ledger_sort_ha", "hard_attack"), ("ledger_sort_trans", "transport_capability"), ("ledger_sort_sea_a", "sea_attack"), ("ledger_sort_range", "range"),
	};

	private readonly CGameState _state;
	private readonly CCountry _player;
	private readonly Func<string, string> _localise;

	public CLedgerPages(CGameState state, CCountry player, Func<string, string> localise)
	{
		_state = state;
		_player = player;
		_localise = localise;
	}

	public static bool IsPies(int page) => page >= FirstPiePage;

	/// <summary>The next page or the one before, going round.</summary>
	public static int Step(int page, bool forward) => (page + (forward ? 1 : Count - 1)) % Count;

	public CLedgerPage Build(int page)
	{
		CLedgerPage built = Make(page);
		built.Spans = CLedgerColumns.Of(page, built.Columns.Count);
		return built;
	}

	private CLedgerPage Make(int page) => page switch
	{
		0 => NationalStatistics(),
		1 => ProvincesOfValue(),
		2 => Comparison("LEDGER_ALLARMIES_TITLE", EUnitBranch.Land),
		3 => Comparison("LEDGER_ALLNAVIES_TITLE", EUnitBranch.Naval),
		4 => Comparison("LEDGER_ALLAIR_TITLE", EUnitBranch.Air),
		5 => SunkShips(),
		6 => Territory(),
		7 => Buildings(),
		8 => Flows(),
		9 => StrategicResources(0, 9),
		10 => StrategicResources(9, int.MaxValue),
		11 => Convoys(),
		12 => Politicians(),
		13 => Brigades(),
		14 => Fleets("LEDGER_SHIP_TITLE", EUnitKind.Navy, "LEDGER_FLEET"),
		15 => Fleets("LEDGER_PLANE_TITLE", EUnitKind.Air, "LEDGER_WING"),
		16 => UnitStats("LEDGER_BRIGADE_STATS", IsLand, BrigadeStats, withExperience: true),
		17 => UnitStats("LEDGER_SHIP_STATS", unit => unit.Kind == EUnitKind.Navy, ShipStats, withExperience: false),
		18 => UnitStats("LEDGER_PLANE_STATS", unit => unit.Kind == EUnitKind.Air, PlaneStats, withExperience: false),
		19 => Commanders("LEDGER_LAND_COMMMANDER", ELeaderType.Land),
		20 => Commanders("LEDGER_SEA_COMMMANDER", ELeaderType.Sea),
		21 => Commanders("LEDGER_AIR_COMMMANDER", ELeaderType.Air),
		22 => PiesOfAll(),
		23 => PiesOfBranch("LEDGER_PIE_LAND", EUnitBranch.Land),
		24 => PiesOfBranch("LEDGER_PIE_AIR", EUnitBranch.Air),
		_ => PiesOfBranch("LEDGER_PIE_NAVAL", EUnitBranch.Naval),
	};

	private static bool IsLand(CUnit unit) => unit.Kind is not EUnitKind.Navy and not EUnitKind.Air;

	private CLedgerColumn Column(string key) => new(_localise(key));

	private string CountryName(CCountryTag? tag) => tag is { } known ? _state.Country(known)?.Name ?? known.Value : "";

	private string FactionName(CCountry country) => country.Faction.Length == 0 ? "" : _localise(country.Faction);

	private IEnumerable<CProvince> Provinces => _state.Provinces.Where(province => province is not null);

	/// <summary>Every country holding land: its faction and victory points. Points off the map are not held here and read nought.</summary>
	private CLedgerPage NationalStatistics()
	{
		var columns = new List<CLedgerColumn>
		{
			Column("LEDGER_COUNTRY"), Column("LEDGER_FACTION"), Column("LEDGER_VP"), Column("LEDGER_OFFMAP_VP"), Column("LEDGER_TOTAL_VP"),
		};
		var rows = new List<CLedgerRow>();
		foreach (CCountry country in _state.Countries.Where(country => CCountryLists.OwnedCount(_state, country.Tag) > 0))
		{
			float points = Provinces.Where(province => province.Controller == country.Tag).Sum(province => (float)province.Points);
			rows.Add(new CLedgerRow(country.Name, new[]
			{
				CLedgerCell.Of(country.Name), CLedgerCell.Of(FactionName(country)), CLedgerCell.Float(points), CLedgerCell.Float(0f), CLedgerCell.Float(points),
			}));
		}
		return new CLedgerPage(_localise("ELEDGER_NATIONAL_STATISTICS"), columns, rows);
	}

	/// <summary>Every province with victory points: who holds it, and its points in the column of the holder's faction.</summary>
	private CLedgerPage ProvincesOfValue()
	{
		var factions = _state.VictoryConditions.Select(entry => entry.Faction).ToList();
		var columns = new List<CLedgerColumn> { Column("LEDGER_PROVINCE"), Column("LEDGER_VALUE"), Column("LEDGER_CONTROLLER"), Column("LEDGER_NEUTRAL") };
		columns.AddRange(factions.Select(Column));
		var rows = new List<CLedgerRow>();
		foreach (CProvince province in Provinces.Where(province => province.Points > 0))
		{
			string faction = province.Controller is { } holder ? _state.Country(holder)?.Faction ?? "" : "";
			var cells = new List<CLedgerCell>
			{
				CLedgerCell.Of(province.Name), CLedgerCell.Int(province.Points), CLedgerCell.Of(CountryName(province.Controller)),
				CLedgerCell.Int(faction.Length == 0 ? province.Points : 0),
			};
			cells.AddRange(factions.Select(each => CLedgerCell.Int(each == faction ? province.Points : 0)));
			rows.Add(new CLedgerRow(province.Name, cells));
		}
		return new CLedgerPage("Nation Statistics", columns, rows);
	}

	/// <summary>The player, the faction's members and allies, and its puppets: how many brigades of each buildable type of the kind each has.</summary>
	private CLedgerPage Comparison(string title, EUnitBranch branch)
	{
		List<CSubUnitType> types = _state.SubUnitTypes.Values.Where(type => type.Branch == branch && CUnitAvailability.IsBuildableType(type)).ToList();
		var columns = new List<CLedgerColumn> { Column("LEDGER_COUNTRY") };
		columns.AddRange(types.Select(type => new CLedgerColumn(_localise(type.Key + "_short"))));
		columns.Add(Column("LEDGER_TOTAL"));
		var rows = new List<CLedgerRow>();
		foreach (CCountry country in Compared())
		{
			var counts = new Dictionary<CSubUnitType, int>();
			foreach (CUnit unit in country.AllUnits())
			{
				foreach (CSubUnit brigade in unit.SubUnits)
				{
					counts[brigade.Type] = counts.GetValueOrDefault(brigade.Type) + 1;
				}
			}
			var cells = new List<CLedgerCell> { CLedgerCell.Of(country.Name) };
			cells.AddRange(types.Select(type => CLedgerCell.Int(counts.GetValueOrDefault(type))));
			cells.Add(CLedgerCell.Int(types.Sum(type => counts.GetValueOrDefault(type))));
			rows.Add(new CLedgerRow(country.Name, cells));
		}
		return new CLedgerPage(_localise(title), columns, rows);
	}

	private IEnumerable<CCountry> Compared()
	{
		var seen = new HashSet<CCountry> { _player };
		yield return _player;
		foreach (CCountry ally in CCountryLists.Allies(_state, _player).Where(ally => CCountryLists.OwnedCount(_state, ally.Tag) > 0))
		{
			if (seen.Add(ally))
			{
				yield return ally;
			}
		}
		foreach (CCountry other in _state.Countries.Where(other => other.Overlord == _player.Tag && CCountryLists.OwnedCount(_state, other.Tag) > 0))
		{
			if (seen.Add(other))
			{
				yield return other;
			}
		}
	}

	private CLedgerPage SunkShips()
	{
		var columns = new[] { "LEDGER_OWNER", "LEDGER_SHIP", "LEDGER_TYPE", "LEDGER_SINKER_NAME", "LEDGER_SINKER_TYPE", "LEDGER_SINKER_COUNTRY" }.Select(Column).ToList();
		var rows = new List<CLedgerRow>();
		foreach (CSunkShip sunk in _state.SunkShips)
		{
			CUnit? sinker = _state.Countries.SelectMany(country => country.AllUnits()).FirstOrDefault(unit => unit.Id == sunk.Ship.SunkById);
			rows.Add(new CLedgerRow(sunk.Ship.Name, new[]
			{
				CLedgerCell.Of(CountryName(sunk.Owner)), CLedgerCell.Of(sunk.Ship.Name), CLedgerCell.Of(_localise(sunk.Ship.Type.Key)),
				CLedgerCell.Of(sinker?.Name ?? ""), CLedgerCell.Of(sinker is null ? "" : _localise(sinker.SubUnits.FirstOrDefault()?.Type.Key ?? "")),
				CLedgerCell.Of(CountryName(sinker?.Owner)),
			}));
		}
		return new CLedgerPage(_localise("LEDGER_SUNK_SHIP"), columns, rows);
	}

	private IEnumerable<CProvince> Controlled => Provinces.Where(province => province.Controller == _player.Tag);

	private CLedgerPage Territory()
	{
		var columns = new[] { "LEDGER_PROVINCE", "LEDGER_ENERGY", "LEDGER_METAL", "LEDGER_RARE", "LEDGER_OIL", "LEDGER_MP", "LEDGER_LEADER" }.Select(Column).ToList();
		var rows = new List<CLedgerRow>();
		foreach (CProvince province in Controlled)
		{
			rows.Add(new CLedgerRow(province.Name, new[]
			{
				CLedgerCell.Of(province.Name), Figure(province.CurrentProducing[EGoods.Energy]), Figure(province.CurrentProducing[EGoods.Metal]),
				Figure(province.CurrentProducing[EGoods.RareMaterials]), Figure(province.CurrentProducing[EGoods.CrudeOil]), Figure(province.Manpower),
				Figure(province.Leadership),
			}));
		}
		return new CLedgerPage(_localise("LEDGER_TERRITORIAL_TITLE"), columns, rows);
	}

	private static CLedgerCell Figure(CFixedPoint value) => CLedgerCell.Float(value.Raw / 1000f);

	private CLedgerPage Buildings()
	{
		var columns = new List<CLedgerColumn> { Column("LEDGER_PROVINCE") };
		columns.AddRange(_state.Buildings.Select(building => new CLedgerColumn("", building.Key + "_icon")));
		var rows = new List<CLedgerRow>();
		foreach (CProvince province in Controlled)
		{
			var cells = new List<CLedgerCell> { CLedgerCell.Of(province.Name) };
			cells.AddRange(_state.Buildings.Select(building => CLedgerCell.Int(province.BuildingLevel(building).Raw / 1000)));
			rows.Add(new CLedgerRow(province.Name, cells));
		}
		return new CLedgerPage(_localise("LEDGER_BUILDING_TITLE"), columns, rows);
	}

	/// <summary>A row for each of the country's pools that has one here. Those the game does not keep apart read nought.</summary>
	private CLedgerPage Flows()
	{
		var columns = new List<CLedgerColumn> { Column("LEDGER_CATEGORY") };
		columns.AddRange(AllGoods.Select(goods => new CLedgerColumn(_localise(OpenHOI3.Interface.Screens.CTradeWindowText.GoodsName(goods)))));
		(string Key, CGoodsPool? Pool)[] pools =
		{
			("LEDGER_HOME_PRODUCED", _player.HomeProduced), ("LEDGER_CONVOYED_IN", _player.ConvoyedIn), ("LEDGER_CONVOYED_OUT", _player.ConvoyedOut),
			("LEDGER_REPAID_DEBT", null), ("LEDGER_INCOMING_DEBT", null), ("LEDGER_CONVERTED_TO", _player.ConversionMade), ("LEDGER_CONVERTED_FROM", _player.ConversionUsed),
			("LEDGER_PUPPET_FROM", null), ("LEDGER_PUPPET_TO", null), ("LEDGER_USED_INDUSTRY", null), ("LEDGER_USED_TROOPS", _player.Usage),
			("Into Network", _player.SentTo), ("LEDGER_STOCKPILE", _player.SentBack), ("LEDGER_FROM_EXILES", _player.ReceivedFromExiles),
			("LEDGER_TRADED_FOR", _player.TradedFor), ("LEDGER_TRADED_AWAY", _player.TradedAway),
		};
		var rows = new List<CLedgerRow>();
		for (int index = 0; index < pools.Length; index++)
		{
			(string key, CGoodsPool? pool) = pools[index];
			var cells = new List<CLedgerCell> { CLedgerCell.Of(_localise(key)) };
			cells.AddRange(AllGoods.Select(goods => Figure(pool is null ? CFixedPoint.Zero : pool[goods])));
			rows.Add(new CLedgerRow(index.ToString("D2", CultureInfo.InvariantCulture), cells));
		}
		return new CLedgerPage(_localise("LEDGER_FLOW_TITLE"), columns, rows);
	}

	/// <summary>
	/// A column for each strategic resource, its icon and name, in the file's order: the first nine on one page and the rest on the next. Each row is the next province of each
	/// resource, its holder's flag and its name.
	/// </summary>
	private CLedgerPage StrategicResources(int from, int to)
	{
		List<string> all = _state.StrategicResourceNames.ToList();
		List<string> shown = all.Skip(from).Take(to == int.MaxValue ? all.Count : to - from).ToList();
		List<List<CProvince>> byResource = shown.Select(name => Provinces.Where(province => province.StrategicResource == name).ToList()).ToList();
		var columns = shown.Select(name => new CLedgerColumn(_localise(name), "ledger_" + name + "_icon")).ToList();
		var rows = new List<CLedgerRow>();
		int tallest = byResource.Select(list => list.Count).DefaultIfEmpty(0).Max();
		for (int row = 0; row < tallest; row++)
		{
			int index = row;
			rows.Add(new CLedgerRow(row.ToString("D8", CultureInfo.InvariantCulture), byResource.Select(list => index < list.Count ? ResourceCell(list[index]) : CLedgerCell.Of("")).ToList()));
		}
		return new CLedgerPage(_localise("PW_STRAT_RESOURCES"), columns, rows);
	}

	private CLedgerCell ResourceCell(CProvince province)
	{
		string flag = province.Controller is { } holder ? "@" + holder.Value + " " : "";
		return new CLedgerCell(flag + province.Name, Tooltip: ResourceTooltip(province));
	}

	/// <summary>Who holds it, and their faction: green for the player's own, red for another, white where the player has none.</summary>
	private string ResourceTooltip(CProvince province)
	{
		string text = _localise("LEDGER_STRAT_RES_CONTROL_TOOLTIP_1") + " " + CountryName(province.Controller);
		if (province.Controller is { } holder && _state.Country(holder) is { Faction.Length: > 0 } country)
		{
			char colour = _player.Faction.Length == 0 ? 'W' : _player.Faction == country.Faction ? 'G' : 'R';
			text += " (§" + colour + FactionName(country) + "§!)";
		}
		return text + " " + _localise("LEDGER_STRAT_RES_CONTROL_TOOLTIP_2") + " " + province.Name;
	}

	private CLedgerPage Convoys()
	{
		var columns = new[] { "LEDGER_FROM", "LEDGER_TO", "LEDGER_TRANSPORTS", "LEDGER_ESCORTS" }.Select(Column).ToList();
		EGoods[] carried = { EGoods.Metal, EGoods.Energy, EGoods.CrudeOil, EGoods.RareMaterials, EGoods.Supplies };
		columns.AddRange(carried.Select(goods => new CLedgerColumn(_localise(OpenHOI3.Interface.Screens.CTradeWindowText.GoodsName(goods)))));
		var rows = new List<CLedgerRow>();
		foreach (CCountry country in _state.Countries)
		{
			foreach (var convoy in country.Convoys)
			{
				var cells = new List<CLedgerCell>
				{
					CLedgerCell.Of(_state.Province(convoy.Start).Name), CLedgerCell.Of(_state.Province(convoy.End).Name),
					CLedgerCell.Int(convoy.Transports), CLedgerCell.Int(convoy.Escorts),
				};
				cells.AddRange(carried.Select(goods => Figure(convoy.Daily[goods])));
				rows.Add(new CLedgerRow(convoy.Id.ToString("D8", CultureInfo.InvariantCulture), cells));
			}
		}
		return new CLedgerPage(_localise("LEDGER_CONVOYS_TITLE"), columns, rows);
	}

	/// <summary>Each minister in each post open to them, and whether their ideology is one the government has.</summary>
	private CLedgerPage Politicians()
	{
		var columns = new[] { "LEDGER_NAME", "LEDGER_IDEOLOGY", "LEDGER_POSSIBLE", "LEDGER_PERSONALITY", "LEDGER_JOB" }.Select(Column).ToList();
		var rows = new List<CLedgerRow>();
		var government = _state.GovernmentOf(_player);
		foreach (CMinister minister in _player.Ministers.Values)
		{
			foreach (var position in _state.GovernmentPositions.Where(each => minister.MayTake(each.Name)))
			{
				bool possible = government is not null && government.Accepted.Contains(minister.Ideology);
				bool holds = _player.Government.TryGetValue(position.Name, out CMinister? holder) && ReferenceEquals(holder, minister);
				rows.Add(new CLedgerRow(minister.Name + position.Index.ToString("D2", CultureInfo.InvariantCulture), new[]
				{
					CLedgerCell.Of(minister.Name), CLedgerCell.Of(_localise(minister.Ideology)), CLedgerCell.Of(_localise(possible ? "YES" : "NO")),
					CLedgerCell.Of(minister.TypeIn(position.Name) is { } type ? _localise(type.Name) : ""),
					CLedgerCell.Of(holds ? _localise(position.Name) : _localise("LEDGER_NONE")),
				}));
			}
		}
		return new CLedgerPage(_localise("LEDGER_POLITICIANS_TITLE"), columns, rows);
	}

	private IEnumerable<CUnit> PlayerUnits(Func<CUnit, bool> kind) => _player.AllUnits().Where(unit => !unit.IsRemoved && kind(unit));

	private static string Higher(CUnit unit, EUnitKind kind)
	{
		for (CUnit? at = unit; at is not null; at = at.Parent)
		{
			if (at.Kind == kind)
			{
				return at.Name;
			}
		}
		return "";
	}

	private CLedgerPage Brigades()
	{
		var columns = new[]
		{
			"LEDGER_NAME", "LEDGER_TYPE", "LEDGER_STRENGTH", "LEDGER_ORG", "LEDGER_LOCATION", "LEDGER_DIVISION", "LEDGER_CORP", "LEDGER_ARMY", "LEDGER_ARMYGROUP", "LEDGER_THEATRE", "LEDGER_ORDER",
		}.Select(Column).ToList();
		var rows = new List<CLedgerRow>();
		foreach (CUnit unit in PlayerUnits(IsLand))
		{
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				rows.Add(new CLedgerRow(brigade.Name, new[]
				{
					CLedgerCell.Of(brigade.Name), CLedgerCell.Of(_localise(brigade.Type.Key)), Figure(brigade.Strength), Figure(brigade.Organisation),
					CLedgerCell.Of(unit.Location > 0 ? _state.Province(unit.Location).Name : ""), CLedgerCell.Of(Higher(unit, EUnitKind.Division)),
					CLedgerCell.Of(Higher(unit, EUnitKind.Corps)), CLedgerCell.Of(Higher(unit, EUnitKind.Army)), CLedgerCell.Of(Higher(unit, EUnitKind.ArmyGroup)),
					CLedgerCell.Of(Higher(unit, EUnitKind.Theatre)), CLedgerCell.Of(""),
				}));
			}
		}
		return new CLedgerPage(_localise("LEDGER_BRIGADE_TITLE"), columns, rows);
	}

	private CLedgerPage Fleets(string title, EUnitKind kind, string groupColumn)
	{
		var columns = new[] { "LEDGER_NAME", "LEDGER_TYPE", "LEDGER_STRENGTH", "LEDGER_ORG", "LEDGER_LOCATION", groupColumn, "LEDGER_BASE", "LEDGER_ORDER" }.Select(Column).ToList();
		var rows = new List<CLedgerRow>();
		foreach (CUnit unit in PlayerUnits(each => each.Kind == kind))
		{
			foreach (CSubUnit ship in unit.SubUnits)
			{
				rows.Add(new CLedgerRow(ship.Name, new[]
				{
					CLedgerCell.Of(ship.Name), CLedgerCell.Of(_localise(ship.Type.Key)), Figure(ship.Strength), Figure(ship.Organisation),
					CLedgerCell.Of(unit.Location > 0 ? _state.Province(unit.Location).Name : ""), CLedgerCell.Of(unit.Name),
					CLedgerCell.Of(unit.Base > 0 ? _state.Province(unit.Base).Name : ""), CLedgerCell.Of(""),
				}));
			}
		}
		return new CLedgerPage(_localise(title), columns, rows);
	}

	private CLedgerPage UnitStats(string title, Func<CUnit, bool> kind, (string Icon, string Stat)[] stats, bool withExperience)
	{
		var columns = new List<CLedgerColumn> { Column("LEDGER_NAME"), Column("LEDGER_TYPE"), Column("LEDGER_MODELNAME") };
		columns.AddRange(stats.Select(stat => new CLedgerColumn("", stat.Icon)));
		if (withExperience)
		{
			columns.Add(Column("LEDGER_EXPERIENCE2"));
		}
		var rows = new List<CLedgerRow>();
		foreach (CUnit unit in PlayerUnits(kind))
		{
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				var cells = new List<CLedgerCell> { CLedgerCell.Of(brigade.Name), CLedgerCell.Of(_localise(brigade.Type.Key)), CLedgerCell.Of("") };
				cells.AddRange(stats.Select(stat => Figure(brigade.Stats[stat.Stat])));
				if (withExperience)
				{
					cells.Add(CLedgerCell.Percent(brigade.Experience.Raw / 1000f / 100f));
				}
				rows.Add(new CLedgerRow(brigade.Name, cells));
			}
		}
		return new CLedgerPage(_localise(title), columns, rows);
	}

	private CLedgerPage Commanders(string title, ELeaderType type)
	{
		var columns = new[]
		{
			"LEDGER_NAME", "LEDGER_RANK", "LEDGER_SKILL", "LEDGER_EXPERIENCE", "LEDGER_COMMISION", "LEDGER_TRAIT1", "LEDGER_TRAIT2", "LEDGER_TRAIT3", "LEDGER_TRAIT4",
		}.Select(Column).ToList();
		var rows = new List<CLedgerRow>();
		foreach (CLeader leader in _player.ActiveLeaders.Where(leader => leader.Type == type))
		{
			var cells = new List<CLedgerCell>
			{
				CLedgerCell.Of(leader.Name), CLedgerCell.Int(leader.Rank), CLedgerCell.Int(leader.Skill), CLedgerCell.Int(leader.Experience.Raw / CFixed15.Scale),
				CLedgerCell.Of(leader.Unit?.Name ?? _localise("LEDGER_NONE")),
			};
			for (int trait = 0; trait < 4; trait++)
			{
				cells.Add(CLedgerCell.Of(trait < leader.Traits.Count ? _localise(leader.Traits[trait].Key) : ""));
			}
			rows.Add(new CLedgerRow(leader.Name, cells, leader.Unit));
		}
		return new CLedgerPage(_localise(title), columns, rows);
	}

	/// <summary>Four figures for a set of brigades: how many, what they use of supplies and of fuel, and what it would take to fill them.</summary>
	private (float Count, float Supply, float Fuel, float Need) Measure(IEnumerable<CSubUnit> brigades)
	{
		float count = 0f;
		float supply = 0f;
		float fuel = 0f;
		float need = 0f;
		foreach (CSubUnit brigade in brigades)
		{
			count += 1f;
			supply += brigade.Stats["supply_consumption"].Raw / 1000f;
			fuel += brigade.Stats["fuel_consumption"].Raw / 1000f;
			need += Math.Max(0, brigade.Stats[CSubUnitType.MaxStrength].Raw - brigade.Strength.Raw) / 1000f;
		}
		return (count, supply, fuel, need);
	}

	private IEnumerable<CSubUnit> PlayerBrigades(EUnitBranch branch) =>
		PlayerUnits(_ => true).SelectMany(unit => unit.SubUnits).Where(brigade => brigade.Type.Branch == branch);

	private CLedgerPage PiesOfAll()
	{
		(EUnitBranch Branch, string Label, CColor Colour)[] branches =
		{
			(EUnitBranch.Land, "LEDGER_BRIGADE_TITLE", LandColour), (EUnitBranch.Air, "AIRWINGS", AirColour), (EUnitBranch.Naval, "LEDGER_SHIP_TITLE", NavalColour),
		};
		string[] plainLabels = { "LEDGER_LAND", "LEDGER_AIR", "LEDGER_NAVAL" };
		var measures = branches.Select(branch => Measure(PlayerBrigades(branch.Branch))).ToList();
		var pies = new List<CLedgerPie>
		{
			Pie("LEDGER_UNIT_SPLIT", branches.Select((branch, index) => new CLedgerSlice(_localise(branch.Label), measures[index].Count, branch.Colour))),
			Pie("LEDGER_SUPPLY_CONSUMPTION", branches.Select((branch, index) => new CLedgerSlice(_localise(plainLabels[index]), measures[index].Supply, branch.Colour))),
			Pie("LEDGER_FUEL_CONSUMPTION", branches.Select((branch, index) => new CLedgerSlice(_localise(plainLabels[index]), measures[index].Fuel, branch.Colour))),
			Pie("LEDGER_UNIT_REINFORCE_NEED", branches.Select((branch, index) => new CLedgerSlice(_localise(plainLabels[index]), measures[index].Need, branch.Colour))),
		};
		return new CLedgerPage(_localise("LEDGER_PIE_ALL"), pies);
	}

	private CLedgerPage PiesOfBranch(string title, EUnitBranch branch)
	{
		var byType = PlayerBrigades(branch).GroupBy(brigade => brigade.Type).OrderBy(group => group.Key.DatabaseIndex).ToList();
		var measures = byType.Select(group => (Group: group, Measure: Measure(group))).ToList();
		CLedgerPie Of(string heading, Func<(float Count, float Supply, float Fuel, float Need), float> value) =>
			Pie(heading, measures.Select((each, index) => new CLedgerSlice(_localise(each.Group.Key.Key), value(each.Measure), TypeColour(index))));
		return new CLedgerPage(_localise(title), new List<CLedgerPie>
		{
			Of("LEDGER_UNIT_SPLIT", measure => measure.Count), Of("LEDGER_SUPPLY_CONSUMPTION", measure => measure.Supply),
			Of("LEDGER_FUEL_CONSUMPTION", measure => measure.Fuel), Of("LEDGER_UNIT_REINFORCE_NEED", measure => measure.Need),
		});
	}

	private static CColor TypeColour(int index)
	{
		string hex = TypeColoursHex[index % TypeColoursHex.Length];
		return new CColor(int.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber) / 255f, int.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber) / 255f,
			int.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber) / 255f);
	}

	/// <summary>A pie with no slices where its total is below a tenth.</summary>
	private CLedgerPie Pie(string heading, IEnumerable<CLedgerSlice> slices)
	{
		List<CLedgerSlice> list = slices.Where(slice => slice.Value > 0f).ToList();
		return new CLedgerPie(_localise(heading), list.Sum(slice => slice.Value) > LeastPie ? list : new List<CLedgerSlice>());
	}
}
