using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// One province: what it is, and who holds it.
///
/// Id 0 is a real entry and means "no province" - it is what a point on the map that
/// belongs to nothing resolves to - so a province array is indexed by id directly, with
/// nothing subtracted. See <c>docs/mechanics/map.md</c>, section 2.
/// </summary>
public sealed class CProvince
{
	private CFixedPoint[] _buildingLevels = System.Array.Empty<CFixedPoint>();
	private CFixedPoint[] _buildingMaxLevels = System.Array.Empty<CFixedPoint>();
	private CCountryTag? _owner;
	private CCountryTag? _controller;
	private CHandChanges? _handChanges;

	public CProvince(int id, string name, bool isSea)
	{
		Id = id;
		Name = name;
		IsSea = isSea;
	}

	public int Id { get; }

	/// <summary>
	/// Localised name; falls back to the localisation key when it is missing. Script may give it
	/// another (<c>docs/mechanics/effects.md</c>, section 5).
	/// </summary>
	public string Name { get; set; }

	public bool IsSea { get; }

	/// <summary>
	/// The country it belongs to, or null: every sea province, and unclaimed land. A change counts in
	/// the game's <see cref="CHandChanges"/>.
	/// </summary>
	public CCountryTag? Owner
	{
		get => _owner;
		set
		{
			if (_owner != value)
			{
				_owner = value;
				_handChanges?.Add();
			}
		}
	}

	/// <summary>
	/// The country whose forces hold it, which differs from the owner under occupation. A change
	/// counts in the game's <see cref="CHandChanges"/>.
	/// </summary>
	public CCountryTag? Controller
	{
		get => _controller;
		set
		{
			if (_controller != value)
			{
				_controller = value;
				_handChanges?.Add();
			}
		}
	}

	/// <summary>Where a change of owner or controller is counted from now on: the game's, as it is made.</summary>
	internal void CountChangesIn(CHandChanges changes)
	{
		_handChanges = changes;
	}

	/// <summary>The countries that claim it as rightfully theirs, in the order the claims were made.</summary>
	public List<CCountryTag> Cores { get; } = new();

	/// <summary>What the province could yield in a day. Only resources are ever set.</summary>
	public CGoodsPool MaxProducing { get; } = new();

	/// <summary>
	/// What it yields in a day now. The same as the most it could until something
	/// damages it.
	/// </summary>
	public CGoodsPool CurrentProducing { get; } = new();

	/// <summary>
	/// What is lying in the province. In a capital this is the country's stockpile: see
	/// <c>docs/mechanics/stockpile.md</c>, section 1.
	/// </summary>
	public CGoodsPool Pool { get; } = new();

	/// <summary>
	/// The pool as the day's supply pass left it when it came to this province. A
	/// province further out is offered no more than this, so what reaches a province
	/// today can go one province further tomorrow. See <c>docs/mechanics/supply.md</c>,
	/// section 12.
	/// </summary>
	public CGoodsPool LastPool { get; } = new();

	/// <summary>The supplies and the fuel the units drawing here ask for in a day, as the day's supply pass counted them.</summary>
	public CGoodsPool SupplyNeed { get; } = new();

	/// <summary>What was asked of the province today: its own shortfall, and everything passed inward from further out.</summary>
	public CGoodsPool SupplyDrawn { get; private set; } = new();

	/// <summary>What was asked of it yesterday.</summary>
	public CGoodsPool LastSupplyDrawn { get; private set; } = new();

	/// <summary>What was taken out of the province today for provinces further out.</summary>
	public CGoodsPool SupplyThroughput { get; private set; } = new();

	/// <summary>What was taken out of it yesterday.</summary>
	public CGoodsPool LastSupplyThroughput { get; private set; } = new();

	/// <summary>A day begins: today's figures become yesterday's and today's start from nothing.</summary>
	public void BeginSupplyDay()
	{
		(SupplyDrawn, LastSupplyDrawn) = (LastSupplyDrawn, SupplyDrawn);
		(SupplyThroughput, LastSupplyThroughput) = (LastSupplyThroughput, SupplyThroughput);
		SupplyDrawn.Clear();
		SupplyThroughput.Clear();
		SupplyNeed.Clear();
	}

	public CFixedPoint Manpower { get; set; }

	public CFixedPoint Leadership { get; set; }

	/// <summary>Victory points.</summary>
	public int Points { get; set; }

	/// <summary>
	/// What the ground is. The map gives every province one, and its history file may
	/// replace it. Null only for the "no province" entry or a map with no terrain data.
	/// </summary>
	public CTerrain? Terrain { get; set; }

	/// <summary>
	/// The strategic resource found here, by name, or an empty string. Whoever controls
	/// the province has what the resource is worth: see <see cref="CCountryModifiers"/>.
	/// </summary>
	public string StrategicResource { get; set; } = "";

	/// <summary>The continent it is on, by the name the map gives it. Empty for the sea.</summary>
	public string Continent { get; init; } = "";

