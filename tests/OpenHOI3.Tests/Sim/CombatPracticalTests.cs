using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Tests.Sim;

/// <summary>What a battle teaches, against <c>docs/mechanics/research.md</c>, section 10.</summary>
public class CombatPracticalTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Ita = new("ITA");

	private static CGameState State()
	{
		var countries = new List<CCountry>();
		foreach (CCountryTag tag in new[] { Ger, Pol, Ita })
		{
			countries.Add(new CCountry { Tag = tag, Name = tag.ToString(), DefinitionFile = "", Capital = 1 });
		}
		var defines = new CDefines();
		defines.Set("country", CResearch.MostAbility, CFixedPoint.FromInt(99));
		defines.Set("country", CResearch.AbilityGainDivisor, CFixedPoint.FromInt(10));
		defines.Set(CManpower.MilitarySection, CCombatPractical.LandIncrease, CFixedPoint.Parse("0.001"));
		defines.Set(CManpower.MilitarySection, CCombatPractical.NavalIncrease, CFixedPoint.Parse("0.01"));
		defines.SetText("", CCombatPractical.LandCategory, "land_doctrine_practical");
		defines.SetText("", CCombatPractical.NavalCategory, "naval_doctrine_practical");
		var categories = new[]
		{
			new CTechnologyCategory("land_doctrine_practical", false),
			new CTechnologyCategory("naval_doctrine_practical", false),
		};
		var provinces = new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Capital", isSea: false) };
		return new CGameState(new CDate(1936, 1, 1), provinces, countries, defines: defines, technologyCategories: categories);
	}

	[Fact]
	public void EveryCountryStillFightingOnASideGainsTheDefineAnHour()
	{
		CGameState state = State();
		var combat = new CCombat(1);
		combat.Attacker.AddCountry(Ger);
		combat.Attacker.AddCountry(Ita);

		CCombatPractical.Land(state, combat.Attacker);

		Assert.Equal(CFixedPoint.Parse("0.001"), state.Country(Ger)!.Ability("land_doctrine_practical"));
		Assert.Equal(CFixedPoint.Parse("0.001"), state.Country(Ita)!.Ability("land_doctrine_practical"));
		Assert.Equal(CFixedPoint.Zero, state.Country(Pol)!.Ability("land_doctrine_practical"));
	}

	[Fact]
	public void ACountryNoLongerStandingOnTheSideGainsNothing()
	{
		CGameState state = State();
		var combat = new CCombat(1);
		combat.Defender.AddCountry(Pol);
		combat.Defender.Standing.Clear();

		CCombatPractical.Land(state, combat.Defender);

		Assert.Equal(CFixedPoint.Zero, state.Country(Pol)!.Ability("land_doctrine_practical"));
	}

	[Fact]
	public void TheGainIsDiminishedAboveTheDivisorAsEveryGainIs()
	{
		CGameState state = State();
		state.Country(Ger)!.Abilities["naval_doctrine_practical"] = CFixedPoint.FromInt(20);
		var combat = new CCombat(1, ECombatKind.Naval);
		combat.Attacker.AddCountry(Ger);

		CCombatPractical.Naval(state, combat.Attacker);

		// Twice the divisor: half the hundredth.
		Assert.Equal(CFixedPoint.Parse("20.005"), state.Country(Ger)!.Ability("naval_doctrine_practical"));
	}

	[Fact]
	public void ANameThatIsNoCategoryCaseAndAllGivesNothing()
	{
		CGameState state = State();
		state.Defines.SetText("", CCombatPractical.LandCategory, "Land_Doctrine_Practical");
		var combat = new CCombat(1);
		combat.Attacker.AddCountry(Ger);

		CCombatPractical.Land(state, combat.Attacker);

		Assert.Equal(CFixedPoint.Zero, state.Country(Ger)!.Ability("land_doctrine_practical"));
	}
}
