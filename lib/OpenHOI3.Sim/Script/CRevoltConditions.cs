using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The conditions on revolts: a province's revolt risk, shown and pure, each strictly above the figure's whole part; and
/// how many of a country's provinces the rebels hold, as a count and as a share. See <c>docs/mechanics/revolt.md</c>,
/// section 5.
/// </summary>
public static class CRevoltConditions
{
	private const string Rebels = "REB";

	public static bool TryAnswer(CGameState state, CTrigger trigger, CEventScope scope, out bool answer)
	{
		CScriptArgument value = trigger.Argument;
		switch (trigger.Keyword)
		{
			case "revolt_risk":
				answer = scope.Province != 0
					&& CRevoltRisk.Shown(state, state.Province(scope.Province)) > CConditions.WholeOf(value.Amount) * CFixedPoint.Scale;
				return true;
			case "pure_revolt_risk":
				answer = scope.Province != 0
					&& CRevoltRisk.Effective(state, state.Province(scope.Province)) > CConditions.WholeOf(value.Amount) * CFixedPoint.Scale;
				return true;
			case "num_of_revolts":
				answer = state.Country(scope.Country) is { } country && Revolts(state, country) >= CConditions.WholeOf(value.Amount);
				return true;
			case "revolt_percentage":
				answer = state.Country(scope.Country) is { } whose && CCountryLists.OwnedCount(state, whose.Tag) is var owned and > 0
					&& Revolts(state, whose) * CFixedPoint.Scale / owned >= value.Amount.Raw;
				return true;
			default:
				answer = false;
				return false;
		}
	}

	/// <summary>How many provinces the country owns that the rebels hold.</summary>
	public static int Revolts(CGameState state, CCountry country)
	{
		int count = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner == country.Tag && province.Controller is { Value: Rebels })
			{
				count++;
			}
		}
		return count;
	}
}
