using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The effects on what countries hold of each other: <c>docs/mechanics/diplomacy.md</c>, section 7.
/// Where the original would write its <c>---</c> placeholder country, nothing here changes.
/// </summary>
public static class CDiplomacyEffects
{
	public static bool TryRun(CGameState state, CEffect effect, CEventScope scope)
	{
		switch (effect.Keyword)
		{
			case "create_alliance":
				// No check of any kind: a second one is a second alliance (agreements.md, section 4).
				if (state.Country(scope.Country) is { } actor && state.Country(CEffects.Target(state, effect, scope)) is { } recipient)
				{
					Agreements.CPacts.MakeAlliance(state, actor, recipient, state.TotalHours);
					Politics.CLaws.OnDiplomacyChanged(state, actor);
					Politics.CLaws.OnDiplomacyChanged(state, recipient);
				}
				return true;
			case "undeclared_war":
				UndeclaredWar(state, effect, scope);
				return true;
			case "relation":
				// who's relation with the scope's country, written both ways; who = THIS is the scope itself.
				if (CEffects.Field(effect, "who") is { } who
					&& state.Country(CEffects.Target(state, who, scope)) is { } holder
					&& state.Country(scope.Country) is { } other)
				{
					CFixedPoint value = CEffects.Field(effect, "value")?.Amount ?? CFixedPoint.Zero;
					CRelations.Change(holder, other, value);
				}
				return true;
			case "threat":
				// who (or which) = TAG: TAG's threat from the scope, unweighed; who = all: spread to everyone.
				if (state.Country(scope.Country) is { } feared
					&& (CEffects.Field(effect, "who") ?? CEffects.Field(effect, "which")) is { } whom)
				{
					long value = CConditions.WholeOf(CEffects.Field(effect, "value")?.Amount ?? CFixedPoint.Zero) * 1000;
					if (whom.IsWord("all"))
					{
						CThreat.SpreadToAll(state, Math.Abs(value), value < 0 ? CThreat.Takes : CThreat.Adds, feared);
					}
					else if (state.Country(CEventScope.Known(whom.Tag)) is { } fearing)
					{
						CThreat.Add(fearing, feared, value);
					}
				}
				return true;
			case "neutrality":
				if (state.Country(scope.Country) is { } country && effect.Argument.Amount != CFixedPoint.Zero)
				{
					CNeutrality.Change(state, country, effect.Argument.Amount);
				}
				return true;
			default:
				return false;
		}
	}

	/// <summary>The region added to the war between the scope and its target, or the controller of the scope's province (war.md, section 15).</summary>
	private static void UndeclaredWar(CGameState state, CEffect effect, CEventScope scope)
	{
		CScriptArgument? target = CEffects.Field(effect, "target");
		CCountryTag? defender = target is not null ? CEffects.Target(state, target, scope) : state.Province(scope.Province).Controller;
		if (scope.Country is not { } attacker || defender is not { } other || CEffects.Field(effect, "region") is not { } region)
		{
			return;
		}
		Wars.CUndeclaredWars.AddRegion(state, attacker, other, region.Text);
		foreach (CCountryTag tag in new[] { attacker, other })
		{
			if (state.Country(tag) is { } country)
			{
				Politics.CLaws.OnDiplomacyChanged(state, country);
			}
		}
	}
}
