using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Theatres;

/// <summary>
/// A new theatre headquarters: where it goes and what it is. The original's <c>CTheatre_PickHQProvince</c> and
/// <c>CCreateNewHQCommand</c>. See <c>docs/mechanics/theatres.md</c>, section 10.
/// </summary>
public static class CTheatreHeadquarters
{
	/// <summary>The type a new headquarters' one brigade is, by its key.</summary>
	public const string HqBrigade = "hq_brigade";

	/// <summary>The least effective infrastructure a province needs to take a headquarters, in thousandths.</summary>
	private const long LeastInfrastructure = 200;

	/// <summary>How far behind the front a headquarters is wanted.</summary>
	private const long Behind = 150;

	/// <summary>A score no province reaches, to start from.</summary>
	private const long Unscored = 10_000_000;

	/// <summary>What a new brigade's strength and experience are of their whole: a fifth.</summary>
	private const long Fifth = 5000;

	/// <summary>
	/// The province a new headquarters of the theatre goes in, or null where none of its provinces can take one.
	/// </summary>
	public static CProvince? PickProvince(CGameState state, CCountry country, CTheatre theatre)
	{
		CProvince? chosen = null;
		long least = Unscored;
		foreach (int id in theatre.AllProvinces)
		{
			CProvince province = state.Province(id);
			if (CSupplyFigures.EffectiveInfrastructure(province) < LeastInfrastructure || province.TheatreId != theatre.Id)
			{
				continue;
			}
			if (CTheatreFigures.HasHostileBeside(state, country, province) || HasLandBattle(state, province.Id))
			{
				continue;
			}
			chosen ??= province;
			long score = Score(state, theatre, province);
			if (score < least)
			{
				least = score;
				chosen = province;
			}
		}
		return chosen;
	}

	private static bool HasLandBattle(CGameState state, int province)
	{
		foreach (CCombat combat in state.Combats)
		{
			if (combat.Province == province && combat.Kind == ECombatKind.Land && !combat.HasEnded)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// |150 - the mean distance to the theatre's front provinces|, x4 bordering one, x2 bordering another holder's land,
	/// /3 for its holder's capital, x100 on a front itself.
	/// </summary>
	private static long Score(CGameState state, CTheatre theatre, CProvince province)
	{
		long total = 0;
		long count = 0;
		bool onFront = false;
		bool besideFront = false;
		foreach (CFront front in theatre.Fronts)
		{
			if (front.Provinces.Count <= 0)
			{
				continue;
			}
			count += front.Provinces.Count;
			foreach (int id in front.Provinces)
			{
				if (id == province.Id)
				{
					onFront = true;
				}
				else if (province.EdgeTo(id) is not null)
				{
					besideFront = true;
				}
				total += CMapDistance.Between(province, state.Province(id), state.MapWidth) * 1000;
			}
		}
		if (count > 0)
		{
			total = total * 1000 / (count * 1000);
		}
		long score = Math.Abs(Behind - total / 1000);
		if (besideFront)
		{
			score *= 4;
		}
		if (CAreaFronts.BordersForeignLand(state, province))
		{
			score *= 2;
		}
		if (state.Country(province.Controller) is { } holder && holder.Capital == province.Id)
		{
			score /= 3;
		}
		if (onFront)
		{
			score *= 100;
		}
		return score;
	}

	/// <summary>
	/// The headquarters made in the province: a theatre-level army with one <c>hq_brigade</c> at a fifth of its strength,
	/// named after the theatre's best province, its leader chosen; the theatre takes it, and the country's orphans are put
	/// under it. Nothing where the type is missing.
	/// </summary>
	public static CUnit? Make(CGameState state, CCountry country, CTheatre theatre, CProvince province)
	{
		if (!state.SubUnitTypes.TryGetValue(HqBrigade, out CSubUnitType? type))
		{
			state.LastUnitId += 2;
			return null;
		}
		var unit = new CUnit(EUnitKind.Theatre, country.Tag) { Id = state.LastUnitId + 1, Location = province.Id };
		var brigade = new CSubUnit(type) { Id = state.LastUnitId + 2, Unit = unit, Home = province.Id };
		state.LastUnitId += 2;

		brigade.Experience = new CFixedPoint(Math.Min(CUnits.MostExperience.Raw,
			country.Modifier[CUnits.UnitStartExperience].Raw * CFixedPoint.Scale / Fifth));
		CUnits.RebuildStats(brigade);
		long most = brigade.Stats[CSubUnitType.MaxStrength].Raw;
		long strength = Math.Max(0, Math.Min(most * CFixedPoint.Scale / Fifth, most));
		brigade.Strength = new CFixedPoint(strength);
		brigade.Highest = CFixedPoint.Max(brigade.Highest, brigade.Strength);
		// Its technologies are applied at the levels its type gives a new brigade - every one at nought - and not at its
		// country's: the start saves' new headquarters hold no doctrine where their countries hold several, and the
		// upgrades bring them up later (theatres.md, section 10; upkeep.md, section 5).
		Revolts.CRebelArmies.SetFull(state, country, brigade);

		country.Units.Add(unit);
		unit.SubUnits.Add(brigade);
		brigade.Name = CUnitNames.ForSubUnit(state, country, type);
		CUnitNames.Register(country, brigade.Name);
		// Put in its province as any land unit entering one, it brings its days of supplies and fuel with it, having come
		// from nowhere: out of the stockpile on its country's own ground (supply.md, section 14) - 2.010 supplies, fifteen
		// days of a fifth of an hq_brigade, which every country given a new headquarters has less on the first screen.
		CSupplyCarry.Bring(state, country, unit, from: 0, province.Id, beforeGame: state.IsBeingMade);
		state.ForgetPositions();
		unit.Name = CUnitNames.ForTheatre(state, country, NamingProvince(state, province)?.Name);
		CUnitNames.Register(country, unit.Name);
		if (country.AutoAssignLeaders)
		{
			CLeaders.AssignBest(state, country, unit);
		}
		CCombats.CheckForCombat(state, unit, province.Id);

		if (CTheatreProvinces.Of(state, province.Id) is { } stamped && stamped.Country == country.Tag)
		{
			stamped.Headquarters = unit.Id;
			foreach (CUnit orphan in country.UnitsInOrder())
			{
				if (orphan.Kind != EUnitKind.Theatre && orphan.IsOrphaned)
				{
					CTheatres.Attach(country, orphan, unit);
					orphan.IsOrphaned = false;
				}
			}
		}
		return unit;
	}

	/// <summary>
	/// The province a theatre's headquarters is named after: of the theatre the province is in, the one with most
	/// <c>victory points x 10000 + 5000 for a naval base + its IC</c>, the first of equals.
	/// </summary>
	public static CProvince? NamingProvince(CGameState state, CProvince province)
	{
		if (CTheatreProvinces.Of(state, province.Id) is not { } theatre)
		{
			return null;
		}
		CBuilding? navalBase = CTheatreFigures.NavalBase(state);
		CProvince? best = null;
		long most = 0;
		foreach (int id in theatre.AllProvinces)
		{
			CProvince candidate = state.Province(id);
			long score = candidate.Points * 10000L
				+ (navalBase is not null && candidate.BuildingMaxLevel(navalBase).Raw > 0 ? 5000 : 0)
				+ candidate.Modifier[CTheatreFigures.Ic].Raw;
			if (best is null || score > most)
			{
				best = candidate;
				most = score;
			}
		}
		return best;
	}
}
