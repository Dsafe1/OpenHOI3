using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The country's IC, against <c>docs/mechanics/ic.md</c> section 3. One industry
/// building worth 1 IC a level, and one that scales a province's IC by a quarter a level
/// in whichever direction the test needs.
/// </summary>
public class IndustrialCapacityTests
{
	private static readonly CBuilding Industry = new(0, "industry", 10, "ic", CFixedPoint.One);
	private static readonly CBuilding Boost = new(1, "boost", 10, "local_ic", CFixedPoint.Parse("0.25"));
	private static readonly CBuilding Blight = new(2, "blight", 10, "local_ic", CFixedPoint.Parse("-2"));
	private static readonly CBuilding[] Buildings = { Industry, Boost, Blight };

	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Fra = new("FRA");

	private static CCountry Country(string tag)
	{
		return new CCountry { Tag = new CCountryTag(tag), Name = tag, DefinitionFile = "" };
	}

	private static CProvince Province(int id, CCountryTag? controller, string industry, CCountryTag? owner = null)
	{
		var province = new CProvince(id, $"P{id}", isSea: false)
		{
			Owner = owner ?? controller,
			Controller = controller,
		};
		province.SetBuildingLevel(Industry, CFixedPoint.Parse(industry));
		return province;
	}

	private static CGameState State(params CProvince[] provinces)
	{
		var all = new List<CProvince> { new(0, "", isSea: false) };
		all.AddRange(provinces);
		return new CGameState(new CDate(1936, 1, 1), all,
			new[] { Country("GER"), Country("FRA"), Country("LUX") }, Buildings);
	}

	[Fact]
	public void TheBaseIsTheSumOfTheProvincesTruncatedToWholeIc()
	{
		CGameState state = State(
			Province(1, Ger, "3"),
			Province(2, Ger, "2.6"),
			Province(3, Fra, "4"));

		CProvinceModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);

