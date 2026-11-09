using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>What the unit panel's rows for a headquarters under an AI say, against <c>docs/mechanics/unitpanel.md</c>, section 9.</summary>
public class PlanRowsTests
{
	private static readonly CCountryTag Ger = new("GER");

	[Theory]
	[InlineData(EPlanStanceKind.Ground, 0, "STANCE_WITHDRAW")]
	[InlineData(EPlanStanceKind.Ground, 4, "STANCE_BLITZ")]
	[InlineData(EPlanStanceKind.Air, 0, "STANCE_AIR_PREPARE")]
	[InlineData(EPlanStanceKind.Naval, 2, "STANCE_NAVAL_ATTACK")]
	[InlineData(EPlanStanceKind.Air, 3, "")]
	[InlineData(EPlanStanceKind.Ground, 5, "")]
	public void AStanceHasItsKeyByItsKind(EPlanStanceKind kind, int stance, string key)
	{
		Assert.Equal(key, CPlanRowsText.StanceKey(kind, stance));
	}

	[Fact]
	public void TheGroundListHasFiveStancesAndTheOthersThree()
	{
		Assert.Equal(5, CPlanRowsText.StanceCount(EPlanStanceKind.Ground));
		Assert.Equal(3, CPlanRowsText.StanceCount(EPlanStanceKind.Air));
		Assert.Equal(3, CPlanRowsText.StanceCount(EPlanStanceKind.Naval));
	}

	[Theory]
	[InlineData(200_000, 100_000, 100, 50)]
	[InlineData(50_000, 200_000, 25, 100)]
	[InlineData(0, 0, 0, 0)]
	// Under 0.100 the scale is nought, and the raw thousandths cut to nothing.
	[InlineData(90, 40, 0, 0)]
	public void TheBarsAreEachSideOverTheLarger(long our, long their, int ourBar, int theirBar)
	{
		Assert.Equal((ourBar, theirBar), CPlanRowsText.PowerBars(our, their));
	}

	[Fact]
	public void OnlyAnAgentAboveAUnitGreysIt()
	{
		var army = new CUnit(EUnitKind.Army, Ger);
		var corps = new CUnit(EUnitKind.Corps, Ger) { Parent = army };
		var agent = new OpenHOI3.Sim.Agents.CAgent(Ger, 1);
		army.Agent = agent;
		Assert.False(CPlanRowsText.AgentAboveItsOwn(army));
		Assert.True(CPlanRowsText.AgentAboveItsOwn(corps));
	}

	[Theory]
	[InlineData(CUnitPlan.Withdraw, -1000)]
	[InlineData(CUnitPlan.Prepare, -500)]
	[InlineData(CUnitPlan.Defensive, 0)]
	[InlineData(CUnitPlan.Attacking, 500)]
	[InlineData(CUnitPlan.Blitzing, 1000)]
	public void AnActivePlanGivesAggressivenessByItsStanceFromAnyUnitAbove(int stance, long expected)
	{
		var army = new CUnit(EUnitKind.Army, Ger);
		army.Plan.Active = true;
		army.Plan.Stance = stance;
		var corps = new CUnit(EUnitKind.Corps, Ger) { Parent = army, HasOwnAggression = true, Aggression = new CFixedPoint(900) };
		Assert.Equal(expected, CUnitAggression.Of(corps));
	}

	[Fact]
	public void WithNoPlanTheFirstAggressionOfItsOwnGoesAndElseNought()
	{
		var army = new CUnit(EUnitKind.Army, Ger) { HasOwnAggression = true, Aggression = new CFixedPoint(-400) };
		var corps = new CUnit(EUnitKind.Corps, Ger) { Parent = army };
		Assert.Equal(-400, CUnitAggression.Of(corps));
		Assert.Equal(0, CUnitAggression.Of(new CUnit(EUnitKind.Corps, Ger)));
		Assert.Equal(0, CUnitAggression.Of(null));
	}

	[Theory]
	[InlineData(500, "§R*§!")]
	[InlineData(-500, "§B*§!")]
	[InlineData(100, "§g*§!")]
	[InlineData(-100, "§g*§!")]
	public void AParentsRowMarksAnAggressionOfItsOwn(long aggression, string mark)
	{
		var unit = new CUnit(EUnitKind.Army, Ger) { HasOwnAggression = true, Aggression = new CFixedPoint(aggression) };
		Assert.Equal(mark, CPlanRowsText.AggressionMark(unit));
		Assert.Equal("", CPlanRowsText.AggressionMark(new CUnit(EUnitKind.Army, Ger)));
	}
}
