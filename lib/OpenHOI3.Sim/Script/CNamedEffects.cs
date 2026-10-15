using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The effects whose key is a name rather than a keyword - a government position, a building, a
/// technology: <c>docs/mechanics/effects.md</c>, sections 4 and 5. <c>this</c> and <c>from</c> are
/// nought to each.
/// </summary>
public static class CNamedEffects
{
	/// <summary>The building a province must have a port for.</summary>
	public const string NavalBase = "naval_base";

	public static void Run(CGameState state, CEffect effect, CEventScope scope)
	{
		CCountry? country = state.Country(scope.Country);
		CScriptArgument argument = effect.Argument;
		CFixedPoint value = argument.IsThis || argument.IsFrom ? CFixedPoint.Zero : argument.Amount;
		switch (effect.Kind)
		{
			case EEffectKind.GovernmentPosition:
				if (country is not null)
				{
					Appoint(country, effect.Key, (int)CConditions.WholeOf(value));
				}
				return;
			case EEffectKind.Building:
				if (scope.Province != 0)
				{
					Build(state, state.Province(scope.Province), effect.Key, value);
				}
				return;
			case EEffectKind.Technology:
				if (country is not null && state.Technologies.TryGetValue(effect.Key, out CTechnology? technology))
				{
					// One step of research, as though that leadership had been given it.
					CResearch.Advance(state, country, technology, value);
				}
				return;
			default:
				throw new InvalidOperationException($"'{effect.Key}' is no name an effect runs");
		}
	}

	/// <summary>
	/// The minister of that id into the post, where he was declared for it and its holder has another
	/// id. Ours: the id is looked for among the country's own, where the original's ids are the
	/// whole game's.
	/// </summary>
	private static void Appoint(CCountry country, string position, int id)
	{
		if (!country.Ministers.TryGetValue(id, out CMinister? minister) || minister.TypeIn(position) is null)
		{
			return;
		}
		if (country.Government.TryGetValue(position, out CMinister? holder) && holder.Id == id)
		{
			return;
		}
		country.Government[position] = minister;
	}

	/// <summary>
	/// Whole levels added to what is built, all of it working: a level added repairs the rest. No cap
	/// and no floor; a naval base needs a port.
	/// </summary>
	private static void Build(CGameState state, CProvince province, string key, CFixedPoint levels)
	{
		CBuilding? building = null;
		foreach (CBuilding each in state.Buildings)
		{
			if (each.Key == key)
			{
				building = each;
				break;
			}
		}
		if (building is null || (building.Key == NavalBase && province.PortSea == 0))
		{
			return;
		}
		CFixedPoint built = province.BuildingMaxLevel(building);
		if (built == CFixedPoint.Zero && levels <= CFixedPoint.Zero)
		{
			return;
		}
		province.SetBuildingLevel(building, built + CFixedPoint.FromInt((int)CConditions.WholeOf(levels)));
		CProvinceModifiers.RebuildOne(state, province);
	}
}
