using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Attrition as the original works it out (docs/mechanics/upkeep.md, section 3a): a brigade's figure in a province, term by
/// term, and the day's loss it makes.
/// </summary>
public class AttritionTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Breslau = 2;
	private const int Posen = 3;

	private static CProvinceEdge Edge(int to) => new(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.One);

	private static CProvince Land(int id, string name, CCountryTag owner, params CProvinceEdge[] edges)
	{
		var province = new CProvince(id, name, isSea: false) { Owner = owner, Controller = owner, Edges = edges };
		province.Modifier.Set(CAttrition.Modifier, CFixedPoint.One);              // the base game's land_province
		province.Modifier.Set(CAttrition.Infrastructure, CFixedPoint.One);        // whole
		return province;
	}

	private static CGameState State(out CCountry germany, out CCountry poland, bool atWar = true)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = Posen };
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
			new[] { germany, poland }, defines: defines, wars: atWar ? new[] { war } : null,
			buildings: CTestRoads.With(new CBuilding[0])).Paved();
	}

	private static CSubUnit Brigade(CCountry country, int province)
	{
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		type.Stats.Values.Set("max_strength", CFixedPoint.FromInt(30));
		var unit = new CUnit(EUnitKind.Division, country.Tag) { Id = 1 + country.Units.Count, Location = province };
		var brigade = new CSubUnit(type) { Unit = unit, Strength = CFixedPoint.FromInt(30), Highest = CFixedPoint.FromInt(30) };
		brigade.Stats.CopyFrom(type.Stats);
		unit.SubUnits.Add(brigade);
		country.Units.Add(unit);
		return brigade;
	}

	[Fact]
	public void OnAnEnemysGroundTheWholeOfItIsTaken()
	{
		CGameState state = State(out CCountry germany, out _);
		CSubUnit brigade = Brigade(germany, Posen);

		// The province's own 1, its infrastructure whole, no weather: 1.000.
		Assert.Equal(1000, CAttrition.Of(state, state.Province(Posen), brigade));

		// Half its infrastructure standing adds what is missing.
		state.Province(Posen).Modifier.Set(CAttrition.Infrastructure, CFixedPoint.Parse("0.5"));
		Assert.Equal(1500, CAttrition.Of(state, state.Province(Posen), brigade));

		// None standing is counted as a hundredth.
		state.Province(Posen).Modifier.Set(CAttrition.Infrastructure, CFixedPoint.Zero);
		Assert.Equal(1990, CAttrition.Of(state, state.Province(Posen), brigade));
	}

	[Fact]
	public void AtHomeItIsNothingButAtTheFrontATenth()
	{
		CGameState state = State(out CCountry germany, out _);
		CSubUnit atFront = Brigade(germany, Berlin);
		CSubUnit behind = Brigade(germany, Breslau);

		Assert.Equal(100, CAttrition.Of(state, state.Province(Berlin), atFront));      // Posen beside it, at war
		Assert.Equal(0, CAttrition.Of(state, state.Province(Breslau), behind));

		CGameState peace = State(out CCountry germanyAtPeace, out _, atWar: false);
		Assert.Equal(0, CAttrition.Of(peace, peace.Province(Berlin), Brigade(germanyAtPeace, Berlin)));
	}

	[Fact]
	public void TheWeatherTheGroundAndTheTechnologyCount()
	{
		CGameState state = State(out CCountry germany, out _);
		CSubUnit brigade = Brigade(germany, Posen);
		CProvince posen = state.Province(Posen);

		// Cold at or below the threshold adds the whole, less the country's winter effects.
		posen.Weather.Temperature = CFixedPoint.FromInt(-25);
		germany.Modifier.Set(CAttrition.WinterEffects, CFixedPoint.Parse("0.25"));
		Assert.Equal(1750, CAttrition.Of(state, posen, brigade));

		// The terrain's own and its adjuster for the brigade, where above nothing.
		posen.Weather.Temperature = CFixedPoint.FromInt(10);
		posen.Terrain = new CTerrain("forest", false, new CColor(0, 0, 0)) { Attrition = CFixedPoint.Parse("0.5") };
		var adjuster = new CModifier("forest");
		adjuster.Set(CAttrition.Modifier, CFixedPoint.Parse("-0.2"));
		brigade.Stats.Adjusters["forest"] = adjuster;
		Assert.Equal(1300, CAttrition.Of(state, posen, brigade));

		// A technology's maximum_attrition cuts the whole.
		germany.Technology.Set(CAttrition.Technology, CFixedPoint.Parse("-0.1"));
		Assert.Equal(1170, CAttrition.Of(state, posen, brigade));
	}

	[Fact]
	public void ADaysLossIsAThirtiethOfAHundredthOfTheFigureTimesTheFullStrength()
	{
		CGameState state = State(out CCountry germany, out _);
		CSubUnit brigade = Brigade(germany, Posen);

		// 1.000 x 0.010 x 30 / 30 = 0.010.
		CAttrition.ApplyDaily(state, germany, brigade.Unit!);
		Assert.Equal(CFixedPoint.Parse("29.99"), brigade.Strength);

		// Not while a game is being made, nor in a battle.
		state.IsBeingMade = true;
		CAttrition.ApplyDaily(state, germany, brigade.Unit!);
		Assert.Equal(CFixedPoint.Parse("29.99"), brigade.Strength);
	}
}
