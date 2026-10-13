using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// The casus belli types and the casus belli held: finding a type by name, whether a country may use a type
/// against another, whether it has any, and giving and spending one. See <c>docs/mechanics/war.md</c>,
/// section 2.
/// </summary>
public static class CCasusBellis
{
	/// <summary>The type of that name - case and all - or the null type where none has it.</summary>
	public static CCasusBelliType Find(CGameState state, string? name)
	{
		if (!string.IsNullOrEmpty(name))
		{
			foreach (CCasusBelliType type in state.CasusBelliTypes)
			{
				if (type.IsReal && string.Equals(type.Name, name, StringComparison.Ordinal))
				{
					return type;
				}
			}
		}
		return CCasusBelliType.Null;
	}

	/// <summary>
	/// Whether the holder may use the type against the target: it holds one of it, or the type is
	/// <c>always</c>; and the type's <c>is_valid</c> holds in the target's scope with THIS the holder. The
	/// original's <c>DiplomacyStatus_CanUseCasusBelliType</c>.
	/// </summary>
	public static bool CanUse(CGameState state, CCountry holder, CCountry target, CCasusBelliType type)
	{
		bool held = CGameState.Status(holder, target).CasusBelli.Exists(c => c.Type == type.Index);
		if (!held && !type.Always)
		{
			return false;
		}
		return IsValid(state, holder.Tag, target.Tag, type);
	}

	/// <summary>The type's <c>is_valid</c>, in the target's scope with THIS the holder and FROM empty.</summary>
	public static bool IsValid(CGameState state, CCountryTag holder, CCountryTag target, CCasusBelliType type)
	{
		CEventScope scope = CEventScope.Start(holder).With(target, 0);
		return CConditions.All(state, type.IsValid, scope, null);
	}

	/// <summary>Whether the holder has some real type, in the file's order, it may use against the target.</summary>
	public static bool HasUsable(CGameState state, CCountry holder, CCountry target)
	{
		foreach (CCasusBelliType type in state.CasusBelliTypes)
		{
			if (type.IsReal && CanUse(state, holder, target, type))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// The original's <c>AddCasusBelli</c>: the holder is given a casus belli of the null type on the target,
	/// for ever, each time it is asked - never on itself.
	/// </summary>
	public static void Give(CGameState state, CCountry holder, CCountry target)
	{
		if (holder != target)
		{
			CGameState.Status(holder, target).CasusBelli.Add(new CCasusBelli(CCasusBelliType.Null.Index));
		}
	}

	/// <summary>The first casus belli of the type the holder holds on the target taken off it, silently - where it holds one.</summary>
	public static void Spend(CCountry holder, CCountry target, CCasusBelliType type)
	{
		var held = CGameState.Status(holder, target).CasusBelli;
		int index = held.FindIndex(c => c.Type == type.Index);
		if (index >= 0)
		{
			held.RemoveAt(index);
		}
	}
}
