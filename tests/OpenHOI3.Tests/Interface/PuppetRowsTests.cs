using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Interface;

/// <summary>The politics screen's rows for making a puppet and liberating a country, against <c>docs/mechanics/politics.md</c>, section 11.1.</summary>
public class PuppetRowsTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Aus = new("AUS");
	private static readonly CCountryTag Pol = new("POL");

	private static string Localise(string key) => key switch
	{
		"VASSALPROV" => "They will receive:",
		"VASSALPROVNUM" => "They will receive $NUMBER$ provinces.",
		_ => key,
	};

	private static (CGameState State, CCountry Germany, CCountry Austria, CCountry Poland) World(int coresHeld = 2)
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 };
		var austria = new CCountry { Tag = Aus, Name = "Austria", DefinitionFile = "", Capital = 2 };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = 3 };
		var provinces = new List<CProvince>
		{
			new(0, "", isSea: false),
			new(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
		};
		for (int id = 2; id < 2 + coresHeld; id++)
		{
			var province = new CProvince(id, id == 2 ? "Wien" : "Linz", isSea: false) { Owner = Ger, Controller = Ger };
			province.Cores.Add(Aus);
			provinces.Add(province);
		}
		return (new CGameState(new CDate(1936, 1, 1), provinces, new[] { germany, austria, poland }), germany, austria, poland);
	}

	[Fact]
	public void ALandlessCountryWithACoreHeldAndItsCapitalOwnedIsAPossiblePuppet()
	{
		(CGameState state, CCountry germany, CCountry austria, _) = World();
		Assert.Equal(new[] { austria }, CCountryLists.PossibleVassals(state, germany));
	}

	[Fact]
	public void LandOnlyOccupiedOffersNoPuppet()
	{
		(CGameState state, CCountry germany, _, _) = World();
		foreach (CProvince province in state.Provinces.Where(province => province.Cores.Contains(Aus)))
		{
			province.Owner = Pol;
		}
		Assert.Empty(CCountryLists.PossibleVassals(state, germany));
	}

	[Theory]
	[InlineData(2, "They will receive: Wien, Linz")]
	[InlineData(3, "They will receive: Wien, Linz, Linz")]
	[InlineData(4, "They will receive 4 provinces.")]
	public void ARowNamesUnderFourProvincesAndCountsTheRest(int cores, string line)
	{
		(CGameState state, CCountry germany, CCountry austria, _) = World(cores);
		Assert.Equal(line, CPuppetRowsText.ProvincesLine(Localise, state, germany, austria));
	}

	[Fact]
	public void AnExileWhoseCapitalIsHeldAndIsNoEnemyMayBeLiberated()
	{
		(CGameState state, CCountry germany, _, CCountry poland) = World();
		poland.InExile = true;
		state.Province(2).Owner = Pol;
		poland.Capital = 2;
		Assert.Equal(new[] { poland }, CCountryLists.PossibleLiberations(state, germany));
		poland.InExile = false;
		Assert.Empty(CCountryLists.PossibleLiberations(state, germany));
	}

	[Fact]
	public void TheCommandRefusesACountryWithLand()
	{
		(CGameState state, _, _, _) = World();
		state.Province(2).Owner = Aus;
		Assert.False(new CCreateVassalCommand(Aus, Ger).MayExecute(state));
	}

	[Fact]
	public void LiberatingNeedsAnExile()
	{
		(CGameState state, _, _, CCountry poland) = World();
		Assert.False(new CLiberateCountryCommand(Pol, Ger).MayExecute(state));
		poland.InExile = true;
		Assert.True(new CLiberateCountryCommand(Pol, Ger).MayExecute(state));
	}

	[Fact]
	public void LiberatingGivesBackLandHeldByTheSameFactionAndEndsTheExile()
	{
		(CGameState state, CCountry germany, _, CCountry poland) = World();
		germany.Faction = "axis";
		poland.Faction = "axis";
		poland.InExile = true;
		state.Province(2).Owner = Pol;
		new CLiberateCountryCommand(Pol, Ger).Execute(state);
		Assert.Equal(Pol, state.Province(2).Controller);
		Assert.Equal(Ger, state.Province(3).Controller);
		Assert.False(poland.InExile);
		Assert.Null(poland.Overlord);
	}

	[Fact]
	public void LiberatingAcrossFactionsMakesAPuppet()
	{
		(CGameState state, CCountry germany, _, CCountry poland) = World();
		poland.InExile = true;
		poland.Faction = "allies";
		new CLiberateCountryCommand(Pol, Ger).Execute(state);
		Assert.Equal(Ger, poland.Overlord);
		Assert.Equal(200, CRelationsOf(germany, poland));
	}

	private static long CRelationsOf(CCountry a, CCountry b) => OpenHOI3.Sim.Diplomacy.CRelations.Of(a, b).Raw / CFixedPoint.Scale;
}
