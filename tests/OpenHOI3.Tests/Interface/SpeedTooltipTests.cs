using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim.Movement;

namespace OpenHOI3.Tests.Interface;

/// <summary>The speed tooltip's breakdown of the multiplier, term by term (<c>docs/mechanics/unitpanel.md</c>, section 10.2).</summary>
public class SpeedTooltipTests
{
	private static string Localise(string key) => key + " $PERC$";

	[Fact]
	public void EachTermNotOneIsALineOfItsOwnInTheOriginalsOrder()
	{
		var terms = new CSpeedTerms(Combat: 330, Fuel: 1000, Terrain: 750, Trait: 1000, Adjacency: 1000, Weather: 900,
			LocalModifier: 1000, MobileBonus: 1100, Retreating: 1000);

		Assert.Equal(
			"SPEED_MODIFIER §R20.40%§!" +
			"\n- SPEED_MODIFIER_COMBAT §R33.00%§!" +
			"\n- SPEED_MODIFIER_TERRAIN §R75.00%§!" +
			"\n- SPEED_MODIFIER_WEATHER §R90.00%§!" +
			"\n- SPEED_MODIFIER_MOTORIZED_BONUS §G110.00%§!",
			CUnitStatusTooltips.SpeedModifier(Localise, 204, terms));
	}
}
