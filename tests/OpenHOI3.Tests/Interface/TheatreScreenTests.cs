using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The theatres screen and what a plan is short of, against <c>docs/mechanics/theatresscreen.md</c>. Germany owns 1 to 5 in a row, Poland 6, beside 5; every province is
/// fully roaded and nobody is at war.
/// </summary>
public class TheatreScreenTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static string Text(string key) => key switch
	{
		"OBJ_AND_MORE" => "$NAME$ & more...",
		"NO_NEEDS_FOR_BUILDS" => "none",
		_ => key,
	};

	private static CSubUnitType Type(string key, EUnitBranch branch, int index)
	{
		var type = new CSubUnitType(key, branch) { DatabaseIndex = index };
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(10));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(CSubUnitType.CombatWidth, CFixedPoint.One);
		type.Stats.Values.Set("softness", CFixedPoint.One);
		type.Stats.Values.Set(CSubUnitStats.BuildCostManpower, CFixedPoint.FromInt(1));
		return type;
	}

	private static CGameState World()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 3, Manpower = CFixedPoint.FromInt(100), TotalIc = 100 };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = 6 };
		var provinces = new List<CProvince>();
		for (int id = 0; id <= 6; id++)
		{
			CCountryTag? owner = id switch { >= 1 and <= 5 => Ger, 6 => Pol, _ => null };
			var neighbours = new List<int>();
			if (id >= 2)
			{
				neighbours.Add(id - 1);
			}
			if (id is >= 1 and < 6)
			{
				neighbours.Add(id + 1);
			}
			provinces.Add(new CProvince(id, $"p{id}", isSea: false) { Owner = owner, Controller = owner, Neighbours = neighbours.ToArray() });
		}
		TestRoads.Pave(provinces);
		return new CGameState(new CDate(1936, 1, 1), provinces.ToArray(), new[] { germany, poland }, buildings: new[] { TestRoads.Infra })
		{
			SubUnitTypes = new Dictionary<string, CSubUnitType>
			{
				["infantry_brigade"] = Type("infantry_brigade", EUnitBranch.Land, 1),
				["garrison_brigade"] = Type("garrison_brigade", EUnitBranch.Land, 2),
				["tactical_bomber"] = Type("tactical_bomber", EUnitBranch.Air, 3),
				["interceptor"] = Type("interceptor", EUnitBranch.Air, 4),
			},
		};
	}

	private static int _nextId = 2000;

	private static CUnit Headquarters(CGameState state)
	{
		var unit = new CUnit(EUnitKind.Theatre, Ger) { Id = ++_nextId, Location = 3, Name = "OB West" };
		state.Country(Ger)!.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	private static CUnit Division(CGameState state, CUnit parent, int location, string type = "infantry_brigade")
	{
		var unit = new CUnit(EUnitKind.Division, Ger) { Id = ++_nextId, Location = location, Name = "d" + _nextId, Parent = parent };
		var brigade = new CSubUnit(state.SubUnitTypes[type]) { Id = ++_nextId, Unit = unit };
		CUnits.RebuildStats(brigade);
		unit.SubUnits.Add(brigade);
		parent.Children.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	[Fact]
	public void CountsLeaveOutNoneAndJoinWithCommas()
	{
		Assert.Equal("3 £infantry_brigade, 1 £garrison_brigade",
			CTheatreScreenText.Counts(new[] { ("infantry_brigade", 3), ("armor_brigade", 0), ("garrison_brigade", 1) }));
	}

	[Fact]
	public void UpToThreeObjectivesAreNamedWithAnAmpersandBeforeTheLast()
	{
		CGameState state = World();
		CUnit headquarters = Headquarters(state);
		headquarters.Plan.Active = true;
		headquarters.Plan.Objectives.AddRange(new[] { new CObjective(1, 0), new CObjective(2, 0), new CObjective(3, 0) });
		Assert.Equal("p1, p2 & p3", CTheatreScreenText.Objectives(Text, state, headquarters));
	}

	[Fact]
	public void FourObjectivesNameTheOneWithTheMostPoints()
	{
		CGameState state = World();
		state.Province(4).Points = 2;
		CUnit headquarters = Headquarters(state);
		headquarters.Plan.Active = true;
		headquarters.Plan.Objectives.AddRange(new[] { new CObjective(1, 0), new CObjective(2, 0), new CObjective(4, 0), new CObjective(5, 0) });
		Assert.Equal("p4 & more...", CTheatreScreenText.Objectives(Text, state, headquarters));
	}

	[Fact]
	public void AnInactivePlanHasNoObjectivesToName()
	{
		CGameState state = World();
		CUnit headquarters = Headquarters(state);
		headquarters.Plan.Objectives.Add(new CObjective(1, 0));
		Assert.Equal("", CTheatreScreenText.Objectives(Text, state, headquarters));
	}

	[Fact]
	public void TheMiddleOfAFrontNamesIt()
	{
		CGameState state = World();
		var front = new CFront();
		front.Provinces.AddRange(new[] { 1, 2, 3, 4, 5 });
		Assert.Equal(3, CTheatreScreenText.NamingProvince(state, front)?.Id);
	}

	[Fact]
	public void AFrontOfOneProvinceOrFacingAFriendGetsNoRow()
	{
		CGameState state = World();
		var theatre = new CTheatre(1, Ger);
		var lone = new CFront { Enemy = Pol };
		lone.Provinces.Add(5);
		var nobody = new CFront();
		nobody.Provinces.AddRange(new[] { 1, 2 });
		var enemy = new CFront { Enemy = Pol };
		enemy.Provinces.AddRange(new[] { 4, 5 });
		theatre.Fronts.AddRange(new[] { lone, nobody, enemy });
		Assert.Equal(new[] { enemy }, CTheatreScreenText.AreaFronts(state, state.Country(Ger)!, theatre));
	}

	[Theory]
	[InlineData(800, 1000, 1, "ODDS_DEFEND_GOOD")]
	[InlineData(600, 1000, 2, "ODDS_DEFEND_AVERAGE")]
	[InlineData(500, 1000, 3, "ODDS_DEFEND_BAD")]
	[InlineData(1000, 0, 3, "ODDS_DEFEND_BAD")]
	public void TheOddsComeFromThePlansPowers(long ours, long theirs, int frame, string key)
	{
		CGameState state = World();
		CUnit headquarters = Headquarters(state);
		headquarters.Plan.Active = true;
		headquarters.Plan.OurPower = ours;
		headquarters.Plan.TheirPower = theirs;
		Assert.Equal(frame, CTheatreScreenText.RatioFrame(state, headquarters));
		Assert.Equal(key, CTheatreScreenText.OddsKey(state, headquarters));
	}

	[Fact]
	public void AnAttackingPlanHasTheAttackSentence()
	{
		CGameState state = World();
		CUnit headquarters = Headquarters(state);
		headquarters.Plan.Active = true;
		headquarters.Plan.Stance = CUnitPlan.Attacking;
		headquarters.Plan.OurPower = 1000;
		headquarters.Plan.TheirPower = 1000;
		Assert.Equal("ODDS_ATTACK_GOOD", CTheatreScreenText.OddsKey(state, headquarters));
	}

	[Fact]
	public void TheStanceButtonWrapsAfterFive()
	{
		Assert.Equal(1, CTheatreScreenText.NextStance(0));
		Assert.Equal(0, CTheatreScreenText.NextStance(4));
	}

	[Fact]
	public void AnAreaOfFiveProvincesAtPeaceAsksAFifthOfNinePerProvince()
	{
		CGameState state = World();
		CUnit headquarters = Headquarters(state);
		Division(state, headquarters, 3);
		CPlanWanted result = CPlanWants.Compute(state, headquarters, new[] { 1, 2, 3, 4, 5 });
		// Size 9, so high 9 and low 4. Fast falls back on infantry and adds one; of the infantry's steps two pass the low bar over the one held,
		// the first a mountain step that falls back on infantry and the second a garrison, each taken with no call for it.
		Assert.Equal(2, result.Wanted[1]);
		Assert.Equal(1, result.Wanted[2]);
	}

	[Fact]
	public void AShortageIsTakenOffTwice()
	{
		CGameState state = World();
		CUnit headquarters = Headquarters(state);
		Division(state, headquarters, 3);
		int[]? needed = CPlanWants.Needed(state, headquarters, new[] { 1, 2, 3, 4, 5 });
		// Wanted is already net of the brigade held, and the brigade is taken off again here.
		Assert.Equal(1, needed![1]);
		Assert.Equal(1, needed[2]);
	}

	[Fact]
	public void AnActivePlanAnswersFromItsOwnFiguresAndASurplusCancelsWithinTheGroup()
	{
		CGameState state = World();
		CUnit headquarters = Headquarters(state);
		Division(state, headquarters, 3);
		headquarters.Plan.Active = true;
		headquarters.Plan.Min.AddRange(new[] { 0, 0, 2, 0, 0 });
		int[]? needed = CPlanWants.Needed(state, headquarters, new[] { 1 });
		// Two garrison wanted, an infantry brigade spare in the same group: one is left.
		Assert.Equal(1, needed![2]);
		Assert.Equal(0, needed[1]);
	}

	[Fact]
	public void TheQueuesLinesAimedAtTheHeadquartersAreTakenOff()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CUnit headquarters = Headquarters(state);
		var line = new CConstruction(Ger) { Size = 2, Target = headquarters.Id };
		line.Brigades.Add(new CBrigadeOrder(state.SubUnitTypes["infantry_brigade"]));
		germany.Constructions.Add(line);
		var needs = new[] { 0, 5, 0, 0, 0 };
		CTheatreScreenText.Subtract(germany, headquarters, needs);
		Assert.Equal(3, needs[1]);
	}

	[Fact]
	public void NoNeedsAreSaidSo()
	{
		CGameState state = World();
		Assert.Equal("none", CTheatreScreenText.NeedsText(Text, state, null));
		Assert.Equal("none", CTheatreScreenText.NeedsText(Text, state, new int[5]));
		Assert.Equal("2 £garrison_brigade", CTheatreScreenText.NeedsText(Text, state, new[] { 0, 0, 2, 0, 0 }));
	}

	[Fact]
	public void BuildOrdersASquareRootsWorthOfLinesAimedAtTheHeadquarters()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		foreach (CSubUnitType type in state.SubUnitTypes.Values)
		{
			type.Flags[CUnitAvailability.Active] = true;
		}
		CUnit headquarters = Headquarters(state);
		var needs = new[] { 0, 0, 0, 9, 0 };
		IReadOnlyList<CCommand> commands = new CTheatreBuildSteps(state, germany, headquarters.Id, needs, () => 0u).Run();
		Assert.Equal(3, commands.Count);
		Assert.Equal(0, needs[3]);
		foreach (CCommand command in commands)
		{
			command.Execute(state);
		}
		Assert.All(germany.Constructions, line =>
		{
			Assert.Equal(headquarters.Id, line.Target);
			Assert.Equal(3, line.Size);
		});
	}
}
