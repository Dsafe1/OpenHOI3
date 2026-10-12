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
}
