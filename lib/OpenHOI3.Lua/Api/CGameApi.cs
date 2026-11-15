using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The classes the original hands its scripts, by its names, with the methods built so far on the
/// simulation. A method a script calls that is not here fails as a Lua error naming it, so the
/// script stops there and the call it was part of is reported; a method that reads what the
/// simulation does not hold answers nought and reports that once. See <c>docs/mechanics/lua.md</c>,
/// section 4.
/// </summary>
public sealed class CGameApi
{
	/// <summary>The null tag, the original's country number 0, which nothing is.</summary>
	public static readonly CCountryTag NullTag = new("---");

	/// <summary>What a report of something not built begins with.</summary>
	public const string UnbuiltPrefix = "LUA unbuilt: ";

	private readonly Func<CGameState> _state;
	private readonly Action<string> _report;
	private readonly HashSet<string> _unbuilt = new(StringComparer.Ordinal);
	private readonly Dictionary<string, CLuaNamed> _governments = new(StringComparer.Ordinal);
	private readonly Dictionary<string, CLuaNamed> _factions = new(StringComparer.Ordinal);
	private readonly Dictionary<string, CLuaNamed> _ideologies = new(StringComparer.Ordinal);
	private readonly Dictionary<string, CLuaNamed> _continents = new(StringComparer.Ordinal);

	public CGameApi(Func<CGameState> state, Action<string>? report = null)
	{
		_state = state;
		_report = report ?? (_ => { });
		Text = CStringClass.Define();
		FixedPoint = CFixedPointClass.Define();
		Date = CDateClass.Define();
		Government = new CLuaClass("CGovernment").Operator("__eq", CLuaEquality.Same<CLuaNamed>("CGovernment"));
		Faction = CFactionClass.Define(this);
		IdeologyGroup = CIdeologyClass.DefineGroup(this);
		Ideology = CIdeologyClass.DefineIdeology(this);
		Continent = CProvinceClass.DefineContinent(this);
		Province = CProvinceClass.DefineProvince(this);
		GoodsPool = CGoodsPoolClass.Define(this);
		Strategy = CAiClass.DefineStrategy(this);
		Country = CCountryClass.Define(this);
		CountryTag = CCountryTagClass.Define(this);
		DiplomacyStatus = CDiplomacyStatusClass.Define(this);
		Ai = CAiClass.DefineAi(this);
		ResourceValues = CResourceValuesClass.Define(this);
		GameState = CGameStateClass.DefineGameState(this);
		CountryDataBase = CGameStateClass.DefineCountryDataBase(this);
		Intel = CAiIntelClass.Define(this);
		TradeAction = CTradeActionClass.DefineAction(this);
		TradeRoute = CTradeActionClass.DefineRoute(this);
		GoodsValues = CGoodsValuesClass.Define();
		Modifier = CCountryModifiersClass.DefineModifier(this);
		TechnologyStatus = CTechnologyStatusClass.Define(this);
		SubUnit = CLicenceActionClass.DefineSubUnit(this);
		SubUnitDataBase = CLicenceActionClass.DefineSubUnitDataBase(this);
		LicenceAction = CLicenceActionClass.DefineAction(this);
		LawGroup = CLawClass.DefineGroup(this);
		Law = CLawClass.DefineLaw(this);
		LawDataBase = CLawClass.DefineDataBase(this);
		MinisterType = CMinisterClass.DefineType(this);
		Minister = CMinisterClass.DefineMinister(this);
		GovernmentPosition = CMinisterClass.DefinePosition(this);
		PositionDataBase = CMinisterClass.DefinePositionDataBase(this);
		PoliticsMinister = CPoliticsMinisterClass.Define(this);
		ChangeLaw = CPoliticsCommandClass.DefineChangeLaw(this);
		ChangeMinister = CPoliticsCommandClass.DefineChangeMinister(this);
		ToggleMobilization = CPoliticsCommandClass.DefineToggleMobilization(this);
		Liberate = CPoliticsCommandClass.DefineLiberate(this);
		CreateVassal = CPoliticsCommandClass.DefineCreateVassal(this);
		SpyPresence = CSpyPresenceClass.Define(this);
		IdeologyData = CIdeologyDataClass.Define(this);
		IntelligenceMinister = CIntelligenceMinisterClass.Define(this);
		ChangeSpyPriority = CSpyCommandClass.DefinePriority(this);
		ChangeSpyMission = CSpyCommandClass.DefineMission(this);
		Technology = CTechnologyClass.DefineTechnology(this);
		TechnologyFolder = CTechnologyClass.DefineFolder(this);
		TechnologyDataBase = CTechnologyClass.DefineDataBase(this);
		Building = CBuildingClass.DefineBuilding(this);
		BuildingDataBase = CBuildingClass.DefineDataBase(this);
		ProvinceBuilding = CBuildingClass.DefineProvinceBuilding(this);
		Construction = CConstructionClass.DefineConstruction(this);
		MilitaryConstruction = CConstructionClass.DefineMilitary(this);
		BrigadeConstruction = CConstructionClass.DefineBrigade(this);
		ArrayInt = CArrayClass.Define("CArrayInt");
		ArrayFix = CArrayClass.Define("CArrayFix");
		ArrayCountryTag = CArrayClass.Define("CArrayCountryTag");
		War = CWarClass.Define(this);
		ForeignMinister = CForeignMinisterClass.Define(this);
		Decision = CDecisionClass.DefineDecision(this);
		EventScope = CDecisionClass.DefineScope();
		Flags = CFlagsClass.Define();
		DiplomaticActions = CDiplomaticActionClass.Kinds.Select(kind => CDiplomaticActionClass.Define(this, kind.Name, kind.Kind)).ToArray();
		TechMinister = CTechMinisterClass.Define(this);
		ProductionMinister = CProductionMinisterClass.Define(this);
		ProductionCommands = new[]
		{
			CProductionCommandClass.DefineSubUnitList(this),
			CProductionCommandClass.DefineId(this),
			CProductionCommandClass.DefineNullTag(this),
			CProductionCommandClass.DefineConstructUnit(this),
			CProductionCommandClass.DefineConstructBuilding(this),
			CProductionCommandClass.DefineConstructConvoy(this),
			CProductionCommandClass.DefineChangeInvestment(this),
			CProductionCommandClass.DefineChangeLeadership(this),
			CProductionCommandClass.DefineStartResearch(this),
			CProductionCommandClass.DefineLendLeaseDistribution(this),
		};
	}

