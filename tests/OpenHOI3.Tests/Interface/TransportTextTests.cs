using System.Collections.Generic;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the screen shows of carrying, against <c>docs/mechanics/transport.md</c>, section 12, on a port, its sea and an
/// enemy's coast beside it:
///
///     1 Kiel ==port== 2 Bight --- 3 Hela (Poland's)
/// </summary>
public class TransportTextTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Kiel = 1;
	private const int Bight = 2;
	private const int Hela = 3;

	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, "", CFixedPoint.Zero);

	private static readonly Dictionary<string, string> Texts = new()
	{
		["LOAD_CAPACITY_LABEL"] = "Can carry $NUM$ weight.",
		["AMPH_INVASION_COMBAT_PROGRESS_DESC"] = "In transports.",
		["AMPH_INVASION_PROGRESS"] = "Amphibious invasion progress: $PERC$%",
		["AMPH_INVASION_ATTACK_SCALE"] = "Invasion total attack ability: $PERC$%",
		["AMPH_INVASION_DEFEND_SCALE"] = "Invasion total defence ability: $PERC$%",
		["BATTLE_ATTACKMOD"] = "Attack Modifier",
		["BATTLE_DEFENDMOD"] = "Defend Modifier",
		["AND"] = "and ",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static CProvinceEdge Edge(int to) => new(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10));

	private static CGameState State(out CCountry germany)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var kiel = new CProvince(Kiel, "Kiel", isSea: false)
		{
			Owner = Ger, Controller = Ger, PortSea = Bight, SeaIndex = -1, Edges = new[] { Edge(Bight) },
		};
		kiel.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(1));
		var defines = new CDefines();
		void Military(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Military(CLanding.MovementCost, "24");
		Military(CLanding.AttackLow, "0.2");
		Military(CLanding.AttackHigh, "1.0");
		Military(CLanding.DefendLow, "1.5");
		Military(CLanding.DefendHigh, "1.0");
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		return new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				kiel,
				new CProvince(Bight, "Bight", isSea: true) { SeaIndex = 0, Edges = new[] { Edge(Kiel), Edge(Hela) } },
				new CProvince(Hela, "Hela", isSea: false) { Owner = Pol, Controller = Pol, SeaIndex = -1, Edges = new[] { Edge(Bight) } },
			},
			new[] { germany, poland }, buildings: new[] { NavalBase }, defines: defines, wars: new List<CWar> { war });
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, int province, string stat, string value, int parts)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = province, Name = kind.ToString() };
		var type = new CSubUnitType("part", kind == EUnitKind.Navy ? EUnitBranch.Naval : EUnitBranch.Land);
		type.Stats.Values.Set(stat, CFixedPoint.Parse(value));
		type.Stats.Values.Set(CSubUnitType.CombatWidth, CFixedPoint.One);
		for (int i = 0; i < parts; i++)
		{
			var part = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = unit, Strength = CFixedPoint.FromInt(3), Organisation = CFixedPoint.FromInt(30) };
			part.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(part);
		}
		country.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	[Fact]
	public void TheHeaderSaysWhatIsLeftAndTheWhole()
	{
		CGameState state = State(out CCountry germany);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Kiel, CTransport.TransportCapability, "40", 3);
		Assert.Equal("Can carry 120 weight.", CTransportText.Header(Localise, fleet));

		CTransport.Board(fleet, Unit(state, germany, EUnitKind.Division, Kiel, CSubUnitType.TransportWeight, "10", 3));
		// The French fleet of the reference saves.
		Assert.Equal("Can carry 90 (120) weight.", CTransportText.Header(Localise, fleet));
		// A warship's panel says it too.
		Assert.Equal("Can carry 0 weight.", CTransportText.Header(Localise, Unit(state, germany, EUnitKind.Navy, Kiel, "sea_attack", "5", 1)));
	}

	[Fact]
	public void TheLoadButtonFindsTheFirstFleetInThePortWithRoomAndTheUnloadButtonWantsAPort()
	{
		CGameState state = State(out CCountry germany);
		CUnit full = Unit(state, germany, EUnitKind.Navy, Kiel, "sea_attack", "5", 1);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Kiel, CTransport.TransportCapability, "40", 1);
		CUnit division = Unit(state, germany, EUnitKind.Division, Kiel, CSubUnitType.TransportWeight, "10", 3);

		CCarryButtons buttons = CTransportText.Buttons(state, germany, division);
		Assert.True(buttons.LoadShown && buttons.LoadEnabled && !buttons.UnloadShown);
		Assert.Equal(fleet.Id, buttons.Fleet);
		Assert.NotEqual(full.Id, buttons.Fleet);
		Assert.Equal("UW_LOAD_IS_VALID", CUnitOrderTooltips.Buttons(Localise, division, false, "load_button", buttons).Text);

		CTransport.Board(fleet, division);
		buttons = CTransportText.Buttons(state, germany, division);
		Assert.True(!buttons.LoadShown && buttons.UnloadShown && buttons.UnloadEnabled);
		Assert.Equal("UW_UNLOAD_VALID", CUnitOrderTooltips.Buttons(Localise, division, false, "unload_button", buttons).Text);

		division.Location = Bight;
		buttons = CTransportText.Buttons(state, germany, division);
		Assert.False(buttons.UnloadEnabled);
		Assert.Equal("UW_UNLOAD_NOT_VALID", CUnitOrderTooltips.Buttons(Localise, division, false, "unload_button", buttons).Text);

		Assert.Equal(CCarryButtons.None, CTransportText.Buttons(state, germany, fleet));
	}

	[Fact]
	public void APassengerInPortHasNoCounterAndOneAtSeaStandsWithItsFleet()
	{
		CGameState state = State(out CCountry germany);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Kiel, CTransport.TransportCapability, "40", 1);
		CUnit division = Unit(state, germany, EUnitKind.Division, Kiel, CSubUnitType.TransportWeight, "10", 3);
		CTransport.Board(fleet, division);
		Assert.False(CMapCounters.HasCounter(state, division));

		fleet.Location = Bight;
		division.Location = Bight;
		Assert.True(CMapCounters.HasCounter(state, division));
		Assert.Equal(EMapStack.Standing, CMapCounters.StackOf(state, division));
		fleet.Path.Add(Kiel);
		Assert.Equal(EMapStack.Moving, CMapCounters.StackOf(state, division));
		fleet.Path.Clear();

		// Its way ashore is drawn with the landing's arrow.
		division.Path.Add(Hela);
		Assert.Equal(CArrowTypes.Invasion, CMapArrows.TypeOf(state, division));
		Assert.Equal(EMapStack.Standing, CMapCounters.StackOf(state, division));
	}

	[Fact]
	public void ALandingUnitsTooltipHasItsProgressAndScalesAndTheModifierLinesBoth()
	{
		CGameState state = State(out CCountry germany);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Bight, CTransport.TransportCapability, "40", 1);
		CUnit division = Unit(state, germany, EUnitKind.Division, Bight, CSubUnitType.TransportWeight, "10", 3);
		CTransport.Board(fleet, division);
		division.Path.Add(Hela);
		division.MovementProgress = CFixedPoint.FromInt(1);
		CLanding.SetScales(state, division);

		Assert.Equal("In transports.\n\nAmphibious invasion progress: §R4.10§!%\nInvasion total attack ability: §R23.20§!%"
			+ "\nInvasion total defence ability: §G148.00§!%\n\n", CBattleUnitTooltips.Landing(Localise, state, division));

		division.CombatAttack = CFixedPoint.Parse("0.331");
		division.CombatDefence = CFixedPoint.Parse("0.331");
		var combat = new CCombat(Hela);
		combat.Attacker.Add(division);
		string text = CBattleUnitTooltips.Unit(Localise, state, combat, division, "").Text;
		Assert.Contains("Attack Modifier: §R7.60§!% / §G33.10§!%\n", text);
		Assert.Contains("Defend Modifier: §G48.90§!% / §R33.10§!%\n", text);

		division.MovementProgress = CFixedPoint.FromInt(24);
		CLanding.SetScales(state, division);
		Assert.Equal("Amphibious invasion progress: §G100.00§!%\n\n", CBattleUnitTooltips.Landing(Localise, state, division));
	}

	[Fact]
	public void TheWarningsProvincesAreAListEachOnce()
	{
		CGameState state = State(out _);
		Assert.Equal("Hela", CMessages.ProvinceList(Localise, state, "3"));
		Assert.Equal("Kiel and  Hela", CMessages.ProvinceList(Localise, state, "1,3"));
		// A repeat is written once; the count runs over the numbers given, so the separator stays.
		Assert.Equal("Hela and  ", CMessages.ProvinceList(Localise, state, "3,3"));
	}
}
