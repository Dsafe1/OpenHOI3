using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;
using Xunit;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The first and third stages of an invasion's hour, against <c>docs/mechanics/moving.md</c>, section 16, on three
/// pieces of land and the seas between them:
///
///     1 Kiel ==port== 10 Bight ---- 11 Sound ==port== 3 Insel --- 4 Hinterland ==port== 12 Gulf
///       |                  \________________________________________________________/  |
///     2 Berlin                                                              13 Reef ==port== 5 Hel --- 6 Strand
///
/// Germany holds Kiel and Berlin, and the island of Insel and Hinterland; Poland holds Hel and Strand, another island.
/// Kiel, Insel and Hel have a naval base, and Hinterland one where a test asks. The two are at war.
/// </summary>
public class InvasionTickTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Kiel = 1;
	private const int Berlin = 2;
	private const int Insel = 3;
	private const int Hinterland = 4;
	private const int Hel = 5;
	private const int Strand = 6;
	private const int Bight = 10;
	private const int Sound = 11;
	private const int Gulf = 12;
	private const int Reef = 13;

	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, CBombingOrders.NavalBaseLevel, CFixedPoint.One);

	private static readonly CSubUnitType Infantry = Type("infantry_brigade", EUnitBranch.Land, (CSubUnitType.TransportWeight, "5"));
	private static readonly CSubUnitType Transport = Type("transport_ship", EUnitBranch.Naval, (CTransport.TransportCapability, "10"));

	private static CSubUnitType Type(string key, EUnitBranch branch, params (string Name, string Value)[] stats)
	{
		var type = new CSubUnitType(key, branch);
		type.Stats.Values.Set(CMovementSpeed.MaximumSpeed, CFixedPoint.FromInt(branch == EUnitBranch.Land ? 4 : 20));
		type.Stats.Values.Set(CUnitOrders.Range, CFixedPoint.FromInt(1000));
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(branch == EUnitBranch.Land ? 3 : 100));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(30));
		foreach ((string name, string value) in stats)
		{
			type.Stats.Values.Set(name, CFixedPoint.Parse(value));
		}
		return type;
	}

	private static CProvinceEdge Edge(int to) => new(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10));

	private static CProvince Land(int id, string name, CCountryTag owner, int x, int y, int portSea, params int[] beside)
	{
		var province = new CProvince(id, name, isSea: false)
		{
			Owner = owner, Controller = owner, CentreX = x, CentreY = y, SeaIndex = -1, PortSea = portSea,
			Edges = beside.Select(Edge).ToArray(),
		};
		if (portSea > 0)
		{
			province.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(2));
		}
		return province;
	}

	private static CProvince Sea(int id, string name, int index, int x, int y, params int[] beside)
	{
		return new CProvince(id, name, isSea: true) { CentreX = x, CentreY = y, SeaIndex = index, Edges = beside.Select(Edge).ToArray() };
	}

	/// <param name="secondPort">Hinterland a port too, on the Gulf - further from Kiel by sea than Insel's.</param>
	private static CGameState State(out CCountry germany, out CCountry poland, bool secondPort = false, bool atWar = true)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = Strand };
		var defines = new CDefines();
		void Military(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Military(CMovementSpeed.AirSpeedModifier, "0.5");
		Military(CMovementSpeed.NavalSpeedModifier, "0.5");
		Military(CMovementSpeed.LandSpeedModifier, "1");
		var wars = new List<CWar>();
		if (atWar)
		{
			var war = new CWar("war");
			war.Attackers.Add(Ger);
			war.Defenders.Add(Pol);
			wars.Add(war);
		}
		var empty = new CProvince(0, "", isSea: false);
		var provinces = new CProvince[14];
		for (int id = 0; id < provinces.Length; id++)
		{
			provinces[id] = id == 0 ? empty : new CProvince(id, "", isSea: false) { SeaIndex = -1 };
		}
		provinces[Kiel] = Land(Kiel, "Kiel", Ger, 10, 10, Bight, Bight, Berlin);
		provinces[Berlin] = Land(Berlin, "Berlin", Ger, 10, 30, 0, Kiel);
		provinces[Insel] = Land(Insel, "Insel", Ger, 60, 10, Sound, Sound, Hinterland);
		provinces[Hinterland] = Land(Hinterland, "Hinterland", Ger, 80, 10, secondPort ? Gulf : 0, secondPort ? new[] { Insel, Gulf } : new[] { Insel });
		provinces[Hel] = Land(Hel, "Hel", Pol, 80, 50, Reef, Reef, Strand);
		provinces[Strand] = Land(Strand, "Strand", Pol, 95, 50, 0, Hel);
		provinces[Bight] = Sea(Bight, "Bight", 0, 25, 10, Kiel, Sound, Reef);
		provinces[Sound] = Sea(Sound, "Sound", 1, 45, 10, Bight, Insel, Gulf);
		provinces[Gulf] = Sea(Gulf, "Gulf", 2, 95, 10, secondPort ? new[] { Sound, Hinterland, Reef } : new[] { Sound, Reef });
		provinces[Reef] = Sea(Reef, "Reef", 3, 60, 50, Bight, Gulf, Hel);
		var state = new CGameState(new CDate(1936, 6, 1), provinces, new[] { germany, poland },
			buildings: CTestRoads.With(NavalBase), defines: defines, wars: wars)
		{
			MapWidth = 1000,
			MapHeight = 1000,
		};
		return state.Paved();
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, int province, CSubUnitType type, int based = 0)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = province, Base = based, Name = type.Key };
		var sub = new CSubUnit(type)
		{
			Id = ++state.LastUnitId, Unit = unit, Strength = type.Stats.Values[CSubUnitType.MaxStrength],
			Organisation = CFixedPoint.FromInt(30), Name = type.Key,
		};
		sub.Stats.CopyFrom(type.Stats);
		unit.SubUnits.Add(sub);
		country.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	private static CUnit Division(CGameState state, CCountry country, int province) =>
		Unit(state, country, EUnitKind.Division, province, Infantry);

	private static CUnit Fleet(CGameState state, CCountry country, int province) =>
		Unit(state, country, EUnitKind.Navy, province, Transport, based: Kiel);

	/// <summary>An invasion of Hel sailing from Kiel, the fleets to meet at Kiel.</summary>
	private static CInvasion Invasion(params CUnit[] units)
	{
		var invasion = new CInvasion(Ger, Hel) { EmbarkationPort = Kiel, Rendezvous = Kiel };
		foreach (CUnit unit in units)
		{
			invasion.Add(unit);
		}
		return invasion;
	}

	/// <summary>The orders posted, each as its command says itself.</summary>
	private static List<string> Posted(CGameState state) => state.Outbox.Select(command => command.ToString()!).ToList();

	/// <summary>Whether the AI sent a unit to a province: by a move, or by the strategic redeployment a long way becomes.</summary>
	private static bool SentTo(CGameState state, CUnit unit, int province)
	{
		return Posted(state).Any(order => order == $"move unit {unit.Id} to {province}"
			|| order == $"order StrategicRedeployment to unit {unit.Id} at {province}");
	}

	// --- stage 1: the troops ---

	[Fact]
	public void TroopsWithAWayByLandMarchToTheEmbarkationPortAndThoseWithNoneToAPortOfTheirOwnArea()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit island = Division(state, germany, Hinterland);
		CUnit mainland = Division(state, germany, Berlin);
		CInvasion invasion = Invasion(island, mainland);

		CInvasionTick.March(state, germany, invasion);

		// No way by land from the island to Kiel: to Insel, its own area's port, which is where the fleets now meet.
		Assert.True(SentTo(state, island, Insel));
		Assert.Equal(Insel, island.Staging);
		Assert.Equal(Insel, invasion.Rendezvous);
		Assert.Equal(Kiel, invasion.EmbarkationPort);
		// From Berlin there is one: to the port itself.
		Assert.True(SentTo(state, mainland, Kiel));
		Assert.Equal(Kiel, mainland.Staging);
		Assert.Equal(2, state.Outbox.Count);
	}

	[Fact]
	public void OfAnAreasPortsTheNearestToTheEmbarkationPortBySeaIsTaken()
	{
		CGameState state = State(out CCountry germany, out _, secondPort: true);
		// Standing in Hinterland, itself a port now - and further from Kiel by sea than Insel's.
		CUnit island = Division(state, germany, Hinterland);
		CInvasion invasion = Invasion(island);
		Assert.True(CSeaTargets.Sea(state, Insel, Kiel) < CSeaTargets.Sea(state, Hinterland, Kiel));

		CInvasionTick.March(state, germany, invasion);

		Assert.True(SentTo(state, island, Insel));
		Assert.Equal(Insel, island.Staging);
		Assert.Equal(Insel, invasion.Rendezvous);
	}

	[Fact]
	public void AUnitAtThePortIsStagedThereAndOneAtItsStagingMovesTheMeetingPlaceBackToIt()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit atPort = Division(state, germany, Kiel);
		CUnit staged = Division(state, germany, Insel);
		staged.Staging = Insel;
		CInvasion invasion = Invasion(atPort, staged);

		CInvasionTick.March(state, germany, invasion);

		// Nobody is sent anywhere: the one stands at the port, the other at its staging.
		Assert.Empty(state.Outbox);
		Assert.Equal(Kiel, atPort.Staging);
		// Everyone settled and the fleets still to meet at the port: they meet where the staged unit is.
		Assert.Equal(Insel, invasion.Rendezvous);

		// With a unit still to be sent, the meeting place is not moved by that rule - only by the sending.
		invasion.Rendezvous = Kiel;
		CUnit late = Division(state, germany, Berlin);
		invasion.Add(late);
		CInvasionTick.March(state, germany, invasion);
		Assert.True(SentTo(state, late, Kiel));
		Assert.Equal(Kiel, invasion.Rendezvous);
	}

	[Fact]
	public void AUnitThatCanWalkToTheObjectiveIsLetGo()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit ashore = Division(state, germany, Strand);
		CUnit waiting = Division(state, germany, Kiel);
		CInvasion invasion = Invasion(ashore, waiting);

		CInvasionTick.March(state, germany, invasion);

		Assert.Equal(new[] { waiting }, invasion.LandForce);
		Assert.Null(ashore.Invasion);
		Assert.Same(invasion, waiting.Invasion);
	}

	// --- stage 3: the fleets ---

	[Fact]
	public void AnEmptyFleetUnderAnyOrderHasItTakenAwayAndSailsTheHourAfter()
	{
		CGameState state = State(out CCountry germany, out _);
		// Back at its base under the transport that put its troops off: empty, and still under the order.
		CUnit fleet = Fleet(state, germany, Insel);
		fleet.Order = EUnitOrder.Transport;
		fleet.OrderProvince = Kiel;
		CInvasion invasion = Invasion(fleet, Division(state, germany, Kiel));

		CInvasionTick.Sail(state, germany, invasion);

		// The order goes, and the meeting place is where the AI has it bound - so nothing sends it this hour.
		Assert.Equal(new[] { typeof(CDropOrderCommand) }, state.Outbox.Select(command => command.GetType()));
		Assert.Equal(Kiel, fleet.AiBound);

		// The hour after, the order gone and nothing bound: sent to the meeting place.
		state.Outbox[0].Execute(state);
		state.Outbox.Clear();
		fleet.AiBound = -1;
		Assert.Equal(EUnitOrder.None, fleet.Order);
		CInvasionTick.Sail(state, germany, invasion);
		Assert.Equal(new[] { $"move unit {fleet.Id} to {Kiel}" }, Posted(state));
	}

	[Fact]
	public void AnEmptyFleetIsSentToTheMeetingPlaceByAMoveThatAsksNoRange()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Insel);
		// Based where it lies, with a range that reaches nowhere from there.
		fleet.Base = Insel;
		fleet.SubUnits[0].Stats.Values.Set(CUnitOrders.Range, CFixedPoint.Parse("0.001"));
		CInvasion invasion = Invasion(fleet, Division(state, germany, Kiel));

		CInvasionTick.Sail(state, germany, invasion);

		CCommand move = Assert.Single(state.Outbox);
		Assert.Equal($"move unit {fleet.Id} to {Kiel}", move.ToString());
		Assert.True(move.MayExecute(state));
		// A player's move of the same fleet there is refused for its range.
		Assert.False(new CMoveUnitCommand(Ger, fleet.Id, Kiel).MayExecute(state));
	}

	[Fact]
	public void TwoFleetsArrivedAreMergedAndNothingIsLoadedThatHour()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit first = Fleet(state, germany, Kiel);
		CUnit second = Fleet(state, germany, Kiel);
		CUnit division = Division(state, germany, Kiel);
		CInvasion invasion = Invasion(first, second, division);

		CInvasionTick.Sail(state, germany, invasion);

		CCommand join = Assert.Single(state.Outbox);
		Assert.IsType<CJoinUnitCommand>(join);
	}

	[Fact]
	public void TheLoneFleetLoadsWhoStandsAtTheMeetingPlaceAndSailsForThePortTheSameHour()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Insel);
		CUnit division = Division(state, germany, Insel);
		CInvasion invasion = Invasion(fleet, division);
		invasion.Rendezvous = Insel;

		CInvasionTick.Sail(state, germany, invasion);

		// Loaded, and with nobody else coming the transport to the embarkation port right behind the load.
		Assert.Collection(state.Outbox,
			command => Assert.IsType<CLoadUnitCommand>(command),
			command => Assert.Equal($"order Transport to unit {fleet.Id} at {Kiel}", command.ToString()));
		Assert.Equal(Kiel, invasion.Rendezvous);
		Assert.Equal(Kiel, fleet.AiBound);
	}

	[Fact]
	public void TheLoneFleetWaitsForTroopsStillWalkingThereWhileTheSmallestStillFits()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Insel);
		CUnit there = Division(state, germany, Insel);
		CUnit coming = Division(state, germany, Hinterland);
		CMovement.SetPath(coming, new[] { Insel }, clear: true);
		CInvasion invasion = Invasion(fleet, there, coming);
		invasion.Rendezvous = Insel;

		CInvasionTick.Sail(state, germany, invasion);

		// Ten of room, five aboard with this load and five still coming: it loads the one and waits for the other.
		CCommand load = Assert.Single(state.Outbox);
		Assert.IsType<CLoadUnitCommand>(load);
		Assert.Equal(Insel, invasion.Rendezvous);

		// A ship that holds only the one does not wait for a unit it could not take.
		state.Outbox.Clear();
		fleet.SubUnits[0].Stats.Values.Set(CTransport.TransportCapability, CFixedPoint.FromInt(5));
		CInvasionTick.Sail(state, germany, invasion);
		Assert.Collection(state.Outbox,
			command => Assert.IsType<CLoadUnitCommand>(command),
			command => Assert.Equal($"order Transport to unit {fleet.Id} at {Kiel}", command.ToString()));
	}

	[Fact]
	public void AFleetNotAtTheMeetingPlaceOrUnderAnOrderLoadsNothingAndIsGivenNoOrder()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Kiel);
		CUnit division = Division(state, germany, Kiel);
		CInvasion invasion = Invasion(fleet, division);

		// Carrying and under its invasion: nothing of the stage touches it.
		CTransport.Board(fleet, division);
		fleet.Order = EUnitOrder.Invasion;
		fleet.OrderProvince = Hel;
		CInvasionTick.Sail(state, germany, invasion);
		Assert.Empty(state.Outbox);

		// Already sent somewhere by the AI this hour: nothing either.
		fleet.Order = EUnitOrder.None;
		fleet.AiBound = Insel;
		CInvasionTick.Sail(state, germany, invasion);
		Assert.Empty(state.Outbox);
	}

	[Fact]
	public void AtThePortTheFleetIsGivenTheInvasionOnAnEnemysCoastAndATransportToAnObjectiveNoEnemyHolds()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Kiel);
		CUnit division = Division(state, germany, Kiel);
		CInvasion invasion = Invasion(fleet, division);
		CTransport.Board(fleet, division);
		var agent = new CAgent(Ger, 1);
		germany.UnitAi.Agents.Add(agent);

		CInvasionTick.Sail(state, germany, invasion);

		// The landing picked again in the objective's area, the invasion on it, and every agent told to sort its units again.
		CCommand order = Assert.Single(state.Outbox);
		Assert.Equal($"order Invasion to unit {fleet.Id} at {invasion.Objective}", order.ToString());
		Assert.Contains(invasion.Objective, new[] { Hel, Strand });
		Assert.Equal(invasion.Objective, fleet.AiBound);
		Assert.True(agent.ReplanSubordinates);

		// An objective no enemy holds any more: the troops are carried there.
		state.Outbox.Clear();
		fleet.AiBound = -1;
		invasion.Objective = Hel;
		state.Province(Hel).Controller = Ger;
		CInvasionTick.Sail(state, germany, invasion);
		Assert.Equal(new[] { $"order Transport to unit {fleet.Id} at {Hel}" }, Posted(state));
	}

	[Fact]
	public void TheOrderAtThePortIsGivenWhetherOrNotAnythingIsAboard()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Kiel);
		// The one division of the force is far off and bound nowhere near: nobody at the port, nobody coming.
		CUnit division = Division(state, germany, Hinterland);
		CInvasion invasion = Invasion(fleet, division);
		state.Province(Hel).Controller = Ger;

		CInvasionTick.Sail(state, germany, invasion);

		Assert.Empty(fleet.Passengers);
		Assert.Equal(new[] { $"order Transport to unit {fleet.Id} at {Hel}" }, Posted(state));
	}
}
