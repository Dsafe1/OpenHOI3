using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the reorganise window writes, against <c>docs/mechanics/reorganise.md</c>, section 2, and
/// screenshots of the original's: "2,999" and "100%", "2 / 7 / 0" and a penalty in thousandths.
/// </summary>
public class ReorgTextTests
{
	private static CSubUnit Brigade(CSubUnitType type, string strength)
	{
		var brigade = new CSubUnit(type) { Id = 1, Strength = CFixedPoint.Parse(strength) };
		brigade.Stats.CopyFrom(type.Stats);
		return brigade;
	}

	private static CSubUnitType Ship(string key, params string[] flags)
	{
		var type = new CSubUnitType(key, EUnitBranch.Naval);
		foreach (string flag in flags)
		{
			type.Flags[flag] = true;
		}
		return type;
	}

	[Fact]
	public void A_brigade_shows_its_men_cut_to_a_whole_man_and_a_ship_a_whole_percentage()
	{
		Assert.Equal("2,999", CReorgText.Strength(Brigade(new CSubUnitType("waffen_brigade", EUnitBranch.Land), "29.996")));
		Assert.Equal("1,000", CReorgText.Strength(Brigade(new CSubUnitType("engineer_brigade", EUnitBranch.Land), "10")));
		Assert.Equal("100%", CReorgText.Strength(Brigade(Ship("heavy_cruiser"), "100")));
	}

	[Fact]
	public void The_ratio_counts_capitals_screens_and_transports_and_a_submarine_nowhere()
	{
		var fleet = new CUnit(EUnitKind.Navy, new CCountryTag("GER"));
		CSubUnitType heavy = Ship("heavy_cruiser", CShipCensus.CapitalFlag);
		CSubUnitType light = Ship("light_cruiser");
		CSubUnitType submarine = Ship("submarine", CShipCensus.SubmarineFlag);
		foreach (CSubUnitType type in new[] { heavy, heavy, light, light, light, light, light, light, light, submarine })
		{
			fleet.SubUnits.Add(Brigade(type, "100"));
		}

		Assert.Equal("Units Ratio: §G2§! / §G7§! / §G0§!", CReorgText.Ratio(_ => "Units Ratio: $RAT$", fleet));
	}

	[Fact]
	public void The_positioning_penalty_is_printed_in_thousandths_without_its_hundredfold_green_only_at_nought()
	{
		var germany = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false) }, new[] { germany });
		state.Defines.Set(CManpower.MilitarySection, CNavalBattle.HullLimit, CFixedPoint.FromInt(10));
		state.Defines.Set(CManpower.MilitarySection, CNavalBattle.HullPenalty, CFixedPoint.Parse("0.005"));
		state.Defines.Set(CManpower.MilitarySection, CNavalBattle.MostPenalty, CFixedPoint.FromInt(1));
		var fleet = new CUnit(EUnitKind.Navy, germany.Tag);
		CSubUnitType cruiser = Ship("heavy_cruiser");
		cruiser.Stats.Values.Set("hull", CFixedPoint.FromInt(5));
		fleet.SubUnits.Add(Brigade(cruiser, "100"));
		string Text(string key) => "Positioning Penalty: $VAL$";

		Assert.Equal("Positioning Penalty: §G0.000%§!", CReorgText.Positioning(Text, state, fleet));
		// 20 of hull is 10 over: a penalty of 0.050, which the unit panel would show as 5%.
		fleet.SubUnits.Add(Brigade(cruiser, "100"));
		fleet.SubUnits.Add(Brigade(cruiser, "100"));
		fleet.SubUnits.Add(Brigade(cruiser, "100"));
		Assert.Equal("Positioning Penalty: §R0.050%§!", CReorgText.Positioning(Text, state, fleet));
	}
}
