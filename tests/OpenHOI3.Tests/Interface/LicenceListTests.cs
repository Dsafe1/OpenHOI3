using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The licence dialog's comparison of two countries' versions of a type and its greying for want of men, against
/// <c>docs/mechanics/aid.md</c>, section 6.
/// </summary>
public class LicenceListTests
{
	private static CSubUnitStats Stats(params (string Statistic, string Value)[] values)
	{
		var stats = new CSubUnitStats();
		foreach ((string statistic, string value) in values)
		{
			stats.Values.Set(statistic, CFixedPoint.Parse(value));
		}
		return stats;
	}

	private static CSubUnitType Infantry()
	{
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		type.Stats.CopyFrom(Stats(("soft_attack", "3"), ("hard_attack", "1"), ("ap_attack", "2"), ("toughness", "7"),
			("defensiveness", "8"), ("armor_value", "1"), ("softness", "0.9"), ("build_cost_manpower", "3.33")));
		var smallArms = new CTechnology("smallarms", 12, new CModifier("smallarms"));
		smallArms.UnitEffects[type.Key] = Stats(("soft_attack", "0.5"));
		type.Technologies.Add(smallArms);
		return type;
	}

	private static CCountry Country(string tag, int level)
	{
		var country = new CCountry { Tag = new CCountryTag(tag), Name = tag, DefinitionFile = "" };
		country.TechnologyLevels["smallarms"] = level;
		return country;
	}

	[Fact]
	public void ALandTypesCombatValueIsItsAttacksATenthOfItsDefencesAndItsArmourByTwoLessItsSoftness()
	{
		CSubUnitType type = Infantry();
		// (3 + 1 + 2 + trunc(15 / 10) + 1) x (2 - 0.9) = 8.5 x 1.1
		Assert.Equal(9350, CLicenceText.CombatValue(Country("POL", 0), type));
		// Two levels of small arms add a point of soft attack: 9.5 x 1.1
		Assert.Equal(10450, CLicenceText.CombatValue(Country("GER", 2), type));
	}

	[Fact]
	public void AnAirTypeAddsItsAttackAndDefenceToAHalfAndAShipTakesItsHull()
	{
		var wing = new CSubUnitType("interceptor", EUnitBranch.Air);
		wing.Stats.CopyFrom(Stats(("air_attack", "4"), ("air_defence", "3")));
		Assert.Equal(7500, CLicenceText.CombatValue(Country("GER", 0), wing));

		var ship = new CSubUnitType("destroyer", EUnitBranch.Naval);
		ship.Stats.CopyFrom(Stats(("sea_attack", "2"), ("sea_defence", "1"), ("hull", "0.7")));
		Assert.Equal(2450, CLicenceText.CombatValue(Country("GER", 0), ship));      // 3.5 x 0.7
	}

	[Fact]
	public void ARowsDelayedTooltipIsTheSellersVersionsFiguresWithNoPlusSigns()
	{
		CSubUnitType type = Infantry();
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new OpenHOI3.Sim.Provinces.CProvince(0, "", isSea: false) },
			new[] { Country("GER", 2) });
		string figures = CLicenceText.RowFigures(key => key, state, state.Countries[0], type);

		// Two levels of small arms on three: the seller's soft attack is four, written green and with no plus.
		Assert.Contains("SOFT_ATTACK §G4.00§W", figures);
		Assert.Contains("HARD_ATTACK §G1.00§W", figures);
		Assert.Contains("SOFTNESS §R0.90§W", figures);
		Assert.DoesNotContain("+", figures);
	}

	[Fact]
	public void ABoxIsGreyedWithTooFewMenFreedWithMoreAndLeftAsItIsWithExactlyEnough()
	{
		CSubUnitType type = Infantry();
		CCountry buyer = Country("POL", 0);
		long one = CLicenceText.ManpowerOfOne(buyer, type).Raw;

		buyer.Manpower = new CFixedPoint(one * 6 - 1);
		Assert.True(CLicenceText.GreyedAfter(false, buyer, type, 3, 2));
		buyer.Manpower = new CFixedPoint(one * 6 + 1);
		Assert.False(CLicenceText.GreyedAfter(true, buyer, type, 3, 2));
		buyer.Manpower = new CFixedPoint(one * 6);
		Assert.True(CLicenceText.GreyedAfter(true, buyer, type, 3, 2));
		Assert.False(CLicenceText.GreyedAfter(false, buyer, type, 3, 2));
	}
}
