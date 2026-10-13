// Checks what this project computes against a savegame the original wrote.
//
//   dotnet run --project tools/OpenHOI3.SaveCheck -- <install folder> <savegame> [examples]
//
// The savegame has to be from the start date, without mods, so that it describes the same
// world the loaders build. Nothing is written: both the install and the save are read only.
// The save is the oracle, so a disagreement here is a fault in this project until shown
// otherwise - and the place to look is docs/mechanics/.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

// Research is checked against two later saves of one game: see CResearchCheck.
if (args.Length > 0 && args[0] == "research")
{
	return CResearchCheck.Run(args);
}

// The ways between provinces are checked against the original's own cache of them.
if (args.Length > 0 && args[0] == "adjacency")
{
	return CAdjacencyCheck.Run(args);
}

// The distances by sea are checked against the original's own cache of them.
if (args.Length > 0 && args[0] == "navaldist")
{
	return CNavalDistanceCheck.Run(args);
}

// The march of land units is checked against two saves of one game, hours apart.
if (args.Length > 0 && args[0] == "march")
{
	return CMarchCheck.Run(args);
}

// The supply network is checked against what a save says of it.
if (args.Length > 0 && args[0] == "supply")
{
	return CSupplyCheck.Run(args);
}

// The carrying of supplies is checked against a save of the start date.
if (args.Length > 0 && args[0] == "supplypass")
{
	return CSupplyPassCheck.Run(args);
}

// A battle is fought with the install's own units and described. A trial, not a check.
if (args.Length > 0 && args[0] == "battle")
{
	return CBattleTrial.Run(args);
}

// The same at sea, between the strongest fleets of two countries.
if (args.Length > 0 && args[0] == "seabattle")
{
	return CSeaBattleTrial.Run(args);
}

// A new game with its two days run is set beside a save of the start date, nothing given to it.
if (args.Length > 0 && args[0] == "firstdays")
{
	return CFirstDaysCheck.Run(args);
}

// One air mission is flown and the battle it comes to is told.
if (args.Length > 0 && args[0] == "airbattle")
{
	return CAirBattleTrial.Run(args);
}

// One bombing mission is flown for some days and what it did is told.
if (args.Length > 0 && args[0] == "bombing")
{
	return CBombingTrial.Run(args);
}

// The technology levels a start-date save has above the history's, and which units hold them.
if (args.Length > 0 && args[0] == "techgap")
{
	return CTechGapCheck.Run(args);
}

// The install's events and decisions are read and every problem in them listed.
if (args.Length > 0 && args[0] == "script")
{
	return CScriptCheck.Run(args);
}

// Every event's and decision's conditions are asked of every country on the start date.
if (args.Length > 0 && args[0] == "conditions")
{
	return CConditionsTrial.Run(args);
}

// Every event option's, immediate block's and decision's text is written, and what came out counted.
if (args.Length > 0 && args[0] == "scripttext")
{
	return CScriptTextTrial.Run(args);
}

// A new game is played for some days, and the events that fired listed.
if (args.Length > 0 && args[0] == "events")
{
	return CEventsTrial.Run(args);
}

// Every event's and decision's effects are run once, and what is unbuilt listed.
if (args.Length > 0 && args[0] == "effects")
{
	return CEffectsTrial.Run(args);
}

// A country's raiders are sent at another's convoys for some days.
if (args.Length > 0 && args[0] == "raid")
{
	return CRaidTrial.Run(args);
}

// A new game's relations, threat and neutrality, beside the start-date save's.
if (args.Length > 0 && args[0] == "diplomacy")
{
	return CDiplomacyCheck.Run(args);
}

// The AI's functions for offers asked of every pair of countries, and what they could not read.
if (args.Length > 0 && args[0] == "aiscore")
{
	return CAiScoreTrial.Run(args);
}

// A new game run hour by hour with the AI trading by the install's Lua.
if (args.Length > 0 && args[0] == "trade")
{
	return CTradeTrial.Run(args);
}

// A war declared on a new game, and who joins it.
if (args.Length > 0 && args[0] == "declare")
{
	return CDeclareTrial.Run(args);
}

if (args.Length < 2)
{
	Console.Error.WriteLine("usage: OpenHOI3.SaveCheck <install folder> <savegame> [examples to show per finding]");
	return 2;
}
int examples = args.Length > 2 && int.TryParse(args[2], out int wanted) ? wanted : 5;

