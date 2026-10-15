using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// What the rebels ask of the world and of their factions: which faction holds a province or owns an army, who is a
/// friend of a faction, which ground it wants, and the words it is named in. See <c>docs/mechanics/rebels.md</c>,
/// sections 4, 6 and 9.
/// </summary>
public static class CRebelFactions
{
	/// <summary>The rebels' tag: they hold land and own their armies under it.</summary>
	public const string Rebels = "REB";

	public static readonly CCountryTag Tag = new(Rebels);

	public static bool IsRebels(CCountryTag? tag) => tag is { } some && some.Value == Rebels;

	/// <summary>
	/// What <c>REB</c> names in <c>is_core</c>, <c>add_core</c>, <c>remove_core</c> and <c>secede_province</c>: with a
	/// faction in the scope, the country it would free - none for a type that defects, whose country to defect to is
	/// always none; without, the rebels' own tag.
	/// </summary>
	public static CCountryTag? Meant(Script.CEventScope scope)
	{
		if (scope.RebelFaction is not { } faction)
		{
			return Tag;
		}
		return faction.Type.Defection ? null : faction.Independence;
	}

	/// <summary>
	/// The original's <c>FindRebelFactionHoldingProvince</c>: the first faction with the province on its list, or one of
	/// whose armies stands in it.
	/// </summary>
	public static CRebelFaction? Holding(CGameState state, int province)
	{
		foreach (CRebelFaction faction in state.RebelFactions)
		{
			if (faction.Provinces.Contains(province))
			{
				return faction;
			}
			foreach (int army in faction.Armies)
			{
				if (FindUnit(state, army) is { } unit && unit.Location == province)
				{
					return faction;
				}
			}
		}
		return null;
	}

	/// <summary>The faction with this army on its list, or none.</summary>
	public static CRebelFaction? OfArmy(CGameState state, CUnit unit)
	{
		foreach (CRebelFaction faction in state.RebelFactions)
		{
			if (faction.Armies.Contains(unit.Id))
			{
				return faction;
			}
		}
		return null;
	}

	/// <summary>A unit still in the game, by its number, whoever's it is.</summary>
	public static CUnit? FindUnit(CGameState state, int id)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.Id == id && !unit.IsRemoved)
				{
					return unit;
				}
			}
		}
		return null;
	}

	/// <summary>
	/// The original's <c>RebelFaction_IsFriendlyCountry</c>: everybody where the faction has no targets; otherwise none
	/// of them, and at war with every one - the rebels counting as at war with everybody.
	/// </summary>
	public static bool IsFriendly(CGameState state, CRebelFaction faction, CCountryTag? country)
	{
		foreach (CCountryTag target in faction.Targets)
		{
			if (country == target)
			{
				return false;
			}
			if (!IsRebels(target) && !IsRebels(country)
				&& (country is not { } some || !CCountryLists.AtWar(state, some, target)))
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>In reach: the type is not tied to its country's land, or the faction's country owns the province.</summary>
	public static bool InReach(CRebelFaction faction, CProvince province)
	{
		return faction.Type.AnyArea || (province.Owner is { } owner && owner == faction.Country);
	}

	/// <summary>
	/// The original's <c>RebelFaction_IsHostileProvince</c>: in reach, and its owner or its holder not friendly.
	/// </summary>
	public static bool IsHostile(CGameState state, CRebelFaction faction, CProvince province)
	{
		return InReach(faction, province)
			&& !(IsFriendly(state, faction, province.Owner) && IsFriendly(state, faction, province.Controller));
	}

	/// <summary>
	/// Whether a province is in a real area, which the rising and the march ask: one of the areas the supply network
	/// is made of - land of one holder with more than one level of infrastructure (<c>supply.md</c>, section 2).
	/// </summary>
	public static CSupplyArea? AreaOf(CGameState state, int province)
	{
		return CSupplyNetworks.Refresh(state).AreaOf(province);
	}

	/// <summary>
	/// A faction's text with its three countries' adjectives put in: COUNTRY its country's, INDEP the country it would
	/// free, EVIL its first target's.
	/// </summary>
	public static string Render(CGameState state, CRebelFaction faction, string text)
	{
		return text.Replace("$COUNTRY$", Adjective(state, faction.Country))
			.Replace("$INDEP$", Adjective(state, faction.Independence))
			.Replace("$EVIL$", Adjective(state, faction.Targets.Count > 0 ? faction.Targets[0] : null));
	}

	private static string Adjective(CGameState state, CCountryTag? tag)
	{
		return state.Country(tag)?.Adjective ?? "";
	}
}
