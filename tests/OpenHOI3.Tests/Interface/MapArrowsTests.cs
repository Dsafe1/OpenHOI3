using System.Collections.Generic;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// Which arrow a selected unit's way is drawn with, against
/// <c>docs/mechanics/movement.md</c>, section 7.
/// </summary>
public class MapArrowsTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Posen = 2;
	private const int Stettin = 3;

	private static CGameState State(bool atWar)
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = Posen };
		var wars = new List<CWar>();
		if (atWar)
		{
			var war = new CWar("war");
			war.Attackers.Add(Ger);
			war.Defenders.Add(Pol);
			wars.Add(war);
		}
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(Posen, "Posen", isSea: false) { Owner = Pol, Controller = Pol },
				new CProvince(Stettin, "Stettin", isSea: false) { Owner = Ger, Controller = Ger },
			},
			new[] { germany, poland }, wars: wars);
	}

	private static CUnit Marching(int to)
	{
		var unit = new CUnit(EUnitKind.Division, Ger) { Id = 1, Location = Berlin };
		unit.Path.Add(to);
		return unit;
	}

	[Fact]
	public void A_march_over_one_s_own_ground_has_the_march_s_arrow()
	{
		Assert.Equal(CArrowTypes.Move, CMapArrows.TypeOf(State(atWar: true), Marching(Stettin)));
	}

	[Fact]
	public void A_way_into_ground_an_enemy_holds_has_the_attack_s_arrow_whoever_stands_there()
	{
		Assert.Equal(CArrowTypes.Attack, CMapArrows.TypeOf(State(atWar: true), Marching(Posen)));
	}

	[Fact]
	public void Ground_held_by_a_country_at_peace_is_no_attack()
	{
		Assert.Equal(CArrowTypes.Move, CMapArrows.TypeOf(State(atWar: false), Marching(Posen)));
	}

	[Fact]
	public void A_unit_falling_back_has_the_retreat_s_arrow_before_anything_else()
	{
		CUnit unit = Marching(Posen);
		unit.IsRetreating = true;

		Assert.Equal(CArrowTypes.Retreat, CMapArrows.TypeOf(State(atWar: true), unit));
	}
}