	/// <summary>The tech and production ministers (docs/mechanics/ministers.md).</summary>
	public CLuaClass TechMinister { get; }

	public CLuaClass ProductionMinister { get; }

	public CLuaClass Technology { get; }

	public CLuaClass TechnologyFolder { get; }

	public CLuaClass TechnologyDataBase { get; }

	public CLuaClass Building { get; }

	public CLuaClass BuildingDataBase { get; }

	public CLuaClass ProvinceBuilding { get; }

	public CLuaClass Construction { get; }

	public CLuaClass MilitaryConstruction { get; }

	public CLuaClass BrigadeConstruction { get; }

	public CLuaClass ArrayInt { get; }

	public CLuaClass ArrayFix { get; }

	public CLuaClass ArrayCountryTag { get; }

	public CLuaClass War { get; }

	/// <summary>The foreign minister and what its decisions are asked with (docs/mechanics/foreignminister.md).</summary>
	public CLuaClass ForeignMinister { get; }

	public CLuaClass Decision { get; }

	public CLuaClass EventScope { get; }

	public CLuaClass Flags { get; }

	/// <summary>The diplomatic actions the foreign minister's scripts make, one class a kind.</summary>
	public CLuaClass[] DiplomaticActions { get; }

	/// <summary>The commands the two scripts make, and what they are made of.</summary>
	public IReadOnlyList<CLuaClass> ProductionCommands { get; }

	/// <summary>Espionage (docs/mechanics/espionage.md, section 15).</summary>
	public CLuaClass SpyPresence { get; }

	public CLuaClass IdeologyData { get; }

	public CLuaClass IntelligenceMinister { get; }

	public CLuaClass ChangeSpyPriority { get; }

	public CLuaClass ChangeSpyMission { get; }

	/// <summary>Politics (docs/mechanics/politics.md, section 12).</summary>
	public CLuaClass Law { get; }

	public CLuaClass LawGroup { get; }

	public CLuaClass LawDataBase { get; }

	public CLuaClass Minister { get; }

	public CLuaClass MinisterType { get; }

	public CLuaClass GovernmentPosition { get; }

	public CLuaClass PositionDataBase { get; }

	public CLuaClass PoliticsMinister { get; }

	public CLuaClass ChangeLaw { get; }

	public CLuaClass ChangeMinister { get; }

	public CLuaClass ToggleMobilization { get; }

	public CLuaClass Liberate { get; }

	public CLuaClass CreateVassal { get; }

	public CLuaClass SubUnit { get; }

	public CLuaClass SubUnitDataBase { get; }

	public CLuaClass LicenceAction { get; }

	public CLuaClass TradeAction { get; }

	public CLuaClass TradeRoute { get; }

	public CLuaClass GoodsValues { get; }

	public CLuaClass Modifier { get; }

	public CLuaClass TechnologyStatus { get; }

	public CLuaClass Country { get; }

	public CLuaClass CountryTag { get; }

	public CLuaClass DiplomacyStatus { get; }

	public CLuaClass Government { get; }

	public CLuaClass Faction { get; }

	public CLuaClass Ideology { get; }

	public CLuaClass IdeologyGroup { get; }

	public CLuaClass Province { get; }

	public CLuaClass Continent { get; }

	public CLuaClass GoodsPool { get; }

	public CLuaClass Strategy { get; }

	public CLuaClass Ai { get; }

	public CLuaClass ResourceValues { get; }

	public CLuaClass GameState { get; }

	public CLuaClass CountryDataBase { get; }

	public CLuaClass Intel { get; }

	public CLuaClass FixedPoint { get; }

