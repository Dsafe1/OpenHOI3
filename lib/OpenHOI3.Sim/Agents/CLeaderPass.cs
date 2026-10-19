using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The AI's leaders: each of its units without one given the first free leader in service of the rank for it, unless a
/// later one is fit for it by a quarter more - fit being his greatest skill, raised or lowered by what each of his traits
/// does for the unit where it stands. No leader in place is ever changed. The original's
/// <c>CEU3AI_AssignLeadersToLeaderlessUnits</c> and <c>AI_LeaderFitForUnit</c>. See <c>docs/mechanics/moving.md</c>,
/// section 13.
/// </summary>
public static class CLeaderPass
{
	private const long Whole = 1000;

	/// <summary>A later leader is taken only where he is fit by this many thousandths of the one already found.</summary>
	private const long Better = 1250;

	public static void Run(CGameState state, CCountry country)
	{
		var given = new HashSet<CLeader>();
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (unit.IsRemoved || unit.Leader is not null)
			{
				continue;
			}
			CLeader? pick = null;
			long pickFit = 0;
			foreach (CLeader leader in country.ActiveLeaders)
			{
				if (leader.Rank <= 0 || leader.Unit is not null || given.Contains(leader))
				{
					continue;
				}
				long fit = Fit(state, leader, unit);
				if (fit >= (pick is null ? Better : pickFit * Better / Whole))
				{
					pick = leader;
					pickFit = fit;
				}
			}
			if (pick is not null)
			{
				given.Add(pick);
				state.Outbox.Add(new CAssignLeaderCommand(country.Tag, unit.Id, pick.Id));
			}
		}
	}

	/// <summary>His greatest skill by a hundred, then by each effect of each trait; half for a police or garrison division.</summary>
	public static long Fit(CGameState state, CLeader leader, CUnit unit)
	{
		if (!CLeaders.MayCommand(leader, unit, onlyFree: false))
		{
			return 0;
		}
		long fit = leader.MaxSkill * 100 * Whole;
		foreach (CTrait trait in leader.Traits)
		{
			foreach (CTraitEffect effect in trait.Effects)
			{
				fit = fit * Factor(state, unit, effect.Kind) / Whole;
			}
		}
		if (unit.Kind is not (EUnitKind.Navy or EUnitKind.Air) && CArmyRole.Of(unit) == CArmyRole.Garrison)
		{
			fit /= 2;
		}
		return fit;
	}

	private static long Factor(CGameState state, CUnit unit, ETraitEffect kind)
	{
		switch (kind)
		{
			case ETraitEffect.SurpriseChance:
			case ETraitEffect.DefenceModifier:
			case ETraitEffect.OffenceModifier:
			case ETraitEffect.EncirclementBonus:
			case ETraitEffect.EnvelopmentBonus:
				return 1200;
			case ETraitEffect.CombinedArmsBonus:
				return CCombatModifiers.HasCombinedArms(state, unit) ? 1500 : 1100;
			case ETraitEffect.OutOfSupplyModifier:
				return unit.SuppliesReceived != CFixedPoint.One ? 1500 : 1100;
			case ETraitEffect.WinterAttrition:
				if (unit.Location > 0 && state.Province(unit.Location) is { } here && here.WeatherPicture != 0
					&& here.Weather.Temperature.Raw < -5000)
				{
					return 1500;
				}
				return 1100;
			case ETraitEffect.SupplyConsumption:
				return 500 * PerBrigadeConsumption(unit) / Whole;
			default:
				return 1100;
		}
	}

	/// <summary>Its supplies and fuel a day over its brigades, without leaders, in thousandths.</summary>
	private static long PerBrigadeConsumption(CUnit unit)
	{
		if (unit.SubUnits.Count == 0)
		{
			return 0;
		}
		long sum = 0;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			sum += brigade.Stats[CUnitSupply.SupplyStatistic].Raw + brigade.Stats[CUnitSupply.FuelStatistic].Raw;
		}
		return sum / unit.SubUnits.Count;
	}
}
