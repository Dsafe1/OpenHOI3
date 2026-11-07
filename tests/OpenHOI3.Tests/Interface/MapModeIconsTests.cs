using System;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The naval and resources map modes' icons, against <c>docs/mechanics/mapmodes.md</c>, sections 5 and 11. Germany is at war
/// with Poland; England is at peace with both.
/// </summary>
public class MapModeIconsTests
{
	private const int Berlin = 1;
	private const int Sea = 2;
	private const int OtherSea = 3;

	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Eng = new("ENG");

	private static readonly CBuilding[] Buildings =
	{
		// Their Finest Hour's first six.
		new(0, "air_base", 10, "", CFixedPoint.Zero), new(1, "naval_base", 10, "", CFixedPoint.Zero),
		new(2, "coastal_fort", 10, "", CFixedPoint.Zero), new(3, "land_fort", 10, "", CFixedPoint.Zero),
		new(4, "anti_air", 10, "", CFixedPoint.Zero), new(5, "radar_station", 10, "", CFixedPoint.Zero),
	};

	private static CGameState State()
	{
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		return new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger, CentreX = 100, CentreY = 50 },
				new CProvince(Sea, "Sea", isSea: true), new CProvince(OtherSea, "Other", isSea: true),
			},
			new[]
			{
				new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" },
				new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" },
				new CCountry { Tag = Eng, Name = "England", DefinitionFile = "" },
			}, buildings: Buildings, wars: new[] { war });
	}

	private static void Finished(CGameState state, int province, CCountryTag? attacker, CCountryTag? defender, bool convoy,
		ECombatKind kind = ECombatKind.Naval)
	{
		var combat = new CCombat(province, kind) { IsConvoyBattle = convoy };
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

	[Fact]
	public void TheNavalFlagsSayWhetherNotHowMany()
	{
		CGameState state = State();
		Finished(state, Sea, Ger, Pol, convoy: true);
		Finished(state, Sea, Ger, Pol, convoy: true);
		Finished(state, Sea, Pol, Ger, convoy: false);
		Finished(state, OtherSea, Pol, Ger, convoy: true);
		Finished(state, OtherSea, Ger, Pol, convoy: false);
		// A naval bombing is not counted, and nor is a battle between two others at peace with the player.
		Finished(state, Berlin, Pol, Ger, convoy: false, ECombatKind.NavalBombing);
		Finished(state, Berlin, Eng, Eng, convoy: false);

		CNavalMapFlags[] flags = CNavalMapIcons.Flags(state, Ger);

		Assert.Equal(new CNavalMapFlags(ConvoysSunk: true, ConvoysRaided: false, RaiderDefended: true, RaiderAttacked: false),
			flags[Sea]);
		Assert.Equal(new CNavalMapFlags(ConvoysSunk: false, ConvoysRaided: true, RaiderDefended: false, RaiderAttacked: true),
			flags[OtherSea]);
		Assert.Equal(default, flags[Berlin]);
		Assert.Equal(default, CNavalMapIcons.Flags(state, null)[Sea]);
	}

	[Fact]
	public void AGoodsIconStandsOnTheBuildingWhosePlaceIsTheGoodsLessTheOriginalsNobuilding()
	{
		CGameState state = State();
		CProvince berlin = state.Province(Berlin);
		berlin.MaxProducing[EGoods.CrudeOil] = CFixedPoint.One;
		berlin.MaxProducing[EGoods.RareMaterials] = CFixedPoint.One;
		berlin.StrategicResource = "rubber";
		var placed = new[]
		{
			Array.Empty<CBuildingPosition>(),
			new[] { new CBuildingPosition("coastal_fort", 10f, 20f), new CBuildingPosition("land_fort", 30f, 40f) },
			Array.Empty<CBuildingPosition>(), Array.Empty<CBuildingPosition>(),
		};
		var icons = CResourceMapIcons.Build(state, placed);

		Assert.Equal(new[]
		{
			// Crude oil on the 3rd building of the file, coastal_fort; rare materials on the 6th, radar_station, with no point of
			// its own, on the middle of the province's box, as the strategic resource always is.
			new CResourceMapIcon(Berlin, "resource_oil", 10f, 20f),
			new CResourceMapIcon(Berlin, "resource_raremat", 100.5f, 50.5f),
			new CResourceMapIcon(Berlin, "STRATEGIC_rubber", 100.5f, 50.5f),
		}, icons);
	}
}
