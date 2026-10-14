using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The effects on spies and on what a country knows: <c>docs/mechanics/espionage.md</c>, section 13, and
/// <c>docs/mechanics/intelligence.md</c>, section 6.
/// </summary>
public static class CEspionageEffects
{
	/// <summary>The level a local boost gives every unit it reaches.</summary>
	public const int LocalBoostLevel = 8;

	public static bool TryRun(CGameState state, CEffect effect, CEventScope scope)
	{
		switch (effect.Keyword)
		{
			case "modify_spies":
				// The original adds a count it never reads from the file, whatever its memory held; here nothing, and the
				// reading reports it (espionage.md, Ours).
				return true;
			case "local_intel_boost":
				LocalBoost(state, effect, scope);
				return true;
			default:
				return false;
		}
	}

	/// <summary>
	/// FROM given a grant of 8 on every unit, anybody's, in the provinces the scope province's controller controls within
	/// the distance written, and each such province asked for at the hour's end.
	/// </summary>
	private static void LocalBoost(CGameState state, CEffect effect, CEventScope scope)
	{
		if (scope.Province <= 0 || scope.Province >= state.Provinces.Count || scope.FromCountry is not { } from)
		{
			return;
		}
		CProvince centre = state.Province(scope.Province);
		if (centre.Controller is not { } controller)
		{
			return;
		}
		long value = effect.Argument.Amount.Raw;
		long radius = value * value / 1000 / 1000;
		int days = (int)(state.Defines["country", "SPY_MILITARY_INTEL_LOCAL_TIME_BASE"].Raw / 1000);
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller != controller || CMapDistance.Squared(province, centre, state.MapWidth) > radius)
			{
				continue;
			}
			foreach (CUnit unit in state.UnitsIn(province.Id))
			{
				CIntelGrants.Grant(unit, from, days, LocalBoostLevel);
			}
			CProvinceIntel.Ask(state, province.Id);
		}
	}
}