		Assert.Equal(5, state.Country(Ger)!.BaseIc);            // 5.6, and the fraction is dropped
		Assert.Equal(5, state.Country(Ger)!.TotalIc);
		Assert.Equal(4, state.Country(Fra)!.BaseIc);
	}

	[Fact]
	public void FractionsAddUpBeforeTheyAreDropped()
	{
		CGameState state = State(Province(1, Ger, "0.5"), Province(2, Ger, "0.5"), Province(3, Ger, "0.9"));

		CProvinceModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);

		Assert.Equal(1, state.Country(Ger)!.BaseIc);            // 1.9 in all, not three zeroes
	}

	[Fact]
	public void AProvinceCountsForWhoeverControlsIt()
	{
		CGameState state = State(Province(1, Ger, "6", owner: Fra));

		CProvinceModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);

		Assert.Equal(6, state.Country(Ger)!.BaseIc);
		Assert.Equal(0, state.Country(Fra)!.BaseIc);
	}

	[Fact]
	public void AProvincesLocalIcScalesItsOwnContribution()
	{
		CProvince boosted = Province(1, Ger, "4");
		boosted.SetBuildingLevel(Boost, CFixedPoint.FromInt(1));    // a quarter more
		CGameState state = State(boosted, Province(2, Ger, "4"));

		CProvinceModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);

		Assert.Equal(9, state.Country(Ger)!.BaseIc);            // 4 * 1.25 + 4
	}

	[Fact]
	public void AContributionThatIsNotPositiveIsLeftOutRatherThanSubtracted()
	{
		CProvince blighted = Province(1, Ger, "4");
		blighted.SetBuildingLevel(Blight, CFixedPoint.FromInt(1));  // 4 * (1 - 2) is -4
		CGameState state = State(blighted, Province(2, Ger, "3"));

		CProvinceModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);

		Assert.Equal(3, state.Country(Ger)!.BaseIc);
	}

	[Fact]
	public void EveryCountryHasAtLeastOneIc()
	{
		CGameState state = State(Province(1, Ger, "3"));

		CProvinceModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);

		CCountry luxembourg = state.Country(new CCountryTag("LUX"))!;
		Assert.Equal(0, luxembourg.BaseIc);                     // it holds nothing
		Assert.Equal(1, luxembourg.TotalIc);                    // and still has one
	}

	private static CLaw Law(string name, string globalIc)
	{
		var modifier = new CModifier(name);
		modifier.Set("global_ic", CFixedPoint.Parse(globalIc));
		return new CLaw("economic_law", modifier);
	}

	[Fact]
	public void TheCountrysGlobalIcScalesTheBaseAndTheResultIsTruncated()
	{
		CGameState state = State(Province(1, Ger, "9"), Province(2, Fra, "9"));
		state.Country(Ger)!.Laws["economic_law"] = Law("full_civilian_economy", "-0.5");
		state.Country(Fra)!.Laws["economic_law"] = Law("war_economy", "0.25");

		CTick.RunEntryPass(state);

		Assert.Equal(9, state.Country(Ger)!.BaseIc);            // the base is untouched
		Assert.Equal(4, state.Country(Ger)!.TotalIc);           // 4.5, truncated
		Assert.Equal(11, state.Country(Fra)!.TotalIc);          // 11.25, truncated
	}

	[Fact]
	public void TheBasesFractionIsLostBeforeTheBonusIsApplied()
	{
		// 3.9 of province IC and a bonus of a third: 3 * 1.333 is 3, where 3.9 * 1.333 would be 5.
		CGameState state = State(Province(1, Ger, "3.9"));
		state.Country(Ger)!.Laws["economic_law"] = Law("boom", "0.333");

		CTick.RunEntryPass(state);

		Assert.Equal(3, state.Country(Ger)!.TotalIc);
	}

	[Fact]
	public void APenaltyOfMoreThanEverythingStillLeavesOneIc()
	{
		CGameState state = State(Province(1, Ger, "9"));
		state.Country(Ger)!.Laws["economic_law"] = Law("ruin", "-1.5");

		CTick.RunEntryPass(state);

		Assert.Equal(1, state.Country(Ger)!.TotalIc);
	}

	[Fact]
	public void ACountrysModifierIsRebuiltNotAddedTo()
	{
		CGameState state = State(Province(1, Ger, "10"));
		CCountry germany = state.Country(Ger)!;
		germany.Laws["economic_law"] = Law("war_economy", "0.25");

		CTick.RunEntryPass(state);
		CTick.RunEntryPass(state);                              // a second rebuild must not double it
		Assert.Equal(CFixedPoint.Parse("0.25"), germany.Modifier["global_ic"]);
		Assert.Equal(12, germany.TotalIc);

		germany.Laws["economic_law"] = Law("full_civilian_economy", "-0.5");
		CTick.RunEntryPass(state);
		Assert.Equal(CFixedPoint.Parse("-0.5"), germany.Modifier["global_ic"]);   // the old law left nothing behind
		Assert.Equal(5, germany.TotalIc);

		germany.Laws.Clear();
		CTick.RunEntryPass(state);
		Assert.Equal(CFixedPoint.Zero, germany.Modifier["global_ic"]);

		// What is left is what its province has, which a country's modifier takes in whole.
		Assert.Equal(CFixedPoint.FromInt(10), germany.Modifier["ic"]);
	}

	[Fact]
	public void SeaAndUnheldProvincesBelongToNobody()
	{
		CGameState state = State(Province(1, null, "8"), Province(2, Ger, "2"));

		CProvinceModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);

		Assert.Equal(2, state.Country(Ger)!.BaseIc);
	}

	[Fact]
	public void IsWorkedOutOnEnteringAGameAndAgainEveryMidnight()
	{
		CProvince contested = Province(1, Fra, "7");
		CGameState state = State(contested, Province(2, Ger, "2"));
		var simulation = new CSimulation(state);

		Assert.Equal(2, state.Country(Ger)!.TotalIc);           // before any hour has passed
		Assert.Equal(7, state.Country(Fra)!.TotalIc);

		// A test may reach into the state; a game would change hands through a command.
		contested.Controller = Ger;
		simulation.RunHours(23);
		Assert.Equal(2, state.Country(Ger)!.TotalIc);           // not until the day turns

		simulation.RunHours(1);
		Assert.Equal(9, state.Country(Ger)!.TotalIc);
		Assert.Equal(1, state.Country(Fra)!.TotalIc);           // nothing left, so the floor
	}
}
