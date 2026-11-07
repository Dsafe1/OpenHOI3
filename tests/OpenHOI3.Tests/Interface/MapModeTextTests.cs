using System;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the province's tooltip adds in the new map modes, against <c>docs/mechanics/mapmodes.md</c>. Germany is at war
/// with Poland; England is at peace with both. The localiser hands each key back in brackets, and one it has no text for
/// as itself, as the real one does.
///
///     Berlin (GER) -- Breslau (GER) -- Warszawa (POL) .......... London (ENG)
/// </summary>
public class MapModeTextTests
{
	private const int Berlin = 1;
	private const int Breslau = 2;
	private const int Warszawa = 3;
	private const int London = 4;
	private const int Sea = 5;

	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Eng = new("ENG");

	private static string Localise(string key) => key switch
	{
		"REGION_NAME" => "$NAME$ Region",
		"named_region" => "Named",
		"europe" => "europe",
		"unnamed_region" => key,
		_ => "[" + key + "]",
	};

	private static CProvince Land(int id, string name, CCountryTag owner, int x) =>
		new(id, name, isSea: false) { Owner = owner, Controller = owner, CentreX = x, CentreY = 100 };

	private static CGameState State(params CRegion[] regions)
	{
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		var state = new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false), Land(Berlin, "Berlin", Ger, 100), Land(Breslau, "Breslau", Ger, 140),
				Land(Warszawa, "Warszawa", Pol, 200), Land(London, "London", Eng, 900),
				new CProvince(Sea, "Sea", isSea: true) { CentreX = 120, CentreY = 150 },
			},
			new[]
			{
				new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" },
				new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" },
				new CCountry { Tag = Eng, Name = "England", DefinitionFile = "" },
			}, wars: new[] { war })
		{
			MapWidth = 1000,
			MapHeight = 1000,
			Regions = regions,
		};
		return state;
	}

	[Fact]
	public void VictoryPointsAreAlwaysSaid()
	{
		CGameState state = State();
		state.Province(Berlin).Points = 5;

		Assert.Equal("\n[PROV_POINTS]: 5", CMapModeText.VictoryPoints(Localise, state.Province(Berlin)));
		Assert.Equal("\n[PROV_POINTS]: 0", CMapModeText.VictoryPoints(Localise, state.Province(Sea)));
	}

	[Fact]
	public void IntelNamesTheLevelAndFullAtNine()
	{
		CGameState state = State();
		CProvinceIntel.Rebuild(state);

		Assert.Equal("\n[MAX_INTEL_LEVEL]", CMapModeText.Intel(Localise, state, state.Province(Berlin), Ger));
		Assert.Equal("\n[INTEL_NONE]", CMapModeText.Intel(Localise, state, state.Province(London), Ger));
		Assert.Equal("", CMapModeText.Intel(Localise, state, state.Province(Berlin), null));
	}

	[Fact]
	public void ResourcesListTheGoodsCrudeOilLastAndTheStrategicResourceAtAnyIntel()
	{
		CGameState state = State();
		CProvinceIntel.Rebuild(state);
		CProvince berlin = state.Province(Berlin);
		berlin.MaxProducing[EGoods.CrudeOil] = CFixedPoint.Parse("2");
		berlin.CurrentProducing[EGoods.CrudeOil] = CFixedPoint.Parse("1.25");
		berlin.MaxProducing[EGoods.Metal] = CFixedPoint.Parse("4.04");
		berlin.CurrentProducing[EGoods.Metal] = CFixedPoint.Parse("4.04");
		berlin.StrategicResource = "rubber";
		state.Province(London).MaxProducing[EGoods.Energy] = CFixedPoint.FromInt(3);
		state.Province(London).StrategicResource = "rubber";

		Assert.Equal("\n[METAL_IRO]: 4.0/4.0\n[CRUDE_OIL_IRO]: 1.3/2.0\n§G[rubber]§W",
			CMapModeText.Resources(Localise, state, berlin, Ger));
		Assert.Equal("\n§G[rubber]§W", CMapModeText.Resources(Localise, state, state.Province(London), Ger));
	}

	[Fact]
	public void TheInfrastructureIsAPercentTruncatedWithNoLabel()
	{
		CGameState state = State();
		CProvinceIntel.Rebuild(state);
		state.Province(Berlin).Modifier.Set("infrastructure", CFixedPoint.Parse("0.7"));
		state.Province(London).Modifier.Set("infrastructure", CFixedPoint.Parse("0.7"));

		Assert.Equal("\n70.00%", CMapModeText.Infrastructure(state, state.Province(Berlin), Ger));
		Assert.Equal("", CMapModeText.Infrastructure(state, state.Province(London), Ger));
	}

	[Fact]
	public void TheDiplomaticPartNamesTheOwnerAndTheFirstStatusThatHolds()
	{
		CGameState state = State();

		Assert.Equal("\n Poland: [ATWARWITHUS]", CMapModeText.Diplomatic(Localise, state, state.Province(Warszawa), Ger));
		Assert.Equal("\n England: [NEUTRAL]", CMapModeText.Diplomatic(Localise, state, state.Province(London), Ger));
		Assert.Equal("\n Germany: ", CMapModeText.Diplomatic(Localise, state, state.Province(Berlin), Ger));
		Assert.Equal("", CMapModeText.Diplomatic(Localise, state, state.Province(Sea), Ger));
	}

	[Fact]
	public void TheDiplomaticPartsVassalKeyIsShownToThePuppet()
	{
		CGameState state = State();
		state.Country(Eng)!.Overlord = Ger;

		Assert.Equal("\n Germany: [OURVASSAL]", CMapModeText.Diplomatic(Localise, state, state.Province(Berlin), Eng));
		Assert.Equal("\n England: [DIP_THEY_PUPPET]", CMapModeText.Diplomatic(Localise, state, state.Province(London), Ger));
	}

	[Fact]
	public void ARegionWithNoNameOfItsOwnIsNamedForItsFirstProvinceAndAPeaceRegionIsStarred()
	{
		CGameState state = State(new CRegion("named_region", new[] { Berlin }, false),
			new CRegion("unnamed_region", new[] { Breslau, Berlin }, true));
		CProvince berlin = new(Berlin, "Berlin", isSea: false) { Continent = "europe" };
		berlin.Regions.AddRange(new[] { 0, 1 });

		Assert.Equal("\n - Named\n - Breslau Region*\nEurope", CMapModeText.Region(Localise, state, berlin));
		// With the editor's continent painting on, the continent alone.
		Assert.Equal("\nEurope", CMapModeText.Region(Localise, state, berlin, continentPainting: true));
	}

	[Fact]
	public void TheNavalPartCountsTheWeeksBattlesFourWays()
	{
		CGameState state = State();
		Finished(state, Sea, Ger, Pol, convoy: true);
		Finished(state, Sea, Ger, Pol, convoy: true);
		Finished(state, Sea, Pol, Ger, convoy: true);
		Finished(state, Sea, Pol, Ger, convoy: false);
		Finished(state, Sea, null, Pol, convoy: false);
		Finished(state, Sea, Eng, Eng, convoy: false);

		Assert.Equal("\n[NAVAL_TOOLTIP_RAIDS]\n[NAVAL_TOOLTIP_CONVOYS_SUNK]\n[NAVAL_TOOLTIP_RAIDER_ATTACKED]"
			+ "\n[NAVAL_TOOLTIP_RAIDER_DEFENDED]", CMapModeText.Naval(Localise, state, state.Province(Sea), Ger));
		Assert.Equal("", CMapModeText.Naval(Localise, state, state.Province(Berlin), Ger));
	}

	private static void Finished(CGameState state, int province, CCountryTag? attacker, CCountryTag? defender, bool convoy)
	{
		var combat = new CCombat(province, ECombatKind.Naval) { IsConvoyBattle = convoy };
		if (attacker is { } first)
		{
			combat.Attacker.AddCountry(first);
		}
		if (defender is { } second)
		{
			combat.Defender.AddCountry(second);
		}
		state.CombatHistory.Record(combat, state.TotalHours);
	}
}