	public CLuaClass Text { get; }

	public CLuaClass Date { get; }

	public IEnumerable<CLuaClass> Classes => new[]
	{
		Country, CountryTag, DiplomacyStatus, Government, Faction, Ideology, IdeologyGroup, Province, Continent,
		GoodsPool, Strategy, Ai, ResourceValues, GameState, CountryDataBase, Intel, FixedPoint, Text, Date,
		TradeAction, TradeRoute, GoodsValues, Modifier, TechnologyStatus, SubUnit, SubUnitDataBase, LicenceAction,
		Law, LawGroup, LawDataBase, Minister, MinisterType, GovernmentPosition, PositionDataBase, PoliticsMinister,
		ChangeLaw, ChangeMinister, ToggleMobilization, Liberate, CreateVassal, SpyPresence, IdeologyData, IntelligenceMinister,
		ChangeSpyPriority, ChangeSpyMission, TechMinister, ProductionMinister, Technology, TechnologyFolder,
		TechnologyDataBase, Building, BuildingDataBase, ProvinceBuilding, Construction, MilitaryConstruction,
		BrigadeConstruction, ArrayInt, ArrayFix, ArrayCountryTag, War, ForeignMinister, Decision, EventScope, Flags,
	}.Concat(ProductionCommands).Concat(DiplomaticActions).Concat(CEnumClasses.Define());

	/// <summary>What has been reported as not built, in the order it was first met.</summary>
	public IReadOnlyCollection<string> Unbuilt => _unbuilt;

	/// <summary>The game being asked.</summary>
	public CGameState State => _state();

	/// <summary>The Lua these classes are handed to, which an offer's score is asked of from inside a script.</summary>
	public Sim.Ai.IAiScripts? Scripts { get; set; }

	/// <summary>Where a script's commands go while it runs on a worker, to reach the outbox afterwards; else null.</summary>
	public List<Sim.Commands.CCommand>? Posted { get; set; }

	/// <summary>
	/// What a script's draws are made on while it runs on a worker - its own, so that its draws are the same
	/// whichever runs first - else the game's.
	/// </summary>
	public CRandom? WorkerRandom { get; set; }

	/// <summary>The generator a script draws on.</summary>
	public CRandom Random => WorkerRandom ?? State.AiRandom;

	/// <summary>
	/// The country whose AI the running script works for - the outermost call's, as the original's draws belong to the
	/// country whose pass runs on the thread - or null outside one. Set by <see cref="CAiScripts"/>.
	/// </summary>
	public Sim.Countries.CCountryTag? Drawer { get; set; }

	/// <summary>One draw a script makes: on the worker's generator, else on the AI's, made for <see cref="Drawer"/>.</summary>
	public int Draw()
	{
		return WorkerRandom?.Next() ?? State.AiRandom.Next(Drawer ?? default);
	}

	/// <summary>A command a script gives, which reaches the game's outbox.</summary>
	public void Post(Sim.Commands.CCommand command)
	{
		(Posted ?? State.Outbox).Add(command);
	}

	/// <summary>Says once a session that a script read something the simulation does not hold.</summary>
	public void ReportUnbuilt(string what)
	{
		if (_unbuilt.Add(what))
		{
			_report(UnbuiltPrefix + what);
		}
	}

	/// <summary>A country, as a script is handed one.</summary>
	public CLuaObject Of(CCountry country) => new(Country, country);

	/// <summary>The country of a tag, or null for the null tag and a tag nobody has.</summary>
	public CLuaObject? OfTag(CCountryTag tag) => State.Country(tag) is { } country ? Of(country) : null;

	/// <summary>A tag, as a script is handed one: a fresh copy each time, as the original pushes it.</summary>
	public CLuaObject Tag(CCountryTag? tag) => new(CountryTag, tag ?? NullTag);

	/// <summary>A figure in thousandths, as a <c>CFixedPoint</c>.</summary>
	public CLuaObject Fixed(CFixedPoint value) => new(FixedPoint, value);

	/// <summary>A text, as a <c>CString</c>.</summary>
	public CLuaObject String(string text) => new(Text, new CLuaText(text));

	public CLuaObject GovernmentNamed(string name) => new(Government, Named(_governments, name));

	/// <summary>A faction by its tag - <c>axis</c> - the empty one being the null faction.</summary>
	public CLuaObject FactionNamed(string name) => new(Faction, Named(_factions, name));

	public CLuaObject IdeologyNamed(string name) => new(Ideology, Named(_ideologies, name));

	public CLuaObject ContinentNamed(string name) => new(Continent, Named(_continents, name));

	/// <summary>The country named by a tag argument.</summary>
	public CCountry? CountryOf(CLuaCall call, int argument) => State.Country(call.Object<CCountryTag>(argument, "CCountryTag"));

	private static CLuaNamed Named(Dictionary<string, CLuaNamed> known, string name)
	{
		if (!known.TryGetValue(name, out CLuaNamed? named))
		{
			known[name] = named = new CLuaNamed(name);
		}
		return named;
	}
}
