using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Espionage;

/// <summary>
/// Lower and raise national unity, which are modifiers and not a day's work: each country's spies in this one on either
/// mission give it <c>spy_lower_national_unity</c> or <c>spy_raise_national_unity</c>, scaled by their level and then by
/// the mission's share. See <c>docs/mechanics/espionage.md</c>, section 10.
/// </summary>
public static class CSpyModifiers
{
	public const string Lower = "spy_lower_national_unity";
	public const string Raise = "spy_raise_national_unity";

	/// <summary>The modifiers on <paramref name="country"/>, in the order of the countries whose spies give them.</summary>
	public static IEnumerable<CModifier> On(CGameState state, CCountry country)
	{
		CModifier? lower = state.FindStaticModifier(Lower);
		CModifier? raise = state.FindStaticModifier(Raise);
		CCountryFacts facts = CCountryFacts.Of(state);
		foreach (CCountry other in state.Countries)
		{
			if (facts.OwnedCount(other.Tag) <= 0)
			{
				continue;
			}
			CSpyPresence presence = CGameState.Presence(other, country);
			if (presence.Level <= 0)
			{
				continue;
			}
			if (lower is not null && presence.Weight(ESpyMission.LowerNationalUnity) > 0)
			{
				yield return Scaled(lower, presence, ESpyMission.LowerNationalUnity);
			}
			if (raise is not null && presence.Weight(ESpyMission.RaiseNationalUnity) > 0)
			{
				yield return Scaled(raise, presence, ESpyMission.RaiseNationalUnity);
			}
		}
	}

	/// <summary>Every value times the level and then times the share, each product truncated.</summary>
	private static CModifier Scaled(CModifier modifier, CSpyPresence presence, ESpyMission mission)
	{
		CModifier byLevel = CCountryModifiers.Scaled(modifier, new CFixedPoint(presence.Level));
		return CCountryModifiers.Scaled(byLevel, new CFixedPoint(presence.Share(mission)));
	}
}