var warnings = new List<string>();
CDirectorySettings directories = CDirectorySettings.At(args[0]);
CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
CMap map = CMap.Load(directories, warnings.Add);
// What the history files give a game, filled: the two days a game then runs before its first
// hour are reasoned about below one system at a time. `firstdays` sets a game that has run
// them beside the save.
CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, firstDays: false);

ClausewitzNode save = ClausewitzParser.ParseFile(args[1]);
Console.WriteLine($"savegame: date {save.GetString("date")}, player {save.GetString("player")}");
Console.WriteLine($"ours:     date {state.Date}.{state.Hour}, {state.Provinces.Count - 1} provinces, {state.Countries.Count} countries");
Console.WriteLine();

var findings = new Dictionary<string, List<string>>();
var compared = new Dictionary<string, int>();
void Compare(string what, bool agrees, Func<string> detail)
{
	compared[what] = compared.GetValueOrDefault(what) + 1;
	if (agrees)
	{
		return;
	}
	if (!findings.TryGetValue(what, out List<string>? list))
	{
		findings[what] = list = new List<string>();
	}
	list.Add(detail());
}

CFixedPoint Number(string? text) => CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
EGoods[] resources = { EGoods.Metal, EGoods.Energy, EGoods.RareMaterials, EGoods.CrudeOil };

