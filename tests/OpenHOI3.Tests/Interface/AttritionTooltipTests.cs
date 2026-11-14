using System.Collections.Generic;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The unit panel's attrition broken into its causes, as the original's tooltip writes it (docs/mechanics/unitpanel.md,
/// section 10.2): the ground's friend or foe, the province's sources, each cause times the base, the technology's cut and the
/// figure.
/// </summary>
public class AttritionTooltipTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Breslau = 2;
	private const int Posen = 3;

	private static readonly Dictionary<string, string> Texts = new()
	{
		["IN_FRIENDLY_FOR_ATT"] = "Friendly.\n",
		["IN_FRONT_PROV_ONLY_10"] = "Front.\n",
		["IN_HOSTILE_FOR_ATT"] = "Hostile.\n",
		["land_province"] = "Land",
		["TERRAIN_ATTRITION"] = "Attrition: ",
	};

	private static string Localise(string key) => Texts.TryGetValue(key, out string? text) ? text : key;

	private static CProvinceEdge Edge(int to) => new(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.One);

	private static CProvince Land(int id, string name, CCountryTag owner, params CProvinceEdge[] edges)
	{
		var province = new CProvince(id, name, isSea: false) { Owner = owner, Controller = owner, Edges = edges };
		var land = new CModifier("land_province");
		land.Set(CAttrition.Modifier, CFixedPoint.One);
		province.Modifier.Add(land);
		province.ModifierSources.Add(new CModifierSource(land));
		province.Modifier.Set(CAttrition.Infrastructure, CFixedPoint.One);
		return province;
	}

	private static CGameState State(out CCountry germany)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = Posen };
		var defines = new CDefines();
		defines.Set(CAttrition.WeatherSection, CAttrition.HighTemperature, CFixedPoint.FromInt(30));
		defines.Set(CAttrition.WeatherSection, CAttrition.LowTemperature, CFixedPoint.FromInt(-20));
		defines.Set(CAttrition.WeatherSection, CAttrition.Wind, CFixedPoint.FromInt(60));
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				Land(Berlin, "Berlin", Ger, Edge(Breslau), Edge(Posen)),
				Land(Breslau, "Breslau", Ger, Edge(Berlin)),
				Land(Posen, "Posen", Pol, Edge(Berlin)),
			},
			new[] { germany, poland }, defines: defines, wars: new[] { war },
			buildings: CTestRoads.With(new CBuilding[0])).Paved();
	}

	private static CSubUnit Brigade(CCountry country, int province, EUnitBranch branch = EUnitBranch.Land)
	{
		var type = new CSubUnitType("infantry_brigade", branch);
		var unit = new CUnit(EUnitKind.Division, country.Tag) { Id = 1 + country.Units.Count, Location = province };
		var brigade = new CSubUnit(type) { Unit = unit, Strength = CFixedPoint.FromInt(30), Highest = CFixedPoint.FromInt(30) };
		unit.SubUnits.Add(brigade);
		country.Units.Add(unit);
		return brigade;
	}

	[Fact]
	public void OnAnEnemysGroundEveryCauseIsCountedWhole()
	{
		CGameState state = State(out CCountry germany);
		CSubUnit brigade = Brigade(germany, Posen);
		CProvince posen = state.Province(Posen);
		posen.Modifier.Set(CAttrition.Infrastructure, CFixedPoint.Parse("0.5"));
		posen.Weather.Temperature = CFixedPoint.FromInt(-25);
		germany.Modifier.Set(CAttrition.WinterEffects, CFixedPoint.Parse("0.25"));
		posen.Terrain = new CTerrain("forest", false, new CColor(0, 0, 0)) { Attrition = CFixedPoint.Parse("0.5") };
		var adjuster = new CModifier("forest");
		adjuster.Set(CAttrition.Modifier, CFixedPoint.Parse("-0.2"));
		brigade.Stats.Adjusters["forest"] = adjuster;
		germany.Technology.Set(CAttrition.Technology, CFixedPoint.Parse("-0.1"));

		// (1 + 0.5 + 0.75 + 0.3) x 0.9 = 2.295, written to two places cut.
		Assert.Equal("Hostile.\n\nLand: §R+1.00§W\nATTR_INFRA: §R+0.50§!\nATTR_WEATHER: §R+0.75§!\nforest: §R+0.30§!\n"
			+ "\nATTR_TECH: §G-10.00%§!\n\n§YAttrition: §!§R+2.29%§!",
			CUnitStatusTooltips.Attrition(Localise, state, posen, brigade));
	}

	[Fact]
	public void AtTheFrontEachCauseIsATenthAndBehindItTheFriendlyLineAlone()
	{
		CGameState state = State(out CCountry germany);

		Assert.Equal("Front.\n\nLand: §R+0.10§W\n\n§YAttrition: §!§R+0.10%§!",
			CUnitStatusTooltips.Attrition(Localise, state, state.Province(Berlin), Brigade(germany, Berlin)));
		Assert.Equal("Friendly.\n", CUnitStatusTooltips.Attrition(Localise, state, state.Province(Breslau), Brigade(germany, Breslau)));
	}

	[Fact]
	public void AWeatherOrGroundOfNothingOrLessIsLeftOutAndANavalBrigadeHasNone()
	{
		CGameState state = State(out CCountry germany);
		CSubUnit brigade = Brigade(germany, Posen);
		CProvince posen = state.Province(Posen);
		posen.Weather.Temperature = CFixedPoint.FromInt(-25);
		germany.Modifier.Set(CAttrition.WinterEffects, CFixedPoint.Parse("1.5"));
		posen.Terrain = new CTerrain("plains", false, new CColor(0, 0, 0)) { Attrition = CFixedPoint.Parse("-0.5") };

		// The weather's -0.5 is held at nothing in the figure and not written; the ground's is not added.
		Assert.Equal("Hostile.\n\nLand: §R+1.00§W\n\n§YAttrition: §!§R+1.00%§!",
			CUnitStatusTooltips.Attrition(Localise, state, posen, brigade));
		Assert.Equal("", CUnitStatusTooltips.Attrition(Localise, state, posen, Brigade(germany, Posen, EUnitBranch.Naval)));
	}
}
