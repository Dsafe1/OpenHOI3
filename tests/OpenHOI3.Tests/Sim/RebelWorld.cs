using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The ground rebels are tried on, all land with roads enough to be in an area:
///
///     1 Berlin --- 2 Frankfurt --- 3 Posen --- 4 Warsaw --- 5 Lodz --- 6 Krakow
///
/// Germany owns 1 to 3 and Poland 4 to 6, at war, Germany holding Warsaw. Posen is a core of Germany and of Slovakia,
/// which owns nothing and has its capital there. Three rebel types as Their Finest Hour has them: partisans where the
/// owner does not hold the province, nationalists freeing somebody and the rabble where it does. The provinces stand
/// fifty pixels apart in a row, and the underground building and an air base are in the game, with Their Finest Hour's
/// underground defines.
/// </summary>
internal static class RebelWorld
{
	public static readonly CCountryTag Ger = new("GER");
	public static readonly CCountryTag Pol = new("POL");
	public static readonly CCountryTag Slo = new("SLO");
	public static readonly CCountryTag Reb = new("REB");
	public static readonly CBuilding Roads = new(0, "infra", 10, CSupplyFigures.Infrastructure, CFixedPoint.Parse("0.1"));
	public static readonly CBuilding Factory = new(1, "industry", 10, "ic", CFixedPoint.One);
	public static readonly CBuilding Underground = new(2, "underground", 1, CUndergrounds.Modifier, CFixedPoint.One) { IsRepaired = false };
	public static readonly CBuilding AirBase = new(3, CBaseCrowding.AirBase, 10, "air_capacity", CFixedPoint.One);
	public static readonly CTechnology Smallarms = new("militia_smallarms", 10, new CModifier("militia_smallarms"));

	public static CGameState State()
	{
		CCountry Country(CCountryTag tag, int capital, string adjective) =>
			new() { Tag = tag, Name = tag.Value, Adjective = adjective, DefinitionFile = "", Capital = capital, OriginalCapital = capital };
		CCountry germany = Country(Ger, 1, "German");
		germany.TechnologyLevels[Smallarms.Name] = 3;
		germany.Modifier.Set(CRebelArmies.Efficiency, CFixedPoint.FromInt(2));

		CProvince Land(int id, CCountryTag owner, params int[] beside)
		{
			var province = new CProvince(id, $"p{id}", isSea: false) { Owner = owner, Controller = owner, Manpower = CFixedPoint.Parse("2.5"), CentreX = id * 50 };
			province.Edges = System.Array.ConvertAll(beside, to => new CProvinceEdge(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.One));
			province.SetBuildingLevel(Roads, CFixedPoint.FromInt(10));
			province.Cores.Add(owner);
			return province;
		}
		CProvince posen = Land(3, Ger, 2, 4);
		posen.Cores.Add(Slo);
		posen.SetBuildingLevel(Factory, CFixedPoint.One);
		CProvince warsaw = Land(4, Pol, 3, 5);
		warsaw.Controller = Ger;

		var partisan = new CSubUnitType("partisan_brigade", EUnitBranch.Land);
		partisan.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(30));
		partisan.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(20));
		partisan.Technologies.Add(Smallarms);
		var infantry = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		infantry.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(100));
		infantry.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(30));

		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		var state = new CGameState(new CDate(1939, 10, 1),
			new[]
			{
				new CProvince(0, "", isSea: false), Land(1, Ger, 2), Land(2, Ger, 1, 3), posen, warsaw, Land(5, Pol, 4, 6),
				Land(6, Pol, 5),
			},
			new[] { germany, Country(Pol, 5, "Polish"), Country(Slo, 3, "Slovak"), Country(Reb, 0, "Partisans") },
			buildings: new[] { Roads, Factory, Underground, AirBase }, technologies: new[] { Smallarms }, wars: new[] { war })
		{
			EndDate = new CDate(1948, 1, 1),
			SubUnitTypes = new Dictionary<string, CSubUnitType> { [partisan.Key] = partisan, [infantry.Key] = infantry },
			NameTexts = new CUnitNameTexts { Brigade = "$NUM$$ORDER$ $TYPE$ Brigade", Ordinals = new[] { "th", "st", "nd", "rd" } },
		};
		foreach ((string name, string value) in new[]
		{
			("SPAWN_PARTISAN_LIMIT", "0.5"), ("SPAWN_UNDERGROUND_LIMIT", "1"), ("MAX_UNDERGROUND_DISTANCE", "1000"),
			("UNDERGOUND_INITIAL_STRENGTH", "0.4"), ("UNDERGROUND_STRENGTH_GAIN", "0.01"), ("UNDERGROUND_DETECT_CHANCE", "2"),
			("UNDERGROUND_SPAWN_STRENGTH", "0.2"),
		})
		{
			state.Defines.Set(CUndergrounds.Section, name, CFixedPoint.Parse(value));
		}
		state.RebelTypes = new[]
		{
			Type("partisans", 0, 10, "controlled_by = owner", independence: false, autoConvert: true, "$COUNTRY$ Partisans"),
			Type("nationalist_rebels", 1, 10, "NOT = { controlled_by = owner }", independence: true, autoConvert: false,
				"$INDEP$ Nationalists"),
			Type("disgruntled_rabble", 2, 1, "NOT = { controlled_by = owner }", independence: false, autoConvert: false, "Rabble"),
		};
		return state;
	}

	private static CRebelType Type(string name, int index, int factor, string veto, bool independence, bool autoConvert,
		string title)
	{
		var type = new CRebelType(name, index)
		{
			Independence = independence,
			AutoConvert = autoConvert,
			Unit = "partisan_brigade",
			DefectDelay = 24,
			NameText = title,
			ArmyText = "$COUNTRY$ " + name,
			DescText = name + " rise",
		};
		type.SpawnChance.Days = factor;
		type.SpawnChance.Modifiers.Add(new CMeanTimeToHappenModifier(CFixedPoint.Zero, ScriptWorld.Condition(veto)));
		type.MovementEvaluation.Days = 1;
		return type;
	}
}
