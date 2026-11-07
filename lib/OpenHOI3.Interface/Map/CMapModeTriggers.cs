using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// What makes the original colour a map mode again besides choosing it: a change of hands, which this project counts
/// already, and a change of the diplomatic statuses its refresh flag follows - a war declared, an alliance, a non-aggression
/// pact, military access, a faction joined or left. Here a figure of all of them, which changes when one does. See
/// <c>docs/mechanics/mapmodes.md</c>, section 1, and <c>reversing/findings/FINDINGS-mapmodesfigures.md</c>, section 1.
/// </summary>
public static class CMapModeTriggers
{
	public static long DiplomacyFigure(CGameState state)
	{
		long figure = 23;
		foreach (CWar war in state.Wars)
		{
			figure = figure * 31 + war.Name.GetHashCode();
			figure = figure * 31 + war.Attackers.Count;
			figure = figure * 31 + war.Defenders.Count;
		}
		figure = figure * 31 + state.Alliances.Count;
		figure = figure * 31 + state.Naps.Count;
		foreach (CCountry country in state.Countries)
		{
			figure = figure * 31 + country.Faction.GetHashCode();
			foreach (CCountry other in state.Countries)
			{
				if (other != country && CGameState.Status(country, other).HasMilitaryAccess)
				{
					figure = figure * 31 + other.Tag.GetHashCode();
				}
			}
		}
		return figure;
	}
}
