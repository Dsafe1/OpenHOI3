using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Occupation;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country's occupation policy towards another set, by the politics screen's choice of a policy. It checks nothing -
/// the button's <c>allow</c> is the screen's - and a name no policy has sets <c>no_occupation</c>. See
/// <c>docs/mechanics/occupation.md</c>, section 2. An enhanced game's fix <c>occupation_policy_checked</c> has the
/// command ask the policy's <c>allow</c> as the button does, and refuse a name no policy has.
/// </summary>
public sealed class CChangeOccupationPolicyCommand : CCommand
{
	public CChangeOccupationPolicyCommand(CCountryTag which, CCountryTag target, string policy)
	{
		Which = which;
		Target = target;
		Policy = policy;
	}

	/// <summary>The country setting it.</summary>
	public CCountryTag Which { get; }

	/// <summary>The country it is set towards.</summary>
	public CCountryTag Target { get; }

	public string Policy { get; }

	public override bool MayExecute(CGameState state)
	{
		if (!state.Rules.Has(Rules.EFix.OccupationPolicyChecked))
		{
			return true;
		}
		return state.Country(Which) is { } which && state.Country(Target) is { } target
			&& COccupationPolicies.Find(state, Policy) is { } policy
			&& COccupationPolicies.IsAllowed(state, which, target, policy);
	}

	public override void Execute(CGameState state)
	{
		if (state.Country(Which) is { } which && state.Country(Target) is { } target)
		{
			COccupationPolicies.Change(state, which, target, COccupationPolicies.Find(state, Policy) ?? state.OccupationPolicies[0]);
		}
	}
}
