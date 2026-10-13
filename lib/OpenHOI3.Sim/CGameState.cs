using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim;

/// <summary>
/// One game: the date, every province and every country.
///
/// A game is an object, not the process. Nothing here is static, so the client can
/// build one, throw it away and build another - which is what "new game", "load" and
/// "exit to menu" each come down to.
/// </summary>
public sealed class CGameState
{
	private readonly Dictionary<string, CCountry> _countries =
		new(StringComparer.OrdinalIgnoreCase);

	private readonly List<CCountry> _countriesInOrder = new();

	private readonly CHandChanges _handChanges = new();

	/// <param name="provinces">Indexed by id, so entry 0 is the "no province" entry.</param>
	private readonly Dictionary<string, CModifier> _staticModifiers =
		new(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, CTechnology> _technologies =
		new(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, CModifier> _strategicResources =
		new(StringComparer.OrdinalIgnoreCase);

	public const int HoursPerDay = 24;

	/// <summary>
	/// The one clock: hours since midnight on 1 January of year 0. The date and the
	/// hour of the day are both read off it. See <c>docs/mechanics/tick.md</c>, section 1.
	/// </summary>
	private long _hours;

	public CGameState(CDate date, IReadOnlyList<CProvince> provinces, IEnumerable<CCountry> countries,
		IReadOnlyList<CBuilding>? buildings = null, IEnumerable<CTechnology>? technologies = null,
		IEnumerable<CModifier>? staticModifiers = null, CDefines? defines = null, IEnumerable<CWar>? wars = null,
		IEnumerable<CModifier>? strategicResources = null,
		IEnumerable<CTechnologyCategory>? technologyCategories = null)
	{
		TechnologyCategories = new List<CTechnologyCategory>(technologyCategories ?? Array.Empty<CTechnologyCategory>());
		Defines = defines ?? new CDefines();
		Wars = new List<CWar>(wars ?? Array.Empty<CWar>());
		foreach (CModifier resource in strategicResources ?? Array.Empty<CModifier>())
		{
			_strategicResources.TryAdd(resource.Name, resource);
		}
		foreach (CModifier modifier in staticModifiers ?? Array.Empty<CModifier>())
		{
			_staticModifiers.TryAdd(modifier.Name, modifier);
		}
		foreach (CTechnology technology in technologies ?? Array.Empty<CTechnology>())
		{
			_technologies.TryAdd(technology.Name, technology);
		}
		_hours = (long)date.DayNumber * HoursPerDay;
		Provinces = provinces;
		foreach (CProvince province in provinces)
		{
			province.CountChangesIn(_handChanges);
		}
		Buildings = buildings ?? Array.Empty<CBuilding>();
		foreach (CCountry country in countries)
		{
			if (_countries.TryAdd(country.Tag.Value, country))
			{
				_countriesInOrder.Add(country);
			}
		}
		// Every country holds a record towards every country, itself included.
		for (int index = 0; index < _countriesInOrder.Count; index++)
		{
			CCountry country = _countriesInOrder[index];
			country.Index = index;
			country.Diplomacy = new CDiplomacyStatus[_countriesInOrder.Count];
			for (int other = 0; other < _countriesInOrder.Count; other++)
			{
				country.Diplomacy[other] = new CDiplomacyStatus(country.Tag, _countriesInOrder[other].Tag);
			}
		}
	}

	/// <summary>
	/// The install's Lua the game asks - the relations a new game is given, and later the AI's
	/// opinions - or null where none is loaded, which is reported where it is asked. See
	/// <c>docs/mechanics/lua.md</c>.
	/// </summary>
	public IAiScripts? AiScripts { get; set; }

	/// <summary>Where the tick writes how long each of its steps took, or null, as it is but for tools.</summary>
	public CTickProfile? Profile { get; set; }

	/// <summary>What <paramref name="holder"/> holds towards <paramref name="other"/>.</summary>
	public static CDiplomacyStatus Status(CCountry holder, CCountry other)
	{
		return holder.Diplomacy[other.Index];
	}

	public long TotalHours => _hours;

	public CDate Date => CDate.FromDayNumber((int)(_hours / HoursPerDay));

	/// <summary>The hour of the day, 0 to 23.</summary>
	public int Hour => (int)(_hours % HoursPerDay);

	/// <summary>Moves the clock on by one hour. Only the hourly tick calls this.</summary>
	public void AdvanceOneHour()
	{
		_hours++;
	}

	public IReadOnlyList<CProvince> Provinces { get; }

	/// <summary>The kinds of building there are, in the order the file declares them.</summary>
	public IReadOnlyList<CBuilding> Buildings { get; }

	/// <summary>Every country, in the order they were declared. Some rules depend on that order.</summary>
	public IReadOnlyList<CCountry> Countries => _countriesInOrder;

	public IReadOnlyDictionary<string, CTechnology> Technologies => _technologies;

	/// <summary>The theories and practicals there are, in the order the file lists them.</summary>
	public IReadOnlyList<CTechnologyCategory> TechnologyCategories { get; }

	/// <summary>The folders technologies are sorted into for the player, in the order the file lists them.</summary>
	public IReadOnlyList<string> TechnologyFolders { get; init; } = Array.Empty<string>();

	/// <summary>The kinds of brigade, ship and wing there are, by key.</summary>
	public IReadOnlyDictionary<string, CSubUnitType> SubUnitTypes { get; init; } =
		new Dictionary<string, CSubUnitType>(StringComparer.OrdinalIgnoreCase);

	/// <summary>The combined arms groups, in the order their file lists them.</summary>
	public IReadOnlyList<CCombinedArmsGroup> CombinedArmsGroups { get; init; } = Array.Empty<CCombinedArmsGroup>();

	/// <summary>The kinds of terrain, in the order <c>map/terrain.txt</c> lists them, those no province has among them.</summary>
	public IReadOnlyList<CTerrain> Terrains { get; init; } = Array.Empty<CTerrain>();

	/// <summary>
	/// The tactics a side of a land battle can fight by, in the order their file lists
	/// them: the first is number 1.
	/// </summary>
	public IReadOnlyList<CCombatTactic> Tactics { get; init; } = Array.Empty<CCombatTactic>();

	/// <summary>The words a new unit's name is made of once its country's lists run out.</summary>
	public CUnitNameTexts NameTexts { get; init; } = new();

	/// <summary>The traits a leader may have, in the order their file declares them.</summary>
	public IReadOnlyList<CTrait> Traits { get; init; } = Array.Empty<CTrait>();

	/// <summary>The traits battles earn a leader, from <c>common/gainable_traits.txt</c>.</summary>
	public CGainableTraits GainableTraits { get; init; } = CGainableTraits.Empty;

	/// <summary>
	/// Every leader of every country, by the number his file gives him - which is what an
	/// order of battle and a savegame name him by. See <c>docs/mechanics/leaders.md</c>.
	/// </summary>
	public Dictionary<int, CLeader> Leaders { get; } = new();

	/// <summary>
	/// The map's regions, in the order <c>region.txt</c> gives them. Each province holds
	/// the numbers of its own in <see cref="CProvince.Regions"/>.
	/// </summary>
	public IReadOnlyList<CRegion> Regions { get; init; } = Array.Empty<CRegion>();

	/// <summary>
	/// Every event, country and province events alike, in the order their files are read; set
	/// once everything they name is loaded. See <c>docs/mechanics/script.md</c>.
	/// </summary>
	public IReadOnlyList<CEvent> Events { get; set; } = Array.Empty<CEvent>();

	/// <summary>Every decision, both groups, in the order their files are read.</summary>
	public IReadOnlyList<CDecision> Decisions { get; set; } = Array.Empty<CDecision>();

	/// <summary>
	/// Each on_action's events' ids, in its file's order: <c>docs/mechanics/events.md</c>, section 8.
	/// </summary>
	public IReadOnlyDictionary<string, IReadOnlyList<int>> OnActions { get; set; } =
		new Dictionary<string, IReadOnlyList<int>>();

	/// <summary>The last pride of the fleet sunk, anyone's, and the ship that sank it - for an event's text.</summary>
	public Units.CSubUnit? LastPrideSunk { get; set; }

	public Units.CSubUnit? LastPrideSinker { get; set; }

	/// <summary>
	/// The defines' <c>start_date</c> and <c>end_date</c>: the first and last days a date
	/// may be set to - an air mission's, for one. See <c>docs/mechanics/aircombat.md</c>,
	/// section 1.
	/// </summary>
	public CDate StartDate { get; init; }

	public CDate EndDate { get; init; }

	/// <summary>
	/// Every name a unit, a brigade, a ship or a wing has been given, in any country:
	/// a new one is never called what something already is.
	/// </summary>
	public HashSet<string> UnitNamesInUse { get; } = new(StringComparer.Ordinal);

	/// <summary>
	/// The last number given to a unit or a sub unit. The next one made takes the one
	/// after it.
	/// </summary>
	public int LastUnitId { get; set; }

	/// <summary>The last number given to a convoy.</summary>
	public int LastConvoyId { get; set; }

	/// <summary>
	/// A new game is being made and its first hour has not come. A convoy made now is
	/// manned for nothing: see <c>docs/mechanics/convoys.md</c>, section 3.
	/// </summary>
	public bool IsBeingMade { get; set; }

	/// <summary>
	/// How many pixels the map is across. Its east edge is beside its west, so the
	/// straight line between two provinces may run either way round. 0 where no map is given.
	/// </summary>
	public int MapWidth { get; init; }

	/// <summary>How many pixels the map is from south to north. Its middle is the equator.</summary>
	public int MapHeight { get; init; }

	private CNavalDistances? _navalDistances;

	private CUnitPositions? _positions;

	/// <summary>
	/// How far it is by sea between any two ports or sea provinces. Worked out from the
	/// map the first time it is asked, a row at a time, and never saved.
	/// </summary>
	public CNavalDistances NavalDistances => _navalDistances ??= new CNavalDistances(Provinces, MapWidth);

	/// <summary>The numbers out of <c>common/defines.lua</c>.</summary>
	public CDefines Defines { get; }

	/// <summary>The wars being fought now - and every war made since, which stays here when a side empties (<c>war.md</c>, section 1).</summary>
	public List<CWar> Wars { get; }

	/// <summary>Wars finished before the game, the original's <c>previous_war</c>: none is read yet, and a war's name is not reused against them.</summary>
	public List<CWar> FinishedWars { get; } = new();

	/// <summary>
	/// The casus belli types of <c>common/cb_types.txt</c>, by their number: the null type first, then the
	/// file's in its order (<c>war.md</c>, section 2).
	/// </summary>
	public IReadOnlyList<CCasusBelliType> CasusBelliTypes { get; set; } = new[] { CCasusBelliType.Null };

	/// <summary>The alliances standing now, two countries each.</summary>
	public List<CAlliance> Alliances { get; } = new();

	/// <summary>The guarantees standing now.</summary>
	public List<CGuarantee> Guarantees { get; } = new();

	/// <summary>The seat the last country to join a faction was given, counting up from the history's nought.</summary>
	public long LastFactionSeat { get; set; }

	/// <summary>The seat the last country put at the front of a faction was given, counting down from nought.</summary>
	public long FirstFactionSeat { get; set; }

	/// <summary>
	/// Each faction's progress, the victory points its members hold, in thousandths, by the faction's name - the null
	/// faction's under "", which nothing sets back to nought (<c>factions.md</c>, section 7). A 32-bit figure, as the
	/// original's, which wraps.
	/// </summary>
	public Dictionary<string, int> FactionProgress { get; } = new(StringComparer.Ordinal);

	/// <summary>
	/// The victory conditions of <c>common/faction_aims.txt</c>, each faction's in the file's order, the factions in the
	/// file's order (<c>factions.md</c>, section 8).
	/// </summary>
	public List<(string Faction, List<Wars.CVictoryCondition> Conditions)> VictoryConditions { get; set; } = new();

	/// <summary>Whether a faction has met its victory conditions or the end date has passed: the game is finished.</summary>
	public bool GameFinished { get; set; }

	/// <summary>The non-aggression pacts standing now: <c>docs/mechanics/agreements.md</c>.</summary>
	public List<Agreements.CNap> Naps { get; } = new();

	/// <summary>The embargoes standing now.</summary>
	public List<Agreements.CEmbargo> Embargoes { get; } = new();

	/// <summary>The trade routes running now, in the order they were made: <c>docs/mechanics/trade.md</c>.</summary>
	public List<Trade.CTradeRoute> TradeRoutes { get; } = new();

	/// <summary>The influences running now, one country's faction on another country.</summary>
	public List<Agreements.CInfluence> Influences { get; } = new();

	/// <summary>The alignments running now, one country's towards another's faction.</summary>
	public List<Agreements.CAlign> Aligns { get; } = new();

	/// <summary>The map's continents, by name, in the order their file declares them.</summary>
	public IReadOnlyList<string> Continents { get; init; } = Array.Empty<string>();

	/// <summary>The corners of the alignment triangle, in the order their file declares them.</summary>
	public IReadOnlyList<CIdeologyGroup> IdeologyGroups { get; init; } = Array.Empty<CIdeologyGroup>();

	/// <summary>
	/// Which depot every province draws on and how far from it, as last worked out.
	/// <see cref="CSupplyNetworks.Refresh"/> keeps it; null until it has been asked for.
	/// </summary>
	public CSupplyNetwork? SupplyNetwork { get; set; }

	/// <summary>The battles being fought now, in the order they began.</summary>
	public List<CCombat> Combats { get; } = new();

	/// <summary>The battles that ended in the past week. See <c>docs/mechanics/airmap.md</c>, section 2.</summary>
	public CCombatHistory CombatHistory { get; } = new();

	/// <summary>
	/// The countries a person plays. The simulation asks it in one place so far: an
	/// idle fleet of a country nobody plays starts no battle
	/// (<c>docs/mechanics/navalcombat.md</c>, section 1).
	/// </summary>
	public HashSet<CCountryTag> Played { get; } = new();

	/// <summary>
	/// The played countries whose player has ticked "Automate Trade": the trades offered to them are
	/// answered by a roll each hour and not shown, and the install's Lua trades for them as for a country
	/// nobody plays. See <c>docs/mechanics/trade.md</c>, section 12.
	/// </summary>
	public HashSet<CCountryTag> AutomateTrade { get; } = new();

	/// <summary>The game's own flags, which script sets: <c>docs/mechanics/effects.md</c>, section 3.</summary>
	public Script.CFlags Flags { get; } = new();

	/// <summary>
	/// The <c>fire_only_once</c> events that have fired, which fire no more:
	/// <c>docs/mechanics/events.md</c>, section 6.
	/// </summary>
	public HashSet<Script.CEvent> FiredOnce { get; } = new();

	/// <summary>
	/// The events waiting for a person's answer, in the order they were opened:
	/// <c>docs/mechanics/events.md</c>, section 7.
	/// </summary>
	public List<Events.COpenEvent> OpenEvents { get; } = new();

	/// <summary>
	/// What the game itself posts while a command runs - an AI's answer to an event, an event that
	/// answers itself - which the queue takes after that command and runs at its next turn.
	/// </summary>
	public List<Commands.CCommand> Outbox { get; } = new();

	/// <summary>
	/// Raised as an event fires, past its <c>fire_only_once</c> test, for whatever reports it - a
	/// message, a tool. Never for anything that changes the game.
	/// </summary>
	public event Action<Script.CEvent, Script.CEventScope>? EventFired;

	internal void RaiseEventFired(Script.CEvent fired, Script.CEventScope scope)
	{
		EventFired?.Invoke(fired, scope);
	}

	/// <summary>
	/// Raised as an event's option runs, before its effects, with the option's index - for the message
	/// the original raises then (<c>docs/mechanics/messages.md</c>, section 2). Never for anything that
	/// changes the game; which message it is hangs on who is playing on this machine.
	/// </summary>
	public event Action<Script.CEvent, Script.CEventScope, int>? EventAnswered;

	internal void RaiseEventAnswered(Script.CEvent answered, Script.CEventScope scope, int option)
	{
		EventAnswered?.Invoke(answered, scope, option);
	}

	/// <summary>
	/// Raised as a pact is made, refused or ended, for its message (<c>docs/mechanics/agreements.md</c>,
	/// section 8). Never for anything that changes the game.
	/// </summary>
	public event Action<Agreements.CPactMessage>? PactMessaged;

	internal void RaisePactMessage(Agreements.CPactMessage message)
	{
		PactMessaged?.Invoke(message);
	}

	/// <summary>
	/// Raised as an offer made to a played country is put to its player, once, for the icon and the
	/// dialog he answers it in (<c>docs/mechanics/agreements.md</c>, section 10). The answer comes back
	/// as a command. Never for anything that changes the game.
	/// </summary>
	public event Action<Agreements.COffer>? OfferAsked;

	internal void RaiseOfferAsked(Agreements.COffer offer)
	{
		OfferAsked?.Invoke(offer);
	}

	/// <summary>Raised as a decision is taken, before its effects, for its message.</summary>
	public event Action<Script.CDecision, Script.CEventScope>? DecisionTaken;

	internal void RaiseDecisionTaken(Script.CDecision taken, Script.CEventScope scope)
	{
		DecisionTaken?.Invoke(taken, scope);
	}

	/// <summary>
	/// How well each country knows each province it has a unit in and does not hold, where
	/// it knows it at all: by the province's id and the country. The name is from when
	/// only fleets asked; land is in it since air missions do. Written by
	/// <see cref="CNavalIntelligence.Refresh"/> once a day, and read through
	/// <see cref="CIntelligence.Level"/>.
	/// </summary>
	public Dictionary<(int Province, CCountryTag Country), int> SeaIntelligence { get; } = new();

	/// <summary>
	/// How many times a province has changed owner or controller in this game. Whatever shows who
	/// holds what watches it to know when to look again. The provinces count it themselves
	/// (<see cref="CHandChanges"/>), so no writer of an owner or a controller can forget to.
	/// </summary>
	public int ControlChanges => _handChanges.Count;

	/// <summary>What the map says of each country this hour, kept by <see cref="Countries.CCountryFacts"/>.</summary>
	public Countries.CCountryFacts? CountryFacts { get; set; }

	/// <summary>How many provinces each country owns, kept by <see cref="Countries.COwnedCounts"/>.</summary>
	public Countries.COwnedCounts? OwnedCounts { get; set; }

	/// <summary>
	/// What every roll of this game starts from. An hour's rolls depend on it, on the
	/// hour, and on the order they are made in - and on nothing else.
	/// </summary>
	public uint Seed { get; set; } = 1;

	/// <summary>The game's one source of chance, seeded afresh every hour.</summary>
	public CRandom Random { get; } = new();

	/// <summary>
	/// Who stands where. Made when first asked for and thrown away at the start of each
	/// hour and whenever a unit is put somewhere outside the march.
	/// </summary>
	public CUnitPositions Positions => _positions ??= new CUnitPositions(this);

	/// <summary>The units in a province, in the order they came.</summary>
	public IReadOnlyList<CUnit> UnitsIn(int province)
	{
		return Positions.Of(province);
	}

	/// <summary>To be called when a unit has been put, made or removed other than by its march.</summary>
	public void ForgetPositions()
	{
		_positions = null;
	}

	/// <summary>Seeds the hour's rolls: the game's seed and the hour it is.</summary>
	public void SeedHour()
	{
		unchecked
		{
			Random.Seed(Seed * 2654435761u + (uint)_hours * 40503u + (uint)(_hours >> 32));
		}
	}

	public bool IsAtWar(CCountryTag tag)
	{
		foreach (CWar war in Wars)
		{
			if (war.Involves(tag))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// A country's stockpile, the original's <c>GetPool</c>: the pool of the province its government sits in - its
	/// acting capital - and not something the country holds, so two countries with one seat share it; a government
	/// in exile's own pool. Null for a country with no seat. See <c>docs/mechanics/stockpile.md</c>, section 1, and
	/// <c>war.md</c>, section 14.
	/// </summary>
	public CGoodsPool? Stockpile(CCountry country)
	{
		if (country.InExile)
		{
			return country.PoolInExile;
		}
		CProvince seat = Province(country.ActingCapital);
		return seat.Id == 0 ? null : seat.Pool;
	}

	/// <summary>
	/// The value a static modifier holds for one kind, or zero when the modifier or the
	/// kind is not there. The engine knows these modifiers by name.
	/// </summary>
	public CFixedPoint StaticModifier(string name, string kind)
	{
		return _staticModifiers.TryGetValue(name, out CModifier? modifier) ? modifier[kind] : CFixedPoint.Zero;
	}

	/// <summary>A static modifier by name, whole, or null when there is none of that name.</summary>
	public CModifier? FindStaticModifier(string name)
	{
		return _staticModifiers.TryGetValue(name, out CModifier? modifier) ? modifier : null;
	}

	/// <summary>What a strategic resource is worth to whoever has it, or null when none has that name.</summary>
	public CModifier? FindStrategicResource(string name)
	{
		return _strategicResources.TryGetValue(name, out CModifier? resource) ? resource : null;
	}

	/// <summary>The province with that id, or the "no province" entry when it is out of range.</summary>
	public CProvince Province(int id)
	{
		return id > 0 && id < Provinces.Count ? Provinces[id] : Provinces[0];
	}

	public CCountry? Country(CCountryTag? tag)
	{
		return tag is { } known && _countries.TryGetValue(known.Value, out CCountry? country)
			? country
			: null;
	}
}
