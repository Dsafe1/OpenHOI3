using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The capital marker, against <c>docs/mechanics/mapunits.md</c>, section 9: on the province that is the acting capital
/// of its owner, at its city point. Berlin is Germany's, Lisbon Portugal's with a city of its own, Hamburg nobody's
/// capital and Danzig owned by nobody.
/// </summary>
public class MapCapitalsTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Por = new("POR");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Lisbon = 2;
	private const int Hamburg = 3;
	private const int Danzig = 4;

	// Lisbon's is Their Finest Hour's one city, 0.86 east and 3 south of its box's centre; the rest are their centres.
	private static readonly CMapPoint[] Cities =
	{
		default, new(2981f, 1728f), new(2541.8562f, 1475.0075f), new(2990f, 1745f), new(3060f, 1790f),
	};

	private static CGameState State(out CCountry germany, out CCountry portugal, out CCountry poland)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		portugal = new CCountry { Tag = Por, Name = "Portugal", DefinitionFile = "", Capital = Lisbon };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = Danzig };
		return new CGameState(new CDate(1936, 1, 1), new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
			new CProvince(Lisbon, "Lisbon", isSea: false) { Owner = Por, Controller = Por },
			new CProvince(Hamburg, "Hamburg", isSea: false) { Owner = Ger, Controller = Ger },
			new CProvince(Danzig, "Danzig", isSea: false),
		}, new[] { germany, portugal, poland });
	}

	[Fact]
	public void EachCountrysActingCapitalHasAMarkerAtItsCityPoint()
	{
		CGameState state = State(out _, out _, out _);

		List<CMapCapital> capitals = CMapCapitals.Build(state, Cities);

		Assert.Equal(new[] { (Berlin, Ger), (Lisbon, Por) }, capitals.Select(capital => (capital.Province, capital.Owner)));
		// Where the file gives a city, there; and nothing is cut to whole pixels.
		Assert.Equal((2541.8562f, 1475.0075f), (capitals[1].X, capitals[1].Y));
		Assert.Equal((2981f, 1728f), (capitals[0].X, capitals[0].Y));
	}

	[Fact]
	public void TheMarkerFollowsTheActingCapitalAndNotTheDeclaredOne()
	{
		CGameState state = State(out CCountry germany, out _, out _);

		germany.ActingCapital = Hamburg;

		// In the order of the provinces.
		Assert.Equal(new[] { Lisbon, Hamburg }, CMapCapitals.Build(state, Cities).Select(capital => capital.Province));
	}

	[Fact]
	public void AnOccupiedCapitalKeepsItsOwnersMarker()
	{
		CGameState state = State(out _, out _, out _);

		state.Province(Lisbon).Controller = Ger;

		Assert.Contains(CMapCapitals.Build(state, Cities), capital => capital.Province == Lisbon && capital.Owner == Por);
	}

	[Fact]
	public void AGovernmentSittingInAnotherCountrysProvinceShowsNoMarker()
	{
		CGameState state = State(out _, out CCountry portugal, out _);

		// Portugal's government in Hamburg, which is Germany's - as an exile in its host's capital would be.
		portugal.ActingCapital = Hamburg;

		Assert.Equal(new[] { Berlin }, CMapCapitals.Build(state, Cities).Select(capital => capital.Province));
	}

	[Fact]
	public void AProvinceWithNoOwnerOrNoControllerShowsNoMarker()
	{
		CGameState state = State(out _, out _, out _);

		// Danzig, Poland's capital, is owned by nobody.
		Assert.DoesNotContain(CMapCapitals.Build(state, Cities), capital => capital.Province == Danzig);

		state.Province(Berlin).Controller = null;
		Assert.DoesNotContain(CMapCapitals.Build(state, Cities), capital => capital.Province == Berlin);
	}

	[Fact]
	public void WhatThePlayerKnowsDoesNotMatter()
	{
		CGameState state = State(out _, out _, out _);

		// Nobody knows anything of Lisbon; the builder asks no player at all.
		Assert.Equal(0, state.Province(Lisbon).Intel.Count(level => level > 0));
		Assert.Contains(CMapCapitals.Build(state, Cities), capital => capital.Province == Lisbon);
	}
}
