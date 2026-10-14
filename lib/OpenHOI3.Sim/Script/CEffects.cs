using System;
using System.Collections.Generic;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// Runs effects as the original does: in order, each in the scope it is given, a scope block in a
/// copy of the scope it names, the leaves in the classes beside it. What the simulation does not
/// hold yet does nothing and goes in the report. See <c>docs/mechanics/effects.md</c>.
/// </summary>
public static class CEffects
{
	/// <summary>Runs a block of effects in order.</summary>
	public static void Run(CGameState state, IEnumerable<CEffect> effects, CEventScope scope, CScriptReport? report = null)
	{
		foreach (CEffect effect in effects)
		{
			Run(state, effect, scope, report);
		}
	}

	public static void Run(CGameState state, CEffect effect, CEventScope scope, CScriptReport? report = null)
	{
		switch (effect.Kind)
		{
			case EEffectKind.Keyword:
				Keyword(state, effect, scope, report);
				return;
			case EEffectKind.Scope:
				// A scope that cannot be made runs nothing, and the effects after it run.
				if (CScopeSwitch.Narrow(state, scope, effect.Argument, report) is { } narrowed)
				{
					Run(state, effect.Children, narrowed, report);
				}
				return;
			case EEffectKind.DeadScope:
				// A key that is no scope - limit the plainest - never runs what it holds.
				return;
			case EEffectKind.Region:
				CEffectSets.Region(state, effect, scope, report);
				return;
			case EEffectKind.GovernmentPosition:
			case EEffectKind.Building:
			case EEffectKind.Technology:
				CNamedEffects.Run(state, effect, scope);
				return;
			case EEffectKind.Ideology:
				CPoliticsEffects.IdeologyBlock(state, effect, scope);
				return;
			case EEffectKind.RebelType:
			case EEffectKind.UnitType:
				report?.Add(effect.Key, EScriptGap.Unbuilt);
				return;
			default:
				throw new InvalidOperationException($"'{effect.Key}' is no effect that runs on its own");
		}
	}

	private static void Keyword(CGameState state, CEffect effect, CEventScope scope, CScriptReport? report)
	{
		if (CEffectSets.TryRun(state, effect, scope, report))
		{
			return;
		}
		if (CUndoneEffects.Reason(effect.Keyword) is { } why)
		{
			report?.Add(effect.Key, why);
			return;
		}
		if (CMarkEffects.TryRun(state, effect, scope)
			|| CCountryEffects.TryRun(state, effect, scope)
			|| CPoliticsEffects.TryRun(state, effect, scope)
			|| CDiplomacyEffects.TryRun(state, effect, scope)
			|| CWarEffects.TryRun(state, effect, scope)
			|| CProvinceEffects.TryRun(state, effect, scope))
		{
			return;
		}
		throw new InvalidOperationException($"'{effect.Key}' is no effect anything runs");
	}

	/// <summary>One of a block effect's own keys, or null where it was not written.</summary>
	public static CScriptArgument? Field(CEffect effect, string key)
	{
		foreach (CScriptField field in effect.Fields)
		{
			if (field.Key == key)
			{
				return field.Argument;
			}
		}
		return null;
	}

	/// <summary>
	/// The country most effects that name one mean: THIS is the root's country, or the owner of the
	/// root's province; FROM the root's FROM country, or the owner of the root's FROM province;
	/// otherwise the tag written, null for none.
	/// </summary>
	public static Countries.CCountryTag? Target(CGameState state, CEffect effect, CEventScope scope)
	{
		return Target(state, effect.Argument, scope);
	}

	/// <summary>The same, of a block's own key - <c>relation</c>'s <c>who</c>.</summary>
	public static Countries.CCountryTag? Target(CGameState state, CScriptArgument value, CEventScope scope)
	{
		if (value.IsThis)
		{
			return scope.Root.Country ?? state.Province(scope.Root.Province).Owner;
		}
		if (value.IsFrom)
		{
			return scope.Root.FromCountry ?? state.Province(scope.Root.FromProvince).Owner;
		}
		return CEventScope.Known(value.Tag);
	}

	/// <summary>Whether a block's <c>limit</c> lets the member's scope through: no limit lets everything.</summary>
	public static bool Passes(CGameState state, CEffect effect, CEventScope member, CScriptReport? report)
	{
		return effect.Limit is null || CConditions.Holds(state, effect.Limit, member, report);
	}
}
