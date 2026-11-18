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
	/// <summary>
	/// The static and event modifiers by name, matched case and all as the original's database matches a name on its bytes:
	/// BlackICE's event modifier <c>VERY_EASY_PLAYER</c> is not the static <c>very_easy_player</c> (<c>effects.md</c>, section 6).
	/// </summary>
	private readonly Dictionary<string, CModifier> _staticModifiers =
		new(StringComparer.Ordinal);

	private readonly Dictionary<string, CTechnology> _technologies =
		new(StringComparer.OrdinalIgnoreCase);

	private readonly List<CTechnology> _technologiesInOrder = new();

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
			if (_technologies.TryAdd(technology.Name, technology))
			{
				_technologiesInOrder.Add(technology);
			}
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
			country.Espionage = new Espionage.CSpyPresence[_countriesInOrder.Count];
			for (int other = 0; other < _countriesInOrder.Count; other++)
			{
				country.Diplomacy[other] = new CDiplomacyStatus(country.Tag, _countriesInOrder[other].Tag);
				country.Espionage[other] = new Espionage.CSpyPresence();
			}
			// And its own presence, where its counter-espionage is, starts at priority 1 with counter-espionage at 1
			// (espionage.md, section 2).
			Espionage.CSpyPresence home = country.Espionage[index];
			home.Priority = 1;
			home.SetMission((int)Espionage.ESpyMission.CounterEspionage, 1);
		}
		// What each country knows of each province (intelligence.md, section 2).
		foreach (CProvince province in provinces)
		{
			province.Intel = new byte[_countriesInOrder.Count];
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

	/// <summary>What <paramref name="holder"/> has in <paramref name="other"/> of spies (<c>espionage.md</c>, section 1).</summary>
	public static Espionage.CSpyPresence Presence(CCountry holder, CCountry other)
	{
		return holder.Espionage[other.Index];
	}

	/// <summary>The covert operations of <c>common/covert_ops.txt</c>, in its order (<c>espionage.md</c>, section 11).</summary>
	public List<Espionage.CCovertOperation> CovertOperations { get; } = new();

	/// <summary>The provinces countries have asked their allies to take (<c>provinceview.md</c>, section 6).</summary>
	public Ai.CAlliedObjectives AlliedObjectives { get; } = new();

	/// <summary>The triggered modifiers of <c>common/triggered_modifiers.txt</c>, in its order (<c>modifiers.md</c>, section 5).</summary>
	public List<CTriggeredModifier> TriggeredModifiers { get; } = new();

	public long TotalHours => _hours;

	public CDate Date => CDate.FromDayNumber((int)(_hours / HoursPerDay));

	/// <summary>The hour of the day, 0 to 23.</summary>
	public int Hour => (int)(_hours % HoursPerDay);

	/// <summary>Sets the clock to a save's hour: only a loaded game, before it runs (<c>docs/mechanics/savegame.md</c>).</summary>
	public void SetClock(long totalHours)
	{
		_hours = totalHours;
	}

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

	/// <summary>The technologies in the order they were read, which is the original's database order (research.md, section 9).</summary>
	public IReadOnlyList<CTechnology> TechnologiesInOrder => _technologiesInOrder;

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
	/// The regions <c>map/coastlines.txt</c> names, as indices into <see cref="Regions"/>: two countries whose naval
	/// bases both border a sea province of one of them may share their strategic resources across continents. See
	/// <c>docs/mechanics/modifiers.md</c>, section 6.
	/// </summary>
	public IReadOnlyList<int> CoastlineRegions { get; init; } = Array.Empty<int>();

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

	/// <summary>What the winner and the loser of the last land, air and sea battle decided lost, whoever fought it: triggers.md, section 5.2.</summary>
	public Combat.CLastBattleLosses LastBattleLosses { get; } = new();

	/// <summary>
	/// The defines' <c>start_date</c> and <c>end_date</c>: the first and last days a date
	/// may be set to - an air mission's, for one. See <c>docs/mechanics/aircombat.md</c>,
	/// section 1.
	/// </summary>
	public CDate StartDate { get; init; }

	public CDate EndDate { get; init; }

	/// <summary>The end date in hours, what a lent unit with no recall under way is saved with.</summary>
	public long EndDateHours => (long)EndDate.DayNumber * HoursPerDay;

	/// <summary>
	/// The last number given to a unit or a sub unit. The next one made takes the one
	/// after it.
	/// </summary>
	public int LastUnitId { get; set; }

	/// <summary>
	/// The first number not made by the history: what a new game's order of battle and queue gave are below it, and
	/// what was made in play from it on - the original's <c>unit</c> counter, which a save writes each number by. See
	/// <c>docs/mechanics/savegame.md</c>, section 5.
	/// </summary>
	public int HistoryUnitNext { get; set; } = 1;

	/// <summary>
	/// The original's own number of a unit, brigade or line - the serial of its save pair, which is what the original seeds
	/// a draw on a unit with: below <see cref="HistoryUnitNext"/> the number itself, the history's; from it on, minted in
	/// play, counted from one at it (<c>docs/mechanics/savegame.md</c>, section 5; <c>docs/mechanics/moving.md</c>, section 2).
	/// </summary>
	public int OriginalSerial(int id)
	{
		return id <= 0 ? 0 : id < HistoryUnitNext ? id : id - HistoryUnitNext + 1;
	}

	/// <summary>The last number given to a convoy.</summary>
	public int LastConvoyId { get; set; }

	/// <summary>The number the next theatre made takes: the original's <c>theatre</c>, from 1 in a new game.</summary>
	public int NextTheatreId { get; set; } = 1;

	/// <summary>Every country's theatres by their numbers (<c>docs/mechanics/theatres.md</c>).</summary>
	public Dictionary<int, Theatres.CTheatre> Theatres { get; } = new();

	/// <summary>
	/// Where the theatre layer tells each step of one country's making - which country, and the line - for a tool that wants
	/// to follow it. Null in a game. Changes nothing.
	/// </summary>
	public (CCountryTag Country, Action<string> Tell)? TheatreTrace { get; set; }

	/// <summary>
	/// A new game is being made and its first hour has not come. A convoy made now is
	/// manned for nothing: see <c>docs/mechanics/convoys.md</c>, section 3.
	/// </summary>
	public bool IsBeingMade { get; set; }

	/// <summary>This game came from a save: its AI's first passes are no first passes (<c>docs/mechanics/savegame.md</c>, section 2).</summary>
	public bool LoadedFromSave { get; set; }

	/// <summary>
	/// A game has been made and entered, and is running: the original's <c>in_game</c>. What only a running game does - an
	/// AI strategy made again every ten days and at a change of war (<c>aistrategy.md</c>, section 1) - waits for it.
	/// </summary>
	public bool IsRunning { get; set; }

	/// <summary>
	/// How many pixels the map is across. Its east edge is beside its west, so the
	/// straight line between two provinces may run either way round. 0 where no map is given.
	/// </summary>
	public int MapWidth { get; init; }

	/// <summary>How many pixels the map is from south to north. Its middle is the equator.</summary>
	public int MapHeight { get; init; }

	private CNavalDistances? _navalDistances;

	private CUnitPositions? _positions;

	/// <summary>The table last thrown away: the order the next one keeps its units in.</summary>
	private CUnitPositions? _lastPositions;

	/// <summary>
	/// How far it is by sea between any two ports or sea provinces. Worked out from the
	/// map the first time it is asked, a row at a time, and never saved.
	/// </summary>
	public CNavalDistances NavalDistances => _navalDistances ??= new CNavalDistances(Provinces, MapWidth);

	/// <summary>The numbers out of <c>common/defines.lua</c>.</summary>
	public CDefines Defines { get; }

	/// <summary>The wars being fought now - and every war made since, which stays here when a side empties (<c>war.md</c>, section 1).</summary>
	public List<CWar> Wars { get; }

	/// <summary>
	/// The original's <c>previous_war</c>s: a new game's are the history's wars not fought on the start date that began
	/// by it (<c>savegame.md</c>, section 10), a loaded game's the save's. A war's name is not reused against them.
	/// </summary>
	public List<CWar> FinishedWars { get; } = new();

	/// <summary>
	/// The fog of war, on unless the original console's <c>debug fow</c> - here the debug menu - turns it off: with it off the
	/// countries played know every province at 9, the AI's left as they are (<c>intelligence.md</c>, section 3;
	/// <c>compatibility.md</c>, entry 21). Not saved, as the original's is not.
	/// </summary>
	public bool FogOfWar { get; set; } = true;

	/// <summary>The console's <c>instantresearch</c>: a level is done whatever its progress.</summary>
	public bool InstantResearch { get; set; }

	/// <summary>The console's <c>instantconstruction</c>: a line the day reaches is done whatever its progress.</summary>
	public bool InstantConstruction { get; set; }

	/// <summary>The console's <c>alwayspartisants</c>: an underground raises partisans at any strength.</summary>
	public bool IgnorePartisanSpawnLimit { get; set; }

	/// <summary>The console's <c>debug nopopup</c>: no message is told at all, and the player's events are answered as the AI's.</summary>
	public bool NoPopups { get; set; }

	/// <summary>The console's <c>debug spy</c>: the intelligence screen shows another country's true figures.</summary>
	public bool SpyCheat { get; set; }

	/// <summary>The console's <c>debug yesmen</c>: an AI answers 100 to whatever a played country offers it.</summary>
	public bool AiYesmen { get; set; }

	/// <summary>Lets go of whatever a screen had listening, when another session takes the game over.</summary>
	public void ForgetListeners()
	{
		EventFired = null;
		EventAnswered = null;
		PactMessaged = null;
		OfferAsked = null;
		CombatBegan = null;
		ConvoyRaidMessaged = null;
		PatrolTracked = null;
		DecisionTaken = null;
		SoundCued = null;
		UnitArrived = null;
	}

	/// <summary>
	/// How many pacts have been made - alliances, guarantees, puppets, NAPs, embargoes, influences, alignments and trade
	/// routes, counted as one: each pact holds its number, which is the order a save writes them in, the original keeping
	/// them on one list (<c>savegame.md</c>, section 10).
	/// </summary>
	public long PactsMade { get; set; }

	/// <summary>The number of a pact made now.</summary>
	public long NextPact() => ++PactsMade;

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

	/// <summary>The factions whose conditions the lobby has chosen, which a new game's own choice leaves alone.</summary>
	public HashSet<string> VictoryChoicesMade { get; } = new();

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
	public IReadOnlyList<CIdeologyGroup> IdeologyGroups
	{
		get => _ideologyGroups;
		init
		{
			_ideologyGroups = value;
			_ideologies = null;
		}
	}

	private IReadOnlyList<CIdeologyGroup> _ideologyGroups = Array.Empty<CIdeologyGroup>();
	private List<Politics.CIdeology>? _ideologies;
	private Dictionary<string, Politics.CIdeology>? _ideologyByName;

	/// <summary>
	/// The ideologies, number 0 the null ideology and the rest in their file's order across the groups
	/// (<c>docs/mechanics/politics.md</c>, section 1). Made from <see cref="IdeologyGroups"/> when first asked.
	/// </summary>
	public IReadOnlyList<Politics.CIdeology> Ideologies
	{
		get
		{
			if (_ideologies is null)
			{
				var made = new List<Politics.CIdeology> { new("", 0, null, default) };
				foreach (CIdeologyGroup group in _ideologyGroups)
				{
					foreach (string name in group.IdeologyOrder.Count > 0 ? group.IdeologyOrder : (IEnumerable<string>)group.Ideologies)
					{
						made.Add(new Politics.CIdeology(name, made.Count, group, group.IdeologyColours.GetValueOrDefault(name)));
					}
				}
				_ideologyByName = new Dictionary<string, Politics.CIdeology>(StringComparer.OrdinalIgnoreCase);
				foreach (Politics.CIdeology ideology in made)
				{
					_ideologyByName.TryAdd(ideology.Name, ideology);
				}
				_ideologies = made;
			}
			return _ideologies;
		}
	}

	/// <summary>The ideology of that name, or null for an empty name or one no group has.</summary>
	public Politics.CIdeology? Ideology(string? name)
	{
		_ = Ideologies;
		return string.IsNullOrEmpty(name) ? null : _ideologyByName!.GetValueOrDefault(name);
	}

	/// <summary>The forms of government, by name (<c>common/governments.txt</c>).</summary>
	public Dictionary<string, Politics.CGovernment> Governments { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The cabinet posts in their file's order; each one's number is its place here plus 1.</summary>
	public IReadOnlyList<Politics.CGovernmentPosition> GovernmentPositions { get; init; } =
		Array.Empty<Politics.CGovernmentPosition>();

	/// <summary>
	/// What a minister can be, in <c>common/minister_types.txt</c>'s order - the objects the ministers hold. The original's
	/// database puts its null type, <c>noMinisterType</c>, before them, which is not here (<c>lua.md</c>, section 4).
	/// </summary>
	public IReadOnlyList<CMinisterType> MinisterTypes { get; init; } = Array.Empty<CMinisterType>();

	/// <summary>The law groups of <c>common/laws.txt</c> in its order, each with its laws (<c>politics.md</c>, section 9).</summary>
	public IReadOnlyList<CLawGroup> LawGroups { get; init; } = Array.Empty<CLawGroup>();

	/// <summary>
	/// The occupation policies: <c>no_occupation</c> first, then those of <c>common/occupation_policies.txt</c> in its
	/// order, each at its number (<c>occupation.md</c>, section 1).
	/// </summary>
	public IReadOnlyList<Occupation.COccupationPolicy> OccupationPolicies { get; set; } = Occupation.COccupationPolicies.WithNull();

	/// <summary>The rebel types of <c>common/rebel_types.txt</c>, in its order (<c>rebels.md</c>, section 2).</summary>
	public IReadOnlyList<Revolts.CRebelType> RebelTypes { get; set; } = Array.Empty<Revolts.CRebelType>();

	/// <summary>The rebel factions, in the order they were made (<c>rebels.md</c>, section 6).</summary>
	public List<Revolts.CRebelFaction> RebelFactions { get; } = new();

	/// <summary>The last number given to a rebel faction; the next takes the one after it.</summary>
	public int LastRebelId { get; set; }

	/// <summary>The law group of that name, or null.</summary>
	public CLawGroup? LawGroup(string name)
	{
		foreach (CLawGroup group in LawGroups)
		{
			if (group.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return group;
			}
		}
		return null;
	}

	/// <summary>A country's government, or null where it has none the file declares.</summary>
	public Politics.CGovernment? GovernmentOf(CCountry country)
	{
		return Governments.GetValueOrDefault(country.GovernmentType);
	}

	/// <summary>
	/// Which depot every province draws on and how far from it, as last worked out.
	/// <see cref="CSupplyNetworks.Refresh"/> keeps it; null until it has been asked for.
	/// </summary>
	public CSupplyNetwork? SupplyNetwork { get; set; }

	/// <summary>The battles being fought now, in the order they began.</summary>
	public List<CCombat> Combats { get; } = new();

	/// <summary>The battles that ended in the past week. See <c>docs/mechanics/airmap.md</c>, section 2.</summary>
	public CCombatHistory CombatHistory { get; } = new();

	/// <summary>Every ship that has left the game, in the order they went. See <see cref="CSunkShips"/>.</summary>
	public List<CSunkShip> SunkShips { get; } = new();

	/// <summary>
	/// The numbers of the units to be taken out of the game at the very end of the hour, in the order they were queued -
	/// the original's <c>pending_unit_removals</c>. Empty between hours but where a command run between them queued one.
	/// Not saved, as the original's is not. See <see cref="CPendingUnitRemovals"/>.
	/// </summary>
	public List<int> PendingUnitRemovals { get; } = new();

	/// <summary>
	/// <c>nation_size_statistics</c>: each country's provinces owned as each year began, a line a country, kept for the save
	/// alone (<see cref="Countries.CNationSize"/>).
	/// </summary>
	public List<Countries.CStatisticsSeries> NationSizeStatistics { get; } = new();

	/// <summary>What the played country's wars have cost each side, enemy by enemy: the Strategic Warfare Manager's records.</summary>
	public StrategicWarfare.CWarfareLedger Warfare { get; } = new();

	/// <summary>
	/// The landings on each player's ground, by the country making them, waiting to be told
	/// (<c>docs/mechanics/transport.md</c>, section 11).
	/// </summary>
	public List<Transport.CInvasionWarning> InvasionWarnings { get; } = new();

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

	/// <summary>The difficulty and the game mode (<c>docs/mechanics/frontend.md</c>, section 4).</summary>
	public Settings.CGamePlaySettings GamePlay { get; } = new();

	/// <summary>What the player has handed to the AI, which every played country goes by (frontend.md, section 4).</summary>
	public Settings.CPlayerAutomation Automation { get; } = new();

	/// <summary>The wars nobody declared (<c>docs/mechanics/war.md</c>, section 15).</summary>
	public List<Wars.CUndeclaredWar> UndeclaredWars { get; } = new();

	/// <summary>
	/// Reads a file of units into a country as a game runs, for <c>load_oob</c>; set by whoever loaded the game, the
	/// simulation reading no files of its own.
	/// </summary>
	public Action<Countries.CCountry, string>? LoadOrderOfBattle { get; set; }

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
	/// The changes of law the install's Lua posted as a new game was set up, waiting on the queue until the game's two
	/// first days are run; empty at any other time (<c>docs/mechanics/politics.md</c>, section 12).
	/// </summary>
	public List<Commands.CCommand> NewGameLaws { get; } = new();

	/// <summary>
	/// The countries the history's wars mobilised as they were replayed, so whose modifiers were made then and resist the
	/// threat of a country set up before them in a new game; empty once its set-up is over
	/// (<c>docs/mechanics/diplomacy.md</c>, section 5.3).
	/// </summary>
	public HashSet<CCountryTag> MobilisedByHistory { get; } = new();

	/// <summary>The battle plans a save held, waiting for the interface that keeps them; nothing simulated reads them.</summary>
	public List<Plans.CBattlePlan> LoadedBattlePlans { get; } = new();

	/// <summary>The interface's battle plans for a save to write, where one has been given (battleplans.md, section 7).</summary>
	public Func<IEnumerable<Plans.CBattlePlan>>? BattlePlansToSave { get; set; }

	/// <summary>
	/// Where every condition and effect tells what of its script it could not answer or do, for a tool that wants to count it -
	/// the headless run. Null in a game, where nobody asks. Changes nothing the events do.
	/// </summary>
	public Script.CScriptReport? ScriptReport { get; set; }

	/// <summary>
	/// The country whose unit AI tells what it decides, and where it tells it, for a tool tracing it. Null in a game.
	/// Changes nothing the AI does.
	/// </summary>
	public (Countries.CCountryTag Country, Action<string> Write)? AiTrace { get; set; }

	/// <summary>
	/// Told of every unit taken out of the game, as it goes, for a tool tracing what a country loses. Null in a game.
	/// Changes nothing.
	/// </summary>
	public Action<Units.CUnit>? UnitRemovedTrace { get; set; }

	/// <summary>Told of each battle's hour and each unit's firing, for a tool setting them beside the original's. Null in a game.</summary>
	public Action<string>? CombatTrace { get; set; }

	/// <summary>Run as a battle's hour begins, for a tool that puts the generator where the original's stood. Null in a game.</summary>
	public Action<Combat.CCombat>? BeforeBattle { get; set; }

	/// <summary>
	/// The AI recorded as it runs, or played back in its place (<c>docs/replay.md</c>). Null in a game, where the AI simply
	/// runs.
	/// </summary>
	public Replay.CReplay? Replay { get; set; }

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

	/// <summary>Raised as a battle or a bombing first runs, for its message. Never for anything that changes the game.</summary>
	public event Action<Combat.CCombat>? CombatBegan;

	internal void RaiseCombatBegan(Combat.CCombat combat)
	{
		if (!combat.IsConvoyBattle)
		{
			CombatBegan?.Invoke(combat);
		}
	}

	/// <summary>
	/// Raised as a raid on a convoy tells a country's player something - ships lost, ships sunk, a convoy too strong
	/// (<c>docs/mechanics/navalmissions.md</c>, section 8.2). Never for anything that changes the game.
	/// </summary>
	public event Action<Convoys.CConvoyRaidMessage>? ConvoyRaidMessaged;

	internal void RaiseConvoyRaidMessage(Convoys.CConvoyRaidMessage message)
	{
		ConvoyRaidMessaged?.Invoke(message);
	}

	/// <summary>
	/// Raised as a patrol takes up a fleet it did not shadow the hour before, for whoever plays the patrol's country to be
	/// told (<c>docs/mechanics/navalmissions.md</c>, section 3.1). Never for anything that changes the game.
	/// </summary>
	public event Action<Missions.CPatrolTrackMessage>? PatrolTracked;

	internal void RaisePatrolTracked(Missions.CPatrolTrackMessage message)
	{
		PatrolTracked?.Invoke(message);
	}

	/// <summary>
	/// Raised where the original plays a sound for one country's player and no message carries it: a convoy's ships lost
	/// and a raider's hit, a level finished, a war declared and declared on, a revolt in a province held
	/// (<c>docs/mechanics/sound.md</c>, section 7). With the sound's name and the country whose player hears it; the client
	/// plays it where that is its player. Never for anything that changes the game.
	/// </summary>
	public event Action<string, CCountryTag>? SoundCued;

	internal void RaiseSound(string sound, CCountryTag heardBy)
	{
		SoundCued?.Invoke(sound, heardBy);
	}

	/// <summary>
	/// Raised as a played country's unit arrives at the end of its way, for its message
	/// (<see cref="Movement.CArrival"/>; <c>docs/mechanics/movement.md</c>, section 6). Never for anything that changes the game.
	/// </summary>
	public event Action<Units.CUnit>? UnitArrived;

	internal void RaiseUnitArrived(Units.CUnit unit)
	{
		UnitArrived?.Invoke(unit);
	}

	/// <summary>Raised as a decision is taken, before its effects, for its message.</summary>
	public event Action<Script.CDecision, Script.CEventScope>? DecisionTaken;

	internal void RaiseDecisionTaken(Script.CDecision taken, Script.CEventScope scope)
	{
		DecisionTaken?.Invoke(taken, scope);
	}

	/// <summary>
	/// The provinces asked for this hour, which <see cref="Intelligence.CProvinceIntel"/> raises at its end
	/// (<c>docs/mechanics/intelligence.md</c>, section 5).
	/// </summary>
	public List<int> IntelRequests { get; } = new();

	/// <summary>
	/// What the hour's end compares against the last hour's to see whether the whole of what countries know of
	/// provinces must be made again: the changes of hands and the pacts (<c>intelligence.md</c>, section 5).
	/// </summary>
	public long IntelFingerprint { get; set; }

	/// <summary>
	/// Whether something in the hour has asked for the whole of what countries know to be made again at its end, as the
	/// original's request of the whole does: a radar province added or taken off, or a province's <c>radar_level</c> moved
	/// in its rebuild (<c>intelligence.md</c>, section 5). It lasts the hour.
	/// </summary>
	public bool IntelWholeAsked { get; set; }

	/// <summary>
	/// How many times a province has changed owner or controller in this game. Whatever shows who
	/// holds what watches it to know when to look again. The provinces count it themselves
	/// (<see cref="CHandChanges"/>), so no writer of an owner or a controller can forget to.
	/// </summary>
	public int ControlChanges => _handChanges.Count;

	private readonly Dictionary<CCountryTag, int> _controlledCounts = new();
	private int _controlledCountsAt = -1;

	/// <summary>
	/// How many provinces a country controls: counted over the map again only when a province has changed hands since
	/// it was last asked. The agents ask it many times an hour, through the surrender progress.
	/// </summary>
	public int ControlledCount(CCountryTag tag)
	{
		if (_controlledCountsAt != ControlChanges)
		{
			_controlledCounts.Clear();
			foreach (CProvince province in Provinces)
			{
				if (province.Controller is { } controller)
				{
					_controlledCounts[controller] = _controlledCounts.GetValueOrDefault(controller) + 1;
				}
			}
			_controlledCountsAt = ControlChanges;
		}
		return _controlledCounts.GetValueOrDefault(tag);
	}

	/// <summary>What the map says of each country this hour, kept by <see cref="Countries.CCountryFacts"/>.</summary>
	public Countries.CCountryFacts? CountryFacts { get; set; }

	/// <summary>
	/// The unit types in the order the AI's plans count them, made once by <see cref="Agents.CAircraftWants.Order"/>:
	/// <see cref="SubUnitTypes"/> never changes in a game. Derived, and never saved.
	/// </summary>
	internal List<Units.CSubUnitType>? PlanTypeOrder { get; set; }

	/// <summary>Every continent a province is on, gathered once by the <c>continent</c> condition. Derived, and never saved.</summary>
	internal HashSet<string>? ContinentNames { get; set; }

	/// <summary>Each unit type's group in the AI's plans, kept by <see cref="Agents.CPlanWants.GroupOf"/>. Derived, and never saved.</summary>
	internal Dictionary<Units.CSubUnitType, Units.CSubUnitType>? PlanGroups { get; set; }

	/// <summary>The route finder's working arrays, kept from one search to the next by <see cref="Movement.CRouteScratch"/>.</summary>
	internal Movement.CRouteScratch? RouteScratch { get; set; }

	/// <summary>Every holder's air bases this hour, kept by <see cref="Agents.CAirBaseHolders"/>.</summary>
	public Agents.CAirBaseHolders? AirBaseHolders { get; set; }

	/// <summary>How many provinces each country owns, kept by <see cref="Countries.COwnedCounts"/>.</summary>
	public Countries.COwnedCounts? OwnedCounts { get; set; }

	/// <summary>The provinces each country controls in its list's order, kept by <see cref="Countries.CControlledLists"/>.</summary>
	public Countries.CControlledLists? ControlledLists { get; set; }

	/// <summary>
	/// What every roll of this game starts from. An hour's rolls depend on it, on the
	/// hour, and on the order they are made in - and on nothing else.
	/// </summary>
	public uint Seed
	{
		get => _seed;
		set
		{
			_seed = value;
			Random.Seed(value);
			AiRandom.Seed(value);
		}
	}

	private uint _seed = 1;

	/// <summary>The game's source of chance: battles, espionage, events, revolts, the weather and the rest.</summary>
	public CRandom Random { get; } = new();

	/// <summary>
	/// The AI's own source of chance, as the original keeps one beside the game's: its trading roll, its answer to an offer,
	/// its ministers' countdowns and decision scopes, the Lua's <c>GetAIRand</c> and <c>CAIIntel</c>, the invasion planner,
	/// the convoy raid and the patrol - so what the AI decides never moves a battle's or an event's rolls
	/// (<c>reversing/findings/FINDINGS-liveread.md</c>, section 59). Seeded with the game's seed, which is ours.
	/// </summary>
	public CRandom AiRandom { get; } = new();

	/// <summary>
	/// Where the AI's estimate of a battle takes the half of a unit's modifier that hangs on the unit alone - its attacking
	/// half, or with false its defending half - from instead of working it out, or null to work it out; in thousandths. The
	/// original never initialises the cache it keeps that half in, so after a load a unit's first looks take what memory held
	/// (<c>docs/mechanics/moving.md</c>, section 11.4). Nothing in the game sets it: the savegame tool's playback of a run of
	/// the original's hands over that run's, for entering alone (<c>docs/replay.md</c>, section 9). A measuring instrument.
	/// </summary>
	public Func<Units.CUnit, bool, long?>? FirstLookModel { get; set; }

	/// <summary>
	/// Where the AI's naval patrol takes the C runtime <c>rand()</c> its centre is drawn with, for a country, instead of its
	/// own stream, or null for its own. The original draws on the <c>rand()</c> of whichever thread runs the country's pass,
	/// and the main thread's has run since the process began, so no game state gives it back (<c>docs/mechanics/moving.md</c>,
	/// sections 14 and 19). Nothing in the game sets it: the savegame tool's playback of a run of the original's hands over that run's
	/// (<c>docs/replay.md</c>, section 9). A measuring instrument.
	/// </summary>
	public Func<Countries.CCountryTag, int?>? PatrolRandFeed { get; set; }

	/// <summary>The weather's own state: the pressure zones and the fronts (<c>docs/mechanics/weather.md</c>).</summary>
	public Weather.CWeatherManager Weather { get; } = new();

	/// <summary>
	/// Who stands where. Made when first asked for and thrown away at the start of each
	/// hour and whenever a unit is put somewhere outside the march.
	/// </summary>
	public CUnitPositions Positions => _positions ??= MakePositions();

	private CUnitPositions MakePositions()
	{
		if (Profile is null)
		{
			return new CUnitPositions(this, _lastPositions);
		}
		long start = System.Diagnostics.Stopwatch.GetTimestamp();
		var made = new CUnitPositions(this, _lastPositions);
		Profile.Add("     of which who stands where, made again", System.Diagnostics.Stopwatch.GetTimestamp() - start);
		return made;
	}

	/// <summary>The units in a province, in the order they came.</summary>
	public IReadOnlyList<CUnit> UnitsIn(int province)
	{
		return Positions.Of(province);
	}

	/// <summary>To be called when a unit has been put, made or removed other than by its march.</summary>
	public void ForgetPositions()
	{
		_lastPositions = _positions ?? _lastPositions;
		_positions = null;
	}

	/// <summary>
	/// Whether the country is at war with some country, the original's <c>at_war</c> flag as <c>UpdateAtWarAndEnemies</c>
	/// sets it. A war whose other side has emptied - a capitulation or a peace leaves it standing, and the original's saves
	/// hold such wars - makes nobody at war (war.md, section 1).
	/// </summary>
	public bool IsAtWar(CCountryTag tag)
	{
		foreach (CWar war in Wars)
		{
			if ((war.Attackers.Contains(tag) && war.Defenders.Count > 0)
				|| (war.Defenders.Contains(tag) && war.Attackers.Count > 0))
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

	/// <summary>The strategic resources by name, in the order of their file.</summary>
	public IEnumerable<string> StrategicResourceNames => _strategicResources.Keys;

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