	/// <summary>
	/// The regions that list it, as numbers into <see cref="CGameState.Regions"/>, in
	/// the order the map gives them: the first is its region where only one is asked for.
	/// </summary>
	public List<int> Regions { get; } = new();

	/// <summary>
	/// The centre of its bounding box on the map, in pixels. A route is searched
	/// towards it: see <c>docs/mechanics/movement.md</c>, section 3.
	/// </summary>
	public int CentreX { get; init; }

	public int CentreY { get; init; }

	/// <summary>
	/// How well each country knows it, 0 to 9, by the country's index: rebuilt each day and raised each hour, and never
	/// saved. See <c>docs/mechanics/intelligence.md</c>, section 2.
	/// </summary>
	public byte[] Intel { get; internal set; } = System.Array.Empty<byte>();

	/// <summary>
	/// The width of its bounding box on the map and the height, added together, in
	/// pixels. It is how big the province counts as when supplies cross it: see
	/// <c>docs/mechanics/supply.md</c>, section 6.
	/// </summary>
	public int Span { get; init; }

	/// <summary>
	/// The one point that stands for the province, in pixels with y counting north:
	/// where it is on the clock and which half of the world it is in. See
	/// <c>docs/mechanics/map.md</c>, section 4.
	/// </summary>
	public int PositionX { get; init; }

	public int PositionY { get; init; }

	/// <summary>
	/// The sea province its port opens onto, or 0 for a province with no port. A fleet
	/// goes between the land and the sea here and nowhere else: see
	/// <c>docs/mechanics/fleetsandair.md</c>, section 2.
	/// </summary>
	public int PortSea { get; init; }

	/// <summary>
	/// Its number in the table of distances by sea, or -1: every port has one, and every
	/// sea province that is not a lake.
	/// </summary>
	public int SeaIndex { get; init; } = -1;

	/// <summary>
	/// The sum of everything that currently applies to the province. Rebuilt, never
	/// adjusted: see <see cref="CProvinceModifiers"/>.
	/// </summary>
	public CModifier Modifier { get; } = new();

	/// <summary>
	/// What <see cref="Modifier"/> was summed from, in the order each was added - its buildings left out, the scaled
	/// ones as their copies: what a tooltip lists as where a figure comes from. Rebuilt with it
	/// (<c>docs/mechanics/modifiers.md</c>, section 3).
	/// </summary>
	public List<CModifierSource> ModifierSources { get; } = new();

	/// <summary>
	/// Its nationalism: the years, in thousandths, since it last changed owner counted down from
	/// <c>YEARS_OF_NATIONALISM</c> (<c>docs/mechanics/occupation.md</c>, section 5).
	/// </summary>
	public CFixedPoint Nationalism { get; set; }

	/// <summary>
	/// The modifiers events and decisions have given it, each to its hour: step 12 of its
	/// modifier. See <c>docs/mechanics/effects.md</c>, section 6.
	/// </summary>
	public List<CTimedModifier> EventModifiers { get; } = new();

	/// <summary>The flags script has set on it: <c>docs/mechanics/effects.md</c>, section 3.</summary>
	public Script.CFlags Flags { get; } = new();

	/// <summary>The weather over it, made again every hour: <c>docs/mechanics/weather.md</c>.</summary>
	public CWeather Weather { get; } = new();

	/// <summary>
	/// The temperature its weather starts from: <c>climate.bmp</c>'s palette index under its unit point, less 100, by
	/// 0.150 of a degree, and its map terrain's <c>temperature</c> (<c>weather.md</c>, section 1).
	/// </summary>
	public CFixedPoint BaseTemperature { get; set; }

	/// <summary>Whether its unit point is in the northern half of the map, which decides its seasons and which way its fronts curve.</summary>
	public bool IsNorthern { get; init; }

	/// <summary>Its season, 1 to 5, made again each day; 3 all year south of about 19 degrees north (<c>weather.md</c>, section 1).</summary>
	public int Climate { get; set; }

	/// <summary>Whether it was night here when its weather was last made.</summary>
	public bool IsNight { get; set; }

	/// <summary>The weather front over it this hour, or none.</summary>
	public CWeatherFront? WeatherFront { get; set; }

	/// <summary>Which of the original's seven pictures its weather shows: <c>weather.md</c>, section 6.</summary>
	public EWeatherPicture WeatherPicture { get; set; }

	/// <summary>What <see cref="LastConvoyAttackHour"/> is for water no convoy was attacked in.</summary>
	public const long NoConvoyAttack = long.MinValue;

	/// <summary>
	/// A sea province: the hour a raid last fought a convoy here. For a day after, a
	/// fleet here is the easier to sight. See <c>docs/mechanics/navalmissions.md</c>,
	/// section 9.
	/// </summary>
	public long LastConvoyAttackHour { get; set; } = NoConvoyAttack;

	/// <summary>
	/// How much of a building there is here that is in working order: its current level,
	/// which is under its maximum only while it is damaged. It is what the province's
	/// modifier is made from. See <c>docs/mechanics/bombing.md</c>, sections 7 and 11.
	/// </summary>
	public CFixedPoint BuildingLevel(CBuilding building)
	{
		return building.Index < _buildingLevels.Length ? _buildingLevels[building.Index] : CFixedPoint.Zero;
	}

