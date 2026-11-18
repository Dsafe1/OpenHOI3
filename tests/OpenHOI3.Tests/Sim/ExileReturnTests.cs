using System;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>An exile's partisans becoming militia as it comes home, against <c>docs/mechanics/war.md</c>, section 14.</summary>
public class ExileReturnTests
{
	private static readonly CCountryTag Fra = new("FRA");

	private static CSubUnitType Type(string key, EUnitBranch branch, string supply, string strength, string organisation,
		params CTechnology[] technologies)
	{
		var type = new CSubUnitType(key, branch);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.Parse(strength));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.Parse(organisation));
		type.Stats.Values.Set(CUnitSupply.SupplyStatistic, CFixedPoint.Parse(supply));
		type.Technologies.AddRange(technologies);
		return type;
	}

	private static CTechnology Technology(string name) => new(name, 12, new CModifier(name));

	private sealed record CWorld(CGameState State, CCountry France, CSubUnitType Partisans, CSubUnitType Militia,
		CSubUnitType Infantry);

	private static CWorld World(string militiaName = "militia_brigade")
	{
		CTechnology smallArms = Technology("smallarms");
		CTechnology guerrilla = Technology("guerrilla_warfare");
		CTechnology militiaGuns = Technology("militia_smallarms");
		CSubUnitType partisans = Type("partisan_brigade", EUnitBranch.Land, "0", "30", "40", smallArms, guerrilla);
		CSubUnitType militia = Type("militia_brigade", EUnitBranch.Land, "0.3", "20", "25", smallArms, militiaGuns);
		CSubUnitType infantry = Type("infantry_brigade", EUnitBranch.Land, "0.67", "30", "30", smallArms);
		var france = new CCountry { Tag = Fra, Name = "France", DefinitionFile = "", Capital = 1, InExile = true, ActingCapital = 2 };
		var defines = new CDefines();
		defines.SetText("", CExile.MilitiaDefine, militiaName);
		var state = new CGameState(new CDate(1941, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Paris", isSea: false), new CProvince(2, "London", isSea: false) },
			new[] { france }, defines: defines)
		{
			SubUnitTypes = new Dictionary<string, CSubUnitType>(StringComparer.OrdinalIgnoreCase)
			{
				[partisans.Key] = partisans, [militia.Key] = militia, [infantry.Key] = infantry,
			},
		};
		return new CWorld(state, france, partisans, militia, infantry);
	}

	private static CSubUnit Brigade(CUnit unit, CSubUnitType type)
	{
		var brigade = new CSubUnit(type)
		{
			Unit = unit, Strength = type.Stats.MaxStrength, Organisation = type.Stats[CSubUnitType.DefaultOrganisation],
			Experience = CFixedPoint.FromInt(12),
		};
		brigade.Highest = brigade.Strength;
		unit.SubUnits.Add(brigade);
		return brigade;
	}

	[Fact]
	public void ComingHomeTurnsEveryBrigadeThatUsesNoSuppliesIntoTheDefinesMilitia()
	{
		CWorld world = World();
		var division = new CUnit(EUnitKind.Division, Fra) { Id = 1, Location = 2 };
		world.France.Units.Add(division);
		CSubUnit partisans = Brigade(division, world.Partisans);
		CSubUnit infantry = Brigade(division, world.Infantry);
		partisans.TechnologyLevels["smallarms"] = 2;
		partisans.TechnologyLevels["guerrilla_warfare"] = 3;
		partisans.TechnologyProgress["smallarms"] = CFixedPoint.Parse("0.4");

		CExile.Leave(world.State, world.France);

		Assert.Same(world.Militia, partisans.Type);
		Assert.Same(world.Infantry, infantry.Type);
		// The shared technology keeps its level and progress; the new type's other is at nothing, the old type's gone.
		Assert.Equal(2, partisans.TechnologyLevels["smallarms"]);
		Assert.Equal(0, partisans.TechnologyLevels["militia_smallarms"]);
		Assert.False(partisans.TechnologyLevels.ContainsKey("guerrilla_warfare"));
		Assert.Equal(CFixedPoint.Parse("0.4"), partisans.TechnologyProgress["smallarms"]);
		// Held to the militia's figures; experience kept.
		Assert.Equal(CFixedPoint.FromInt(20), partisans.Strength);
		Assert.Equal(CFixedPoint.FromInt(25), partisans.Organisation);
		Assert.Equal(CFixedPoint.Parse("0.3"), partisans.Stats[CUnitSupply.SupplyStatistic]);
		Assert.Equal(CFixedPoint.FromInt(12), partisans.Experience);
		Assert.False(world.France.InExile);
		Assert.Equal(1, world.France.ActingCapital);
	}

	[Fact]
	public void ANameThatIsNoTypeCaseAndAllConvertsNothing()
	{
		CWorld world = World("Militia_Brigade");
		var division = new CUnit(EUnitKind.Division, Fra) { Id = 1, Location = 2 };
		world.France.Units.Add(division);
		CSubUnit partisans = Brigade(division, world.Partisans);

		CExile.Leave(world.State, world.France);

		Assert.Same(world.Partisans, partisans.Type);
		Assert.False(world.France.InExile);
	}

	[Fact]
	public void ACountryNotInExileConvertsNothing()
	{
		CWorld world = World();
		world.France.InExile = false;
		var division = new CUnit(EUnitKind.Division, Fra) { Id = 1, Location = 2 };
		world.France.Units.Add(division);
		CSubUnit partisans = Brigade(division, world.Partisans);

		CExile.Leave(world.State, world.France);

		Assert.Same(world.Partisans, partisans.Type);
	}
}
