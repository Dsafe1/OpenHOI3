using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Strategic warfare's allies part and the allies a country can count on, against <c>docs/mechanics/politics.md</c>,
/// section 6. Germany attacks Posen; Poland holds it and Warsaw behind it, France fights beside Poland.
/// </summary>
public class StrategicAlliesTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Fra = new("FRA");

	private const int Berlin = 1;
	private const int Posen = 2;
	private const int Warsaw = 3;

	private static CProvinceEdge Edge(int to) => new(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.One);

	private static CGameState State(int franceIc = 100, bool franceAtWar = true)
	{
		CCountry Country(CCountryTag tag, int capital, int ic, string faction) => new()
		{
			Tag = tag, Name = tag.ToString(), DefinitionFile = "", Capital = capital, ActingCapital = capital,
			BaseIc = ic, TotalIc = ic, Faction = faction,
		};
		CCountry germany = Country(Ger, Berlin, 100, "axis");
		CCountry poland = Country(Pol, Warsaw, 100, "allies");
		CCountry france = Country(Fra, Warsaw, franceIc, "allies");
		var defines = new CDefines();
		defines.Set(CManpower.MilitarySection, "STRAT_NO_ALLIES_HELP_PENALTY", CFixedPoint.Parse("-0.001"));
		defines.Set(CManpower.MilitarySection, "STRAT_ALLIES_HELP", CFixedPoint.Parse("0.1"));
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		if (franceAtWar)
		{
			war.Defenders.Add(Fra);
		}
		CProvince Land(int id, CCountryTag owner, params CProvinceEdge[] edges) =>
			new(id, id.ToString(), isSea: false) { Owner = owner, Controller = owner, Edges = edges };
		return new CGameState(new CDate(1939, 9, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				Land(Berlin, Ger, Edge(Posen)),
				Land(Posen, Pol, Edge(Berlin), Edge(Warsaw)),
				Land(Warsaw, Pol, Edge(Posen)),
			},
			new[] { germany, poland, france }, defines: defines, wars: new[] { war });
	}

	private static CUnit Division(CGameState state, CCountryTag owner, int province, int brigades)
	{
		var unit = new CUnit(EUnitKind.Division, owner) { Id = ++state.LastUnitId, Location = province };
		for (int i = 0; i < brigades; i++)
		{
			unit.SubUnits.Add(new CSubUnit(new CSubUnitType("infantry_brigade", EUnitBranch.Land)) { Id = ++state.LastUnitId, Unit = unit });
		}
		state.Country(owner)!.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	private static CCombat Battle(CGameState state, CUnit attacker, params CUnit[] defenders)
	{
		var combat = new CCombat(Posen);
		combat.Attacker.Add(attacker);
		foreach (CUnit defender in defenders)
		{
			combat.Defender.Add(defender);
		}
		return combat;
	}

	[Fact]
	public void AnAllyAtWarOnTheContinentWithHalfTheIcIsOneToCountOn()
	{
		CGameState state = State(franceIc: 50);
		Assert.Equal(new[] { Fra }, CCountryLists.StrongAlliesAtWar(state, state.Country(Pol)!).ConvertAll(c => c.Tag));

		CGameState weak = State(franceIc: 49);
		Assert.Empty(CCountryLists.StrongAlliesAtWar(weak, weak.Country(Pol)!));

		CGameState peaceful = State(franceAtWar: false);
		Assert.Empty(CCountryLists.StrongAlliesAtWar(peaceful, peaceful.Country(Pol)!));
	}

	[Fact]
	public void TheHoldersUnitsCostItAndItsStrongAlliesUnitsHelpByTheirBrigades()
	{
		CGameState state = State();
		CUnit german = Division(state, Ger, Berlin, 3);
		CUnit polish = Division(state, Pol, Posen, 3);
		CUnit french = Division(state, Fra, Posen, 2);
		CCombat combat = Battle(state, german, polish, french);

		CStrategicWarfareImpacts.AlliesFought(state, combat, combat.Defender);

		// Poland holds Posen, in the area of Warsaw: its own three brigades cost 0.003, France's two help 0.2.
		CStrategicWarfare warfare = state.Country(Pol)!.StrategicWarfare;
		Assert.Equal(new long[] { -3, 200 }, warfare.Allies.ConvertAll(entry => entry.Value));
		Assert.Empty(state.Country(Fra)!.StrategicWarfare.Allies);
	}

	[Fact]
	public void ACountryWithNobodyToCountOnGetsNoEntryAtAll()
	{
		CGameState state = State(franceAtWar: false);
		CUnit german = Division(state, Ger, Berlin, 3);
		CUnit polish = Division(state, Pol, Posen, 3);
		CCombat combat = Battle(state, german, polish);

		CStrategicWarfareImpacts.AlliesFought(state, combat, combat.Defender);

		Assert.Empty(state.Country(Pol)!.StrategicWarfare.Allies);
	}

	[Fact]
	public void AnAttackingSideWhoseFirstUnitIsNoFriendOfTheOwnerIsWeighedForTheProvincesHolder()
	{
		CGameState state = State();
		CUnit german = Division(state, Ger, Berlin, 3);
		CUnit polish = Division(state, Pol, Posen, 3);
		CCombat combat = Battle(state, german, polish);

		// Germany is no friend of Poland, so the holder is Posen's: Poland - whose units are not on this side.
		CStrategicWarfareImpacts.AlliesFought(state, combat, combat.Attacker);

		Assert.Empty(state.Country(Pol)!.StrategicWarfare.Allies);
		Assert.Empty(state.Country(Ger)!.StrategicWarfare.Allies);
	}
}
