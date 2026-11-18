using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The provinces a country holds, walked as a walk of the whole map testing each would give them: in the map's order,
/// by the tag or by its letters, and a change of hands on the way seen as that walk sees it.
/// </summary>
public class HeldProvincesTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static CGameState State(params CCountryTag?[] controllers)
	{
		var provinces = new List<CProvince> { new(0, "", isSea: false) };
		for (int i = 0; i < controllers.Length; i++)
		{
			provinces.Add(new CProvince(i + 1, $"p{i + 1}", isSea: false) { Owner = controllers[i], Controller = controllers[i] });
		}
		return new CGameState(new CDate(1936, 1, 1), provinces,
			new[]
			{
				new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" },
				new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" },
			});
	}

	private static int[] Ids(IEnumerable<CProvince> provinces) => provinces.Select(province => province.Id).ToArray();

	[Fact]
	public void AWalkGivesTheProvincesItHoldsInTheMapsOrder()
	{
		CGameState state = State(Ger, Pol, null, Ger, Pol, Ger);

		Assert.Equal(new[] { 1, 4, 6 }, Ids(CHeldProvinces.Walk(state, Ger)));
		Assert.Equal(new[] { 2, 5 }, Ids(CHeldProvinces.Walk(state, Pol)));
	}

	[Fact]
	public void ByItsLettersATagIsMatchedWithoutCase()
	{
		CGameState state = State(Ger, new CCountryTag("ger"), Pol);

		Assert.Equal(new[] { 1 }, Ids(CHeldProvinces.Walk(state, Ger)));
		Assert.Equal(new[] { 1, 2 }, Ids(CHeldProvinces.Walk(state, Ger, ignoringCase: true)));
	}

	[Fact]
	public void AProvinceChangingHandsOnTheWayIsSeenAsAWalkOfTheMapSeesIt()
	{
		CGameState state = State(Ger, Pol, Ger, Pol, Ger);
		var seen = new List<int>();

		foreach (CProvince province in CHeldProvinces.Walk(state, Ger))
		{
			seen.Add(province.Id);
			if (province.Id == 1)
			{
				// Germany takes the next province but one and loses the one after: both are seen as they now stand.
				state.Province(4).Controller = Ger;
				state.Province(5).Controller = Pol;
			}
		}

		Assert.Equal(new[] { 1, 3, 4 }, seen);
	}

	[Fact]
	public void TheProvincesAreGatheredAgainOnceOneHasChangedHands()
	{
		CGameState state = State(Ger, Pol);
		Assert.Equal(new[] { 1 }, Ids(CHeldProvinces.Walk(state, Ger)));

		state.Province(2).Controller = Ger;

		Assert.Equal(new[] { 1, 2 }, Ids(CHeldProvinces.Walk(state, Ger)));
	}
}