// ---- provinces: everything the history files decide
foreach (ClausewitzNode node in save.Children)
{
	if (node.Key is null || !node.IsBlock
		|| !int.TryParse(node.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
		|| id <= 0 || id >= state.Provinces.Count)
	{
		continue;
	}
	CProvince province = state.Provinces[id];
	string where = $"{id} {province.Name}";

	Compare("province owner", node.GetString("owner") == (province.Owner?.Value ?? ""),
		() => $"{where}: save '{node.GetString("owner")}', ours '{province.Owner?.Value}'");
	Compare("province controller", node.GetString("controller") == (province.Controller?.Value ?? ""),
		() => $"{where}: save '{node.GetString("controller")}', ours '{province.Controller?.Value}'");

	List<string> savedCores = node.All("core").Select(core => core.Value ?? "").OrderBy(core => core).ToList();
	List<string> ourCores = province.Cores.Select(core => core.Value).OrderBy(core => core).ToList();
	Compare("province cores", savedCores.SequenceEqual(ourCores),
		() => $"{where}: save [{string.Join(' ', savedCores)}], ours [{string.Join(' ', ourCores)}]");

	foreach (CBuilding building in state.Buildings)
	{
		// The save writes a pair for each building. At the start the two are equal.
		ClausewitzNode? pair = node.Get(building.Key);
		CFixedPoint first = pair is { IsBlock: true, Children.Count: >= 2 } ? Number(pair.Children[0].Value) : CFixedPoint.Zero;
		CFixedPoint second = pair is { IsBlock: true, Children.Count: >= 2 } ? Number(pair.Children[1].Value) : CFixedPoint.Zero;
		CFixedPoint ours = province.BuildingLevel(building);
		Compare("province buildings", first == ours && second == ours,
			() => $"{where} {building.Key}: save {first} / {second}, ours {ours}");
	}

	foreach (EGoods goods in resources)
	{
		string key = CGoodsPool.KeyOf(goods);
		CFixedPoint current = Number(node.Get("current_producing")?.GetString(key));
		CFixedPoint most = Number(node.Get("max_producing")?.GetString(key));
		Compare("province production", current == province.CurrentProducing[goods] && most == province.MaxProducing[goods],
			() => $"{where} {key}: save {current} of {most}, ours {province.CurrentProducing[goods]} of {province.MaxProducing[goods]}");
	}

	Compare("province manpower", Number(node.GetString("manpower")) == province.Manpower,
		() => $"{where}: save {node.GetString("manpower")}, ours {province.Manpower}");
	Compare("province leadership", Number(node.GetString("leadership")) == province.Leadership,
		() => $"{where}: save {node.GetString("leadership")}, ours {province.Leadership}");
	Compare("province victory points", node.GetInt("points") == province.Points,
		() => $"{where}: save {node.GetInt("points")}, ours {province.Points}");
}

// ---- countries: what the history files decide
Dictionary<string, Dictionary<string, CLaw>> lawBook = CModifierReader.ReadLaws(directories, warnings.Add);
HashSet<string> seats = CModifierReader.ReadGovernmentPositions(directories, warnings.Add);
var savedCountries = new List<(ClausewitzNode Node, CCountry Country)>();
foreach (ClausewitzNode node in save.Children)
{
	if (node.Key is not null && node.IsBlock && CCountryTag.TryParse(node.Key, out CCountryTag tag)
		&& state.Country(tag) is { } country)
	{
		savedCountries.Add((node, country));
	}
}

// ---- units: what the orders of battle make, before anything below gives a country the save's laws
List<string> unitLaws = CUnitCheck.Run(directories, savedCountries, state, lawBook, Compare);

// ---- leaders: who is in service, and who commands each of those units
int aiHeadquartersLed = CLeaderCheck.Run(savedCountries, state, save.GetString("player"), Compare);

foreach ((ClausewitzNode node, CCountry country) in savedCountries)
{
	string tag = country.Tag.Value;
	bool existsAtStart = state.Provinces.Any(province => province.Owner?.Value == tag);

	Compare("country capital", node.GetInt("capital") == country.Capital,
		() => $"{tag}: save {node.GetInt("capital")}, ours {country.Capital}");

	// Nothing moves national unity here, and in the save foreign spies have had two
	// days at it - a thousandth or two a day. So it is the history's whole number
	// that is compared.
	if (CFixedPoint.TryParse(node.Get("national_unity")?.Value, out CFixedPoint savedUnity))
	{
		long nearest = (savedUnity.Raw + CFixedPoint.Scale / 2) / CFixedPoint.Scale;
		Compare("country national unity, to the nearest whole number", nearest == country.NationalUnity.ToInt(),
			() => $"{tag}: save {savedUnity}, ours {country.NationalUnity}");
	}

	foreach (string seat in seats)
	{
		string ours = country.Government.TryGetValue(seat, out CMinister? minister)
			? minister.Id.ToString(CultureInfo.InvariantCulture) : "";
		// The original fills the government of a country that does not exist yet by itself.
		Compare(existsAtStart ? "country government" : "country government (no provinces at the start)",
			node.GetString(seat) == ours, () => $"{tag} {seat}: save '{node.GetString(seat)}', ours '{ours}'");
	}

	foreach (string group in lawBook.Keys)
	{
		string ours = country.Laws.TryGetValue(group, out CLaw? law) ? law.Name : "";
		Compare("country laws (the original's AI has already chosen its own by the first save)",
			node.GetString(group).Equals(ours, StringComparison.OrdinalIgnoreCase),
			() => $"{tag} {group}: save '{node.GetString(group)}', ours '{ours}'");
	}

	foreach (CTechnologyCategory category in state.TechnologyCategories)
	{
		CFixedPoint saved = Number(node.GetString(category.Name));
		CFixedPoint ours = country.Ability(category.Name);
		if (saved != CFixedPoint.Zero || ours != CFixedPoint.Zero)
		{
			Compare("country abilities", saved == ours, () => $"{tag} {category.Name}: save {saved}, ours {ours}");
		}
	}

	ClausewitzNode? technology = node.Get("technology");
	if (technology is not null)
	{
		foreach (ClausewitzNode entry in technology.Children)
		{
			if (entry.Key is null || !entry.IsBlock || entry.Children.Count == 0 || !state.Technologies.ContainsKey(entry.Key))
			{
				continue;
			}
			int saved = (int)Number(entry.Children[0].Value).ToInt();
			int ours = country.TechnologyLevels.GetValueOrDefault(entry.Key);
			Compare("country technology levels", saved == ours, () => $"{tag} {entry.Key}: save {saved}, ours {ours}");
		}
	}
}

// ---- what a new game starts with. The save is two daily passes on from that, so two days of
// its own home production and usage come off its stockpile first. Stockpiles are compared by
// capital, because two countries with one capital share one.
EGoods[] startingGoods = { EGoods.Metal, EGoods.RareMaterials, EGoods.Money };
var conversions = new Dictionary<int, (string Who, CGoodsPool Change)>();
foreach (IGrouping<int, (ClausewitzNode Node, CCountry Country)> sharing in savedCountries.GroupBy(saved => saved.Country.Capital))
{
	CProvince capital = state.Province(sharing.Key);
	ClausewitzNode? savedCapital = save.Get(sharing.Key.ToString(CultureInfo.InvariantCulture));
	if (capital.Id == 0 || savedCapital?.Get("pool") is not { } savedPool
		|| !sharing.Any(saved => CResources.HoldsCapital(state, saved.Country)))
	{
		continue;                                            // nobody's stockpile: what was put there has been moved on
	}

	string who = string.Join("+", sharing.Select(saved => saved.Country.Tag.Value));
	foreach (EGoods goods in startingGoods)
	{
		string key = CGoodsPool.KeyOf(goods);
		CFixedPoint expected = Number(savedPool.GetString(key));
		foreach ((ClausewitzNode node, CCountry country) in sharing)
		{
			CFixedPoint home = Number(node.Get("home")?.GetString(key));
			CFixedPoint usage = Number(node.Get("usage")?.GetString(key));
			// A country that does not exist has its usage reset each day and its home
			// production never, so the save holds one day of the first and two of the second.
			expected -= CResources.HoldsCapital(state, country) ? home + home - usage - usage : home - usage - usage;
		}
		Compare("stockpile at the start: " + key, expected == capital.Pool[goods],
			() => $"{who}: the save implies {expected}, ours {capital.Pool[goods]}  (off by {expected - capital.Pool[goods]})");
	}

	// Energy and crude oil have the two conversions between the start and the save as well.
	// What is left over once production and usage are taken off is what they did, and it is
	// compared further down, once the countries have the IC and technology the save gives them.
	var converted = new CGoodsPool();
	foreach (EGoods goods in new[] { EGoods.Energy, EGoods.CrudeOil })
	{
		string key = CGoodsPool.KeyOf(goods);
		CFixedPoint expected = Number(savedPool.GetString(key));
		foreach ((ClausewitzNode node, CCountry country) in sharing)
		{
			CFixedPoint home = Number(node.Get("home")?.GetString(key));
			CFixedPoint usage = Number(node.Get("usage")?.GetString(key));
			expected -= CResources.HoldsCapital(state, country) ? home + home - usage - usage : home - usage - usage;
		}
		converted[goods] = expected - capital.Pool[goods];
	}
	conversions[sharing.Key] = (who, converted);
}

// ---- what is computed. The original's AI has changed laws by the time anything can be saved,
// so each country is first given the laws and the government the save says it has.
var historyLaws = new Dictionary<string, Dictionary<string, CLaw>>();
foreach ((ClausewitzNode node, CCountry country) in savedCountries)
{
	historyLaws[country.Tag.Value] = new Dictionary<string, CLaw>(country.Laws, StringComparer.OrdinalIgnoreCase);
	foreach ((string group, Dictionary<string, CLaw> laws) in lawBook)
	{
		if (laws.TryGetValue(node.GetString(group), out CLaw? law))
		{
			country.Laws[group] = law;
		}
	}
	country.Government.Clear();
	foreach (string seat in seats)
	{
		if (int.TryParse(node.GetString(seat), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
			&& country.Ministers.TryGetValue(id, out CMinister? minister))
		{
			country.Government[seat] = minister;
		}
	}

	// The save has technology levels the history files do not give, and the shares of IC
	// are whatever the country had when it was written.
	foreach (ClausewitzNode entry in node.Get("technology")?.Children ?? new List<ClausewitzNode>())
	{
		if (entry.Key is not null && entry.IsBlock && entry.Children.Count > 0 && state.Technologies.ContainsKey(entry.Key))
		{
			country.TechnologyLevels[entry.Key] = (int)Number(entry.Children[0].Value).ToInt();
		}
	}
	List<ClausewitzNode> shares = node.Get("distribution")?.Children ?? new List<ClausewitzNode>();
	for (int i = 0; i < CProduction.CategoryCount && shares.Count == CProduction.CategoryCount; i++)
	{
		country.ProductionShares[i] = Share(shares[i].Value);
	}
}

// The save is two days in, and a day begins by working out who has which strategic resource.
CTick.RunEntryPass(state);
CStrategicResources.Update(state);
CTick.RunEntryPass(state);

// A share is written with five decimals; the nearest value with fifteen binary places is it.
static CFixed15 Share(string? text)
{
	return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value)
		? new CFixed15((long)Math.Round(value * CFixed15.Scale))
		: CFixed15.Zero;
}

// Nothing moves the shares of a country nobody plays and no AI runs: one that does not exist,
// and the one the player had not touched. For those, the save's figures are what its shares give.
string player = save.GetString("player");
bool Untouched(CCountry country) => !CResources.HoldsCapital(state, country) || country.Tag.Value == player;

foreach ((ClausewitzNode node, CCountry country) in savedCountries)
{
	string tag = country.Tag.Value;
	ClausewitzNode? home = node.Get("home");
	foreach (EGoods goods in resources)
	{
		string key = CGoodsPool.KeyOf(goods);
		CFixedPoint saved = Number(home?.GetString(key));
		if (saved == CFixedPoint.Zero && country.HomeProduced[goods] == CFixedPoint.Zero)
		{
			continue;
		}
		Compare("home production of " + key, saved == country.HomeProduced[goods],
			() => $"{tag}: save {saved}, ours {country.HomeProduced[goods]}  (off by {saved - country.HomeProduced[goods]})");
	}

	// A country that does not exist never has its home production reset, so two days in
	// its money and its supplies are two days' worth.
	bool exists = CResources.HoldsCapital(state, country);
	if (CProduction.TakesPart(state, country))
	{
		CFixedPoint money = Number(home?.GetString("money"));
		CFixedPoint ourMoney = country.HomeProduced[EGoods.Money] * (exists ? 1 : 2);
		Compare("money made in a day", money == ourMoney, () => $"{tag}: save {money}, ours {ourMoney}  (off by {money - ourMoney})");
	}

	// IC is not saved. What industry used is, and it cannot be more than there was.
	CFixedPoint used = Number(node.Get("usage")?.GetString("metal"));
	if (used > CFixedPoint.Zero)
	{
		Compare("IC is at least what the save says industry used", used <= CFixedPoint.FromInt(country.TotalIc),
			() => $"{tag}: the save used {used}, ours has {country.TotalIc} (base {country.BaseIc})");
	}
}

// ---- one day of the shares. Only consumer goods and supplies spend anything here, so a
// country with no units and nothing in its build queue should use exactly what the save says,
// and one with either should use no more than that.
//
// The laws the save shows are not always the laws the last day was run under: the AI goes on
// changing them afterwards, without the date moving. So where a country's figures do not fit
// the save's laws, the laws its history gave it are tried in their place, group by group, and
// the smallest set of them that fits is taken to be what the day saw.
CFixedPoint OurSupplies(CCountry country) =>
	CProduction.SuppliesMade(state, country) * (CResources.HoldsCapital(state, country) ? 1 : 2);
bool Fits(ClausewitzNode node, CCountry country)
{
	bool supplies = Number(node.Get("home")?.GetString("supplies")) == OurSupplies(country);
	bool usage = Number(node.Get("usage")?.GetString("metal")) ==
		CProduction.Spent(country, EProductionCategory.ConsumerGoods) + CProduction.Spent(country, EProductionCategory.Supplies);
	return supplies && (usage || CResources.HoldsCapital(state, country));   // the player has units to pay for too
}
var putBack = new List<string>();
foreach ((ClausewitzNode node, CCountry country) in savedCountries)
{
	if (!CProduction.TakesPart(state, country) || !Untouched(country) || Fits(node, country))
	{
		continue;
	}
	Dictionary<string, CLaw> savedLaws = new(country.Laws, StringComparer.OrdinalIgnoreCase);
	List<string> changed = historyLaws[country.Tag.Value]
		.Where(law => !savedLaws.TryGetValue(law.Key, out CLaw? now) || now != law.Value)
		.Select(law => law.Key).OrderBy(group => group).ToList();
	string? found = null;
	foreach (int subset in Enumerable.Range(1, (1 << changed.Count) - 1).OrderBy(bits => System.Numerics.BitOperations.PopCount((uint)bits)))
	{
		foreach ((string group, CLaw law) in savedLaws)
		{
			country.Laws[group] = law;
		}
		var groups = changed.Where((_, index) => (subset & (1 << index)) != 0).ToList();
		foreach (string group in groups)
		{
			country.Laws[group] = historyLaws[country.Tag.Value][group];
		}
		CTick.RunEntryPass(state);
		if (Fits(node, country))
		{
			found = string.Join(", ", groups);
			break;
		}
	}
	if (found is null)
	{
		foreach ((string group, CLaw law) in savedLaws)
		{
			country.Laws[group] = law;
		}
		CTick.RunEntryPass(state);
	}
	else
	{
		putBack.Add($"{country.Tag.Value} ({found})");
	}
}

foreach ((ClausewitzNode node, CCountry country) in savedCountries)
{
	if (CProduction.TakesPart(state, country) && Untouched(country))
	{
		CFixedPoint supplies = Number(node.Get("home")?.GetString("supplies"));
		Compare("supplies made in a day, where nothing has moved the shares", supplies == OurSupplies(country),
			() => $"{country.Tag.Value}: save {supplies}, ours {OurSupplies(country)}  (off by {supplies - OurSupplies(country)})");
	}
}

// ---- the two conversions. Each capital's holder is given its first day as this project would
// run it - the day's production in, energy into crude oil, crude oil into fuel - and twice
// that is set against what the save's stockpile is short of, or over, by.
foreach ((int capitalId, (string who, CGoodsPool change)) in conversions)
{
	CProvince capital = state.Province(capitalId);
	CCountry? holder = savedCountries.Select(saved => saved.Country)
		.FirstOrDefault(country => country.Capital == capitalId && CResources.HoldsCapital(state, country));
	if (holder is null)
	{
		continue;
	}
	var day = new CGoodsPool();
	day.CopyFrom(capital.Pool);
	foreach (EGoods goods in resources)
	{
		day[goods] += holder.HomeProduced[goods];
	}
	CFixedPoint energy = CConversions.EnergyToConvert(holder, day);
	CFixedPoint oil = energy * CConversions.OilRate(holder);
	day[EGoods.Energy] -= energy;
	day[EGoods.CrudeOil] += oil;
	CFixedPoint refined = CConversions.CrudeOilToRefine(holder, day);

	CFixedPoint energyGone = CFixedPoint.Zero - change[EGoods.Energy];
	Compare("energy turned into crude oil in the save's two days", energyGone == energy + energy,
		() => $"{who}: the save is {energyGone} down, ours would be {energy + energy}  (IC {holder.TotalIc}, {CConversions.OilRate(holder)} oil a unit)");
	CFixedPoint ourOil = oil + oil - refined - refined;
	Compare("crude oil made and refined in the save's two days", change[EGoods.CrudeOil] == ourOil,
		() => $"{who}: the save is {change[EGoods.CrudeOil]} up, ours would be {ourOil}  (IC {holder.TotalIc})");

	// The fuel a new game starts with is so many days of what the units use, and by the save
	// the supply network has carried some of it out of the capital. So it is all the fuel in
	// the provinces the country holds that is counted, less the two days' refining.
	var refining = new CGoodsPool();
	refining.CopyFrom(day);
	CGoodsPool usedBefore = new();
	usedBefore.CopyFrom(holder.ConversionUsed);
	CGoodsPool madeBefore = new();
	madeBefore.CopyFrom(holder.ConversionMade);
	CConversions.CrudeOilIntoFuel(holder, refining);
	CFixedPoint fuelMade = refining[EGoods.Fuel] - day[EGoods.Fuel];
	holder.ConversionUsed.CopyFrom(usedBefore);
	holder.ConversionMade.CopyFrom(madeBefore);

	string holderTag = holder.Tag.Value;
	CFixedPoint fuelHeld = CFixedPoint.Zero;
	foreach (ClausewitzNode province in save.Children)
	{
		if (province.Key is not null && province.IsBlock && char.IsDigit(province.Key[0])
			&& province.GetString("controller") == holderTag)
		{
			fuelHeld += Number(province.Get("pool")?.GetString("fuel"));
		}
	}
	CFixedPoint fuelAtStart = fuelHeld - fuelMade - fuelMade;
	bool alone = savedCountries.Count(saved => saved.Country.Capital == capitalId) == 1;
	Compare(alone ? "fuel at the start: all a country's provinces hold, less two days' refining"
			: "fuel at the start, where two countries share a capital",
		fuelAtStart == capital.Pool[EGoods.Fuel],
		() => $"{who}: the save implies {fuelAtStart}, ours {capital.Pool[EGoods.Fuel]}  (off by {fuelAtStart - capital.Pool[EGoods.Fuel]}; " +
			$"{CStockpile.DaysAtStart(state, holder)} days of {CUnitSupply.DailyUse(state, holder).Fuel}, {fuelMade} refined a day)");
}

// ---- the units two days on, which is where the save is: now that each country has the laws,
// the technology and the shares its last day was run with.
List<string> upkeepLaws = CUpkeepCheck.Run(savedCountries, state, save, historyLaws, lawBook, Compare,
	out List<string> heldToCeilings, out List<string> notHeldToCeilings);

CProduction.RunDaily(state);
string playersDay = "";
foreach ((ClausewitzNode node, CCountry country) in savedCountries)
{
	if (!CProduction.TakesPart(state, country) || !Untouched(country))
	{
		continue;
	}
	string tag = country.Tag.Value;
	ClausewitzNode? usage = node.Get("usage");
	if (CResources.HoldsCapital(state, country))
	{
		CFixedPoint used = Number(usage?.GetString("metal"));
		playersDay = $"{tag}'s last day, by share, spent of given: " + string.Join(", ",
			Enum.GetValues<EProductionCategory>().Select(category =>
				$"{category} {CProduction.Spent(country, category)} of {CProduction.Given(country, category)}"));
		Compare("what the player's industry used on the save's last day, the build queue with it",
			country.Usage[EGoods.Metal] == used,
			() => $"{tag}: the save used {used}, ours {country.Usage[EGoods.Metal]}  (off by {used - country.Usage[EGoods.Metal]}; " +
				string.Join(", ", Enum.GetValues<EProductionCategory>().Select(category =>
					$"{category} {CProduction.Spent(country, category)} of {CProduction.Given(country, category)}")) + ")");
		continue;
	}
	foreach (EGoods goods in new[] { EGoods.Metal, EGoods.Energy, EGoods.RareMaterials })
	{
		string key = CGoodsPool.KeyOf(goods);
		CFixedPoint used = Number(usage?.GetString(key));
		Compare("what a country that does not exist uses in a day: " + key, used == country.Usage[goods],
			() => $"{tag}: save {used}, ours {country.Usage[goods]}  (off by {used - country.Usage[goods]})");
	}
}

// ---- leadership. The total is not saved, but what three of its four shares made in the save's
// two days is: spies in training outright, and officers and diplomatic influence on top of a
// whole number the country started with. The countries have the laws the save shows, or the
// history's where those were put back above.
foreach ((ClausewitzNode node, CCountry country) in savedCountries)
{
	if (!CLeadership.TakesPart(country))
	{
		continue;
	}
	string tag = country.Tag.Value;
	string facts = $"leadership {country.TotalLeadership}";

	List<ClausewitzNode> shares = node.Get("leadership")?.Children ?? new List<ClausewitzNode>();
	bool sameShares = shares.Count == CLeadership.CategoryCount;
	for (int i = 0; i < CLeadership.CategoryCount && sameShares; i++)
	{
		sameShares = Share(shares[i].Value) == country.LeadershipShares[i];
	}
	Compare("shares of leadership", sameShares,
		() => $"{tag}: save {string.Join(" ", shares.Select(share => share.Value))}, ours {string.Join(" ", country.LeadershipShares)}");

	// A country that has somewhere to send spies has sent the whole ones already.
	CFixedPoint spies = Number(node.GetString("spies"));
	CFixedPoint ourSpies = CLeadership.SpiesMade(state, country) * 2;
	CFixedPoint sent = ourSpies - spies;
	Compare("spies trained in the save's two days, less whole spies sent out",
		sent >= CFixedPoint.Zero && sent.Raw % CFixedPoint.Scale == 0,
		() => $"{tag}: save {spies}, ours {ourSpies}  ({facts})");

	CFixedPoint influence = Number(node.GetString("diplo_influence")) - CLeadership.InfluenceMade(state, country) * 2;
	Compare("diplomatic influence, less two days of it, is a whole number", influence.Raw % CFixedPoint.Scale == 0,
		() => $"{tag}: save {node.GetString("diplo_influence")}, two days here {CLeadership.InfluenceMade(state, country) * 2}  ({facts})");
	Compare(CResources.HoldsCapital(state, country)
			? "diplomatic influence at the start"
			: "diplomatic influence at the start, for a country that does not exist",
		influence == country.DiplomaticInfluence,
		() => $"{tag}: the save implies it started with {influence}, ours {country.DiplomaticInfluence}  (base IC {country.BaseIc})");

	CFixedPoint officers = Number(node.GetString("officers")) - CLeadership.OfficersMade(state, country) * 2;
	Compare("officers, less two days of them, are a whole number", officers.Raw % CFixedPoint.Scale == 0,
		() => $"{tag}: save {node.GetString("officers")}, two days here {CLeadership.OfficersMade(state, country) * 2}  ({facts}, recruitment {country.Modifier["officer_recruitment"]})");
	// What its units came with. The theatres the original makes as a game starts bring none.
	Compare("officers at the start", officers == country.Officers,
		() => $"{tag}: the save implies it started with {officers}, ours {country.Officers}  (off by {officers - country.Officers}; " +
			$"ratio {country.OfficersRatio}, {country.AllUnits().Count()} units)");
}

// ---- manpower. A new game gives a country its most, and the save is two days of income on
// from that. Both are worked out here from the laws and the technology the save shows, because
// the original's AI has chosen its laws before a country is set up. The start is worked out
// before strategic resources count, and the two days after.
HashSet<string> landowners = CManpower.Landowners(state);
var startingManpower = new Dictionary<string, CFixedPoint>();
foreach (CCountry country in state.Countries)
{
	country.StrategicResources.Clear();                  // none is worked out until the first day is run
}
CCountryModifiers.Rebuild(state);
CManpower.UpdateMost(state);
foreach ((ClausewitzNode _, CCountry country) in savedCountries)
{
	startingManpower[country.Tag.Value] = CManpower.AtStart(state, country, landowners.Contains(country.Tag.Value));
}
CStrategicResources.Update(state);
CTick.RunEntryPass(state);
foreach ((ClausewitzNode node, CCountry country) in savedCountries)
{
	string tag = country.Tag.Value;
	CFixedPoint saved = Number(node.GetString("manpower"));
	CFixedPoint start = startingManpower[tag];
	CFixedPoint ours = start + CManpower.MadeToday(state, country) * 2;
	string facts = $"started with {start} of {country.MaxManpower}, {country.MonthlyManpower} a month, " +
		$"law {country.Modifier["global_manpower_modifier"]}, technology {country.Technology["manpower_gain"]}" +
		(state.IsAtWar(country.Tag) ? $", at war {CStockpile.MonthsAtWar(state, country)} months" : "");
	bool exists = landowners.Contains(tag);
	Compare(exists ? "manpower two days in" : "manpower two days in, for a country that does not exist", saved == ours,
		() => $"{tag}: save {saved}, ours {ours}  (off by {saved - ours}; {facts})");
}

// ---- report
int faults = 0;
foreach ((string what, int count) in compared.OrderBy(entry => entry.Key))
{
	int wrong = findings.TryGetValue(what, out List<string>? list) ? list.Count : 0;
	Console.WriteLine($"{(wrong == 0 ? "agree   " : "DISAGREE")}  {what}: {count - wrong} of {count}");
	if (list is not null)
	{
		faults += wrong;
		foreach (string line in list.Take(examples))
		{
			Console.WriteLine("            " + line);
		}
	}
}
if (playersDay.Length > 0)
{
	Console.WriteLine();
	Console.WriteLine(playersDay);
}
if (putBack.Count > 0)
{
	Console.WriteLine();
	Console.WriteLine($"{putBack.Count} countries fit only with laws from their history put back, which the AI changed after the last day was run:");
	Console.WriteLine("   " + string.Join("; ", putBack));
}
if (aiHeadquartersLed > 0)
{
	Console.WriteLine();
	Console.WriteLine($"{aiHeadquartersLed} of the headquarters the original's AI made in its two days hold a leader, " +
		"one nobody else was given: the AI is not built.");
}
{
	// A look and not a check: a save keeps nothing of radio range.
	List<CUnit> outOfRange = state.Countries.SelectMany(country => country.AllUnits())
		.Where(unit => CMovementSpeed.IsLand(unit) && CCommandReach.IsOutOfRange(state, unit)).ToList();
	List<CUnit> players = outOfRange.Where(unit => unit.Owner.Value == save.GetString("player")).ToList();
	Console.WriteLine();
	Console.WriteLine($"{outOfRange.Count} land units start out of their chain of command's radio range, {players.Count} of them the player's; " +
		"for example " + string.Join(", ", (players.Count > 0 ? players : outOfRange).Take(6)
			.Select(unit => $"{unit.Owner} '{unit.Name}' in {unit.Location} under '{unit.Parent?.Name}' in {unit.Parent?.Location}")));
}
if (unitLaws.Count > 0)
{
	Console.WriteLine();
	Console.WriteLine($"{unitLaws.Count} countries' units fit a law out of their history better than the save's, which the AI changed after they were made:");
	Console.WriteLine("   " + string.Join("; ", unitLaws));
}
if (upkeepLaws.Count > 0)
{
	Console.WriteLine();
	Console.WriteLine($"{upkeepLaws.Count} countries' upgrades over the two days fit a law out of their history better than the save's, which the AI changed after the last day was run:");
	Console.WriteLine("   " + string.Join("; ", upkeepLaws));
}
if (heldToCeilings.Count + notHeldToCeilings.Count > 0)
{
	Console.WriteLine();
	Console.WriteLine($"{heldToCeilings.Count} countries' brigades are held to the ceiling on their organisation, as a change of law after the two days holds them:");
	Console.WriteLine("   " + string.Join(" ", heldToCeilings));
	Console.WriteLine($"{notHeldToCeilings.Count} countries' brigades are above it, as the two days left them:");
	Console.WriteLine("   " + string.Join(" ", notHeldToCeilings));
}
Console.WriteLine();
Console.WriteLine($"{warnings.Count} warnings while loading");
foreach (string warning in warnings.Take(examples))
{
	Console.WriteLine("   " + (warning.Length > 200 ? warning[..200] + " ..." : warning));
}
return faults == 0 ? 0 : 1;