	/// <summary>How much of a building has been built here, damaged or not: what repair brings it back to.</summary>
	public CFixedPoint BuildingMaxLevel(CBuilding building)
	{
		return building.Index < _buildingMaxLevels.Length ? _buildingMaxLevels[building.Index] : CFixedPoint.Zero;
	}

	/// <summary>Sets how much of a building has been built here, all of it in working order.</summary>
	public void SetBuildingLevel(CBuilding building, CFixedPoint level)
	{
		if (building.Index >= _buildingLevels.Length)
		{
			System.Array.Resize(ref _buildingLevels, building.Index + 1);
		}
		if (building.Index >= _buildingMaxLevels.Length)
		{
			System.Array.Resize(ref _buildingMaxLevels, building.Index + 1);
		}
		_buildingLevels[building.Index] = level;
		_buildingMaxLevels[building.Index] = level;
	}

	/// <summary>
	/// Sets how much of a building has been built here and leaves what works of it, held to what is built now - as a
	/// finished building line raises it (<c>docs/mechanics/buildinglines.md</c>, section 5).
	/// </summary>
	public void SetBuildingMaxLevel(CBuilding building, CFixedPoint level)
	{
		CFixedPoint working = BuildingLevel(building);
		SetBuildingLevel(building, level);
		_buildingLevels[building.Index] = working > level ? level : working;
	}

	/// <summary>
	/// Sets how much of a building is in working order, between nothing and what has
	/// been built. Damage and repair go through here, and where the level moves the
	/// province is marked for its modifier to be made again within the hour.
	/// </summary>
	public void SetBuildingCurrentLevel(CBuilding building, CFixedPoint level)
	{
		CFixedPoint most = BuildingMaxLevel(building);
		if (building.Index >= _buildingLevels.Length)
		{
			System.Array.Resize(ref _buildingLevels, building.Index + 1);
		}
		CFixedPoint set = level < CFixedPoint.Zero ? CFixedPoint.Zero : level > most ? most : level;
		if (set != _buildingLevels[building.Index])
		{
			BuildingMoved = true;
		}
		_buildingLevels[building.Index] = set;
	}

	/// <summary>
	/// Whether a building's working level has moved since its modifier was last made: the original's "changed" byte on
	/// each building, which the hour's look at the province asks and the rebuild clears
	/// (<c>docs/mechanics/modifiers.md</c>, section 3).
	/// </summary>
	public bool BuildingMoved { get; set; }

	/// <summary>Every province within two edges, as <see cref="CNearProvinces"/> worked it out; null until asked for.</summary>
	internal List<int>? NearProvinces { get; set; }

	/// <summary>
	/// Its revolt risk, the accumulator, in thousandths of a point: moved by the day's step while its holder is at war
	/// with its owner, never below nought (<c>docs/mechanics/revolt.md</c>, section 3).
	/// </summary>
	public CFixedPoint RevoltRisk { get; set; }

	/// <summary>The suppression in and around it, worked out once a day (<c>docs/mechanics/revolt.md</c>, section 2).</summary>
	public CFixedPoint Suppression { get; set; }

	/// <summary>
	/// The last day the rebels took it with a capture's consequences, as its history keeps it - what a faction's
	/// independence counts from - or none (<c>docs/mechanics/rebels.md</c>, section 12).
	/// </summary>
	public CDate? RebelsTook { get; set; }

	/// <summary>
	/// The rebel type and size its history's <c>revolt</c> gives it, carried out as a new game is entered; none
	/// (<c>docs/mechanics/rebels.md</c>, section 8).
	/// </summary>
	public string? RevoltType { get; set; }

	public int RevoltSize { get; set; }

	/// <summary>Whose its underground is, where it has one (<c>docs/mechanics/undergrounds.md</c>, section 1).</summary>
	public CCountryTag? UndergroundOwner { get; set; }

	/// <summary>
	/// What its underground does: 0 raise partisans, 1 spread, 2 build up strength - 2 until set, and never reset, so a
	/// new underground takes up the last action set here.
	/// </summary>
	public int UndergroundAction { get; set; } = Revolts.CUndergrounds.BuildUp;

	/// <summary>Ids of the provinces this one touches, ascending. Filled from the map.</summary>
	public IReadOnlyList<int> Neighbours { get; set; } = System.Array.Empty<int>();

	/// <summary>
	/// The ways out of the province, in the order the original holds them, which is the
	/// order a route is searched in. Filled from the map.
	/// </summary>
	public IReadOnlyList<CProvinceEdge> Edges { get; set; } = System.Array.Empty<CProvinceEdge>();

	/// <summary>The way to a neighbour, or null when the two are not joined.</summary>
	public CProvinceEdge? EdgeTo(int neighbour)
	{
		foreach (CProvinceEdge edge in Edges)
		{
			if (edge.To == neighbour)
			{
				return edge;
			}
		}
		return null;
	}

	public override string ToString()
	{
		return $"{Id} ({Name})";
	}
}
