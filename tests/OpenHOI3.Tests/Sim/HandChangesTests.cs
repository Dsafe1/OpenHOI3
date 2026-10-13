using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A province counts its own changes of owner and controller in the game it belongs to, so nothing that
/// keeps a picture of who holds what can be left looking at an old map by a writer that forgot.
/// </summary>
public class HandChangesTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static (CGameState State, CProvince Posen) World()
	{
		var none = new CProvince(0, "", isSea: false);
		var berlin = new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger };
		var posen = new CProvince(2, "Posen", isSea: false) { Owner = Pol, Controller = Pol };
		// Read as the history is, before there is a game: nothing to count it in.
		posen.Controller = Ger;
		posen.Controller = Pol;
		var countries = new[]
		{
			new CCountry { Tag = Ger, Name = "GER", DefinitionFile = "", Capital = 1 },
			new CCountry { Tag = Pol, Name = "POL", DefinitionFile = "", Capital = 2 },
		};
		return (new CGameState(new CDate(1936, 1, 1), new[] { none, berlin, posen }, countries), posen);
	}

	[Fact]
	public void EachRealChangeCountsOnce()
	{
		(CGameState state, CProvince posen) = World();
		Assert.Equal(0, state.ControlChanges);

		posen.Controller = Ger;
		Assert.Equal(1, state.ControlChanges);
		posen.Controller = Ger;
		Assert.Equal(1, state.ControlChanges);
		posen.Owner = Ger;
		Assert.Equal(2, state.ControlChanges);
		posen.Owner = null;
		Assert.Equal(3, state.ControlChanges);
	}

	[Fact]
	public void AnOccupiedProvinceTakenOverIsSeenWithinTheHour()
	{
		(CGameState state, CProvince posen) = World();
		posen.Controller = Ger;
		Assert.Equal(1, CCountryFacts.Of(state).OwnedCount(Ger));

		// Germany already holds it, so only the owner moves - which once counted nothing.
		CProvinceHands.ChangeOwner(state, posen, Ger);

		Assert.Equal(2, CCountryFacts.Of(state).OwnedCount(Ger));
		Assert.Equal(0, CCountryFacts.Of(state).OwnedCount(Pol));
	}
}
