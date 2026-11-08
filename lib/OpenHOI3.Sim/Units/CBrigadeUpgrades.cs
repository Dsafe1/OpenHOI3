using System.Collections.Generic;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// Changing a brigade into another type, as <c>common/unit_upgrades.txt</c> allows: who may, to what, and what a line of it
/// is to its unit. See <c>docs/mechanics/upgrades.md</c>.
/// </summary>
public static class CBrigadeUpgrades
{
	public const string CostDefine = "UNIT_UPGRADE_COST";
	public const string TimeDefine = "UNIT_UPGRADE_TIME";
	public const string PracticalDefine = "UNIT_UPGRADE_PRACTICAL_MOD";

	/// <summary>The order a line's price is made by, with this game's defines.</summary>
	public static CBrigadeUpgradeOrder Order(CGameState state, CSubUnitType from, long strength)
	{
		return new CBrigadeUpgradeOrder(from, strength, state.Defines[CManpower.MilitarySection, CostDefine],
			state.Defines[CManpower.MilitarySection, TimeDefine], state.Defines[CManpower.MilitarySection, PracticalDefine]);
	}

	/// <summary>
	/// Whether a brigade may be changed where its unit is: the unit in no battle and not falling back, a fleet in a province with
	/// a naval base and an air unit with an air base, and the province in the same area as its owner's acting capital.
	/// </summary>
	public static bool MayUpgradeWhereItStands(CGameState state, CSubUnit brigade)
	{
		if (brigade.Unit is not { } unit || unit.IsFighting || unit.IsRetreating || state.Country(unit.Owner) is not { } owner)
		{
			return false;
		}
		CProvinceRef where = new(state, unit.Location);
		if (unit.Kind == EUnitKind.Navy && !where.HasBuilt(CMovementRules.NavalBase))
		{
			return false;
		}
		if (unit.Kind == EUnitKind.Air && !where.HasBuilt(CMovementRules.AirBase))
		{
			return false;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		return network.AreaOf(unit.Location) is { } area && ReferenceEquals(area, network.AreaOf(owner.ActingCapital));
	}

	/// <summary>What a brigade may become, in the file's order: the types it upgrades to that the country may still build one of.</summary>
	public static List<CSubUnitType> Targets(CGameState state, CCountry country, CSubUnit brigade)
	{
		var targets = new List<CSubUnitType>();
		foreach (CSubUnitType target in brigade.Type.UpgradeTargets)
		{
			if (CUnitAvailability.StillBuildable(state, country, target) >= 1)
			{
				targets.Add(target);
			}
		}
		return targets;
	}

	/// <summary>
	/// How many of the units' brigades may be changed where they stand into something the country may still build, and into
	/// what: each type once, in the order found.
	/// </summary>
	public static int CountUpgradeable(CGameState state, CCountry country, IEnumerable<CUnit> units, List<CSubUnitType>? found = null)
	{
		int count = 0;
		foreach (CUnit unit in units)
		{
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				if (!MayUpgradeWhereItStands(state, brigade))
				{
					continue;
				}
				List<CSubUnitType> targets = Targets(state, country, brigade);
				if (targets.Count > 0)
				{
					count++;
				}
				if (found is not null)
				{
					foreach (CSubUnitType target in targets)
					{
						if (!found.Contains(target))
						{
							found.Add(target);
						}
					}
				}
			}
		}
		return count;
	}

	/// <summary>Whether a line changes a brigade's type.</summary>
	public static bool IsUpgrade(CConstruction line)
	{
		return line.Brigades.Count != 0 && line.Brigades[0].Upgrade is not null;
	}

	/// <summary>A line finished or cancelled: its unit no longer shows it.</summary>
	public static void LineEnded(CGameState state, CConstruction line)
	{
		if (IsUpgrade(line) && CUnitFinder.Find(state, line.Country, line.Target) is { } unit)
		{
			unit.UpgradeLines.Remove(line);
		}
	}

	/// <summary>Every unit's list of the lines changing its brigades, made again from the queues after a load.</summary>
	public static void RegisterLines(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CConstruction line in country.Constructions)
			{
				if (IsUpgrade(line) && line.Target != 0 && CUnitFinder.Find(state, country.Tag, line.Target) is { } unit
					&& !unit.UpgradeLines.Contains(line))
				{
					unit.UpgradeLines.Add(line);
				}
			}
		}
	}

	private readonly struct CProvinceRef
	{
		private readonly CGameState _state;
		private readonly int _province;

		public CProvinceRef(CGameState state, int province)
		{
			_state = state;
			_province = province;
		}

		public bool HasBuilt(string building) => CMovementRules.BuildingLevel(_state, _state.Province(_province), building).Raw > 0;
	}
}
