using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Occupation;

/// <summary>
/// The policy every country holds towards every country: what sets it - a capture, the player, the history - and what
/// a province is given of it. See <c>docs/mechanics/occupation.md</c>, sections 1 to 3.
/// </summary>
public static class COccupationPolicies
{
	/// <summary>The game's own policy, number 0, held where nothing has set one.</summary>
	public const string NoOccupation = "no_occupation";

	/// <summary>The policy a capture looks for by name before it falls back on the file's first.</summary>
	public const string CivilWar = "civil_war";

	private const string Rebels = "REB";

	/// <summary>The list a game holds before its file is read: <c>no_occupation</c> alone.</summary>
	public static List<COccupationPolicy> WithNull()
	{
		return new List<COccupationPolicy> { new(new CModifier(NoOccupation), 0) };
	}

	/// <summary>The policy of that name, <c>no_occupation</c> among them, or null.</summary>
	public static COccupationPolicy? Find(CGameState state, string name)
	{
		foreach (COccupationPolicy policy in state.OccupationPolicies)
		{
			if (policy.Name == name)
			{
				return policy;
			}
		}
		return null;
	}

	/// <summary>The policy <paramref name="holder"/> holds towards <paramref name="other"/>.</summary>
	public static COccupationPolicy Of(CGameState state, CCountry holder, CCountry other)
	{
		int index = CGameState.Status(holder, other).OccupationPolicy;
		return index > 0 && index < state.OccupationPolicies.Count ? state.OccupationPolicies[index] : state.OccupationPolicies[0];
	}

	/// <summary>Whether <paramref name="country"/> may choose it towards <paramref name="occupied"/>: its <c>allow</c>, with the occupied as FROM.</summary>
	public static bool IsAllowed(CGameState state, CCountry country, CCountry occupied, COccupationPolicy policy)
	{
		return policy.Allow is not { } allow
			|| CConditions.All(state, allow, CEventScope.Start(country.Tag, fromCountry: occupied.Tag), null);
	}

	/// <summary>
	/// A province changing controller, before the new one is written: where the new controller holds
	/// <c>no_occupation</c> towards the old, <c>civil_war</c> if its <c>allow</c> holds, else the file's first policy.
	/// A policy already set is kept.
	/// </summary>
	public static void Capture(CGameState state, CCountryTag? taker, CCountryTag? from)
	{
		if (state.Country(taker) is not { } country || state.Country(from) is not { } old || state.OccupationPolicies.Count < 2)
		{
			return;
		}
		Diplomacy.CDiplomacyStatus status = CGameState.Status(country, old);
		if (status.OccupationPolicy > 0)
		{
			return;
		}
		if (Find(state, CivilWar) is { } civilWar && IsAllowed(state, country, old, civilWar))
		{
			status.OccupationPolicy = civilWar.Index;
			return;
		}
		status.OccupationPolicy = 1;
	}

	/// <summary>The history's lines, each set with no <c>allow</c> asked; one towards a country there is not passes.</summary>
	public static void SetFromHistory(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach ((string name, CCountryTag tag) in country.HistoryOccupationPolicies)
			{
				if (Find(state, name) is { } policy && state.Country(tag) is { } occupied)
				{
					CGameState.Status(country, occupied).OccupationPolicy = policy.Index;
				}
			}
		}
	}

	/// <summary>
	/// The player's command: the policy set, checking nothing, then the provinces' modifiers made again and the setter's
	/// own. Nothing is told.
	/// </summary>
	public static void Change(CGameState state, CCountry which, CCountry target, COccupationPolicy policy)
	{
		CGameState.Status(which, target).OccupationPolicy = policy.Index;
		// Ours: every province is made again, where the original makes again those the setter holds of the other's; the
		// rest come out as they were.
		CProvinceModifiers.Rebuild(state);
		CCountryModifiers.RebuildOne(state, which);
	}

	/// <summary>
	/// What a province is given of occupation: its controller's policy towards its owner - none where its owner holds it,
	/// where the rebels do, or where either is not a country.
	/// </summary>
	public static COccupationPolicy? Applied(CGameState state, CProvince province)
	{
		if (province.Owner is not { } owner || province.Controller is not { } controller || owner == controller
			|| controller.Value == Rebels || state.Country(controller) is not { } holder || state.Country(owner) is not { } owning)
		{
			return null;
		}
		return Of(state, holder, owning);
	}
}
