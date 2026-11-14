using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The conditions on the scope's province: <c>docs/mechanics/triggers.md</c>, section 5. Only those
/// the original guards ask whether there is a province; the rest read province 0, which has no
/// owner and nothing on it.
/// </summary>
public static class CProvinceConditions
{
	public static bool TryAnswer(CGameState state, CTrigger trigger, CEventScope scope, out bool answer)
	{
		CProvince here = CConditions.Here(state, scope);
		bool none = scope.Province == 0;
		CScriptArgument value = trigger.Argument;
		switch (trigger.Keyword)
		{
			case "province_id":
				answer = !none && scope.Province == CConditions.WholeOf(value.Amount);
				return true;
			case "empty":
				answer = !none && CConditions.Is(trigger, here.Owner is null);
				return true;
			case "port":
				answer = !none && CConditions.Is(trigger, here.PortSea != 0);
				return true;
			case "is_capital":
				answer = CConditions.Is(trigger, state.Country(here.Owner) is { } owner && owner.Capital == here.Id);
				return true;
			case "has_building":
				// Ours: a building nobody has is false, where the original faults.
				answer = CNamedConditions.Built(state, here, value.Text) > 0;
				return true;
			case "nationalism":
				// Strictly more whole years than the figure's whole part (occupation.md, section 5).
				answer = here.Nationalism.Raw > CConditions.WholeOf(value.Amount) * CFixedPoint.Scale;
				return true;
			case "leadership":
				answer = here.Leadership >= value.Amount;
				return true;
			case "out_of_supply_days":
				// Strictly more days than the figure, of the scope's province alone (supply.md, section 18).
				answer = !none && here.OutOfSupplyDays > CConditions.WholeOf(value.Amount);
				return true;
			case "continent":
				answer = here.Continent.Equals(Continent(state, trigger, scope), StringComparison.Ordinal);
				return true;
			case "region":
				answer = !none && InRegion(state, here, value.Text);
				return true;
			case "strategic_resource":
				answer = StrategicResource(state, value) is { } resource
					&& here.StrategicResource.Equals(resource, StringComparison.Ordinal);
				return true;
			case "has_strategic_resource":
				// The value is not read.
				answer = here.StrategicResource.Length > 0;
				return true;
			case "owned_by":
				answer = !none && here.Owner == CConditions.TagByOwner(state, trigger, scope);
				return true;
			case "controlled_by":
				answer = !none && here.Controller == (value.IsWord("owner") ? here.Owner : CConditions.TagByOwner(state, trigger, scope));
				return true;
			case "is_core":
				answer = IsCore(state, trigger, scope);
				return true;
			case "units_in_province":
				answer = UnitsInProvince(state, trigger, scope);
				return true;
			case "country_units_in_province":
				answer = CConditions.TagByOwner(state, trigger, scope) is { } whose
					&& HasUnitOf(state, scope.Province, whose);
				return true;
			case "has_empty_adjacent_province":
				// The value is not read.
				answer = scope.Province > 0 && HasEmptyNeighbour(state, here);
				return true;
			case "unit_has_leader":
				answer = AnyUnit(state, scope.Province, unit => unit.Leader is not null);
				return true;
			case "unit_in_battle":
				answer = AnyUnit(state, scope.Province, unit => unit.Combats.Count > 0 && unit.Owner == here.Owner);
				return true;
			default:
				answer = false;
				return false;
		}
	}

	/// <summary>
	/// The continent asked for: the one written, or that of THIS's or FROM's declared capital. A
	/// name no province is on is the sea's, as the original's null continent is.
	/// </summary>
	private static string Continent(CGameState state, CTrigger trigger, CEventScope scope)
	{
		CScriptArgument value = trigger.Argument;
		if (value.IsThis || value.IsFrom)
		{
			CCountryTag? whose = value.IsThis ? scope.Root.Country : scope.FromCountry;
			return state.Province(state.Country(whose)?.Capital ?? 0).Continent;
		}
		foreach (CProvince province in state.Provinces)
		{
			if (province.Continent.Equals(value.Text, StringComparison.Ordinal))
			{
				return value.Text;
			}
		}
		return "";
	}

	private static bool InRegion(CGameState state, CProvince here, string name)
	{
		foreach (int index in here.Regions)
		{
			if (state.Regions[index].Name.Equals(name, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The resource asked for, "" for <c>none</c>, or null for a name no resource has.</summary>
	private static string? StrategicResource(CGameState state, CScriptArgument value)
	{
		if (value.IsWord("none"))
		{
			return "";
		}
		return state.FindStrategicResource(value.Text) is null ? null : value.Text;
	}

	/// <summary>
	/// <c>is_core</c>: with a number, whether that province is a core of the scope's country;
	/// otherwise whether the scope's province is a core of the country named.
	/// </summary>
	private static bool IsCore(CGameState state, CTrigger trigger, CEventScope scope)
	{
		CScriptArgument value = trigger.Argument;
		if (value.Province != 0)
		{
			return scope.Country is { } us && state.Province(value.Province).Cores.Contains(us);
		}
		// REB is the scope's rebel faction's country (rebels.md, section 10).
		CCountryTag? asked = !value.Quoted && value.Text == CProvinceEffects.Rebels
			? Revolts.CRebelFactions.Meant(scope) : CConditions.TagByOwner(state, trigger, scope);
		return asked is { } named && CConditions.Here(state, scope).Cores.Contains(named);
	}

	/// <summary>
	/// A count of every unit in the province; or, with <c>this</c> (by owner) or <c>from</c>,
	/// whether that country has any there.
	/// </summary>
	private static bool UnitsInProvince(CGameState state, CTrigger trigger, CEventScope scope)
	{
		CScriptArgument value = trigger.Argument;
		if (!value.IsThis && !value.IsFrom)
		{
			return state.UnitsIn(scope.Province).Count >= CConditions.WholeOf(value.Amount);
		}
		CCountryTag? whose = value.IsFrom ? scope.FromCountry : CConditions.ThisByOwner(state, scope);
		return whose is { } owner && HasUnitOf(state, scope.Province, owner);
	}

	private static bool HasUnitOf(CGameState state, int province, CCountryTag owner)
	{
		return AnyUnit(state, province, unit => unit.Owner == owner);
	}

	private static bool AnyUnit(CGameState state, int province, Func<CUnit, bool> test)
	{
		foreach (CUnit unit in state.UnitsIn(province))
		{
			if (test(unit))
			{
				return true;
			}
		}
		return false;
	}

	private static bool HasEmptyNeighbour(CGameState state, CProvince here)
	{
		foreach (CProvinceEdge edge in here.Edges)
		{
			if (state.Province(edge.To).Owner is null)
			{
				return true;
			}
		}
		return false;
	}
}
