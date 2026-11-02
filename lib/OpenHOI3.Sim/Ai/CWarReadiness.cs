using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// Whether a prepared war is ready: the original's <c>CAIStrategy_IsReadyForWar</c>, which counts the fronts of the
/// country's areas that face the target - the enemy's strength beside them, its own fit units on them, and how much of
/// them stands empty. See <c>docs/mechanics/foreignminister.md</c>, section 7.
/// </summary>
public static class CWarReadiness
{
	/// <summary>A unit counts on a front only at this share of its strength and organisation or more, in thousandths.</summary>
	private const long Fit = 990;

	/// <summary>The enemy's strength once the war has been prepared three months, in thousandths.</summary>
	private const long Weakened = 660;

	/// <summary>More of the front than this standing empty, in thousandths, and the war waits.</summary>
	private const long MostEmpty = 300;

	/// <summary>Japan is ready on a front shorter than this, whatever else.</summary>
	private const int ShortFront = 4;

	public static bool IsReady(CGameState state, CCountry country, CWarTarget entry)
	{
		CReadinessTally tally = Count(state, country, entry.Target);
		long enemy = tally.Enemy;
		int fronts = tally.Fronts.Count;

		CDate prepared = CDate.FromDayNumber((int)(entry.Hour / CGameState.HoursPerDay));
		CDate today = CDate.FromDayNumber((int)(state.TotalHours / CGameState.HoursPerDay));
		if (prepared.AddMonths(3) <= today)
		{
			enemy = enemy * Weakened / 1000;
			if (country.Tag.Value == "JAP" && fronts < ShortFront)
			{
				return true;
			}
		}
		if (fronts > 0 && tally.Empty * 1000L / fronts > MostEmpty)
		{
			return false;
		}
		return enemy <= tally.Ours;
	}

	/// <summary>What the test counts on the country's fronts facing the target, before the dates and the bars are applied.</summary>
	public static CReadinessTally Count(CGameState state, CCountry country, CCountryTag target)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var tally = new CReadinessTally();
		var enemyMet = new HashSet<int>();
		foreach (CSupplyArea area in network.Areas)
		{
			if (area.Controller != country.Tag || !Faces(state, area, target))
			{
				continue;
			}
			foreach (List<int> front in CAreaFronts.Of(state, area))
			{
				foreach (int id in front)
				{
					if (network.AreaOf(id) is null)
					{
						continue;
					}
					bool onFront = false;
					foreach (CProvinceEdge edge in state.Province(id).Edges)
					{
						if (edge.Kind == EProvinceEdgeKind.Impassable || state.Province(edge.To).Controller != target
							|| network.AreaOf(edge.To) is null)
						{
							continue;
						}
						onFront = true;
						if (enemyMet.Add(edge.To))
						{
							tally.EnemyProvinces.Add(edge.To);
							tally.Enemy += LandStrength(state, edge.To, owner: null);
						}
					}
					if (!onFront)
					{
						continue;
					}
					long held = LandStrength(state, id, country);
					tally.Fronts.Add((id, held));
					if (held == 0)
					{
						tally.Empty++;
					}
					tally.Ours += held;
				}
			}
		}
		return tally;
	}

	/// <summary>Whether a neighbouring area of the area's has its first province held by the target.</summary>
	private static bool Faces(CGameState state, CSupplyArea area, CCountryTag target)
	{
		foreach (CSupplyArea beside in area.Neighbours)
		{
			if (beside.Provinces.Count > 0 && state.Province(beside.Provinces[0]).Controller == target)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// The strength of the land units in a province - with an owner given, only that country's out of a battle's wait and
	/// at least 990 of their strength and organisation.
	/// </summary>
	private static long LandStrength(CGameState state, int province, CCountry? owner)
	{
		long total = 0;
		foreach (CUnit unit in state.Positions.Of(province))
		{
			if (!CTheatres.IsLand(unit))
			{
				continue;
			}
			if (owner is not null && (unit.Owner != owner.Tag || unit.NoBattleHours > 0 || !IsFit(state, owner, unit)))
			{
				continue;
			}
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				total += brigade.Strength.Raw;
			}
		}
		return total;
	}

	/// <summary>A unit's average shares of its strength and organisation, each in thousandths, both at least 990.</summary>
	private static bool IsFit(CGameState state, CCountry owner, CUnit unit)
	{
		if (unit.SubUnits.Count == 0)
		{
			return true;
		}
		long strength = 0;
		long organisation = 0;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			long most = CUnits.MaxStrength(state, owner, brigade).Raw;
			strength += most == 0 ? -1 : brigade.Strength.Raw * 1000 / most;
			long ceiling = COrganisation.Ceiling(state, owner, brigade).Raw;
			organisation += ceiling == 0 ? -1 : brigade.Organisation.Raw * 1000 / ceiling;
		}
		long count = unit.SubUnits.Count * 1000L;
		return strength * 1000 / count >= Fit && organisation * 1000 / count >= Fit;
	}
}
