using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// The rules about units that do not belong to one unit: what a brigade's figures are
/// made of, how strong it may be, and what the units of an order of battle hold on the
/// first hour of a new game. See <c>docs/mechanics/units.md</c>.
/// </summary>
public static class CUnits
{
	/// <summary>The modifier a conscription law sets: how much of a reserve is missing. Below nothing.</summary>
	public const string ReservesPenaltySize = "reserves_penalty_size";

	/// <summary>The modifier a training law sets: the experience of a unit nobody gave one.</summary>
	public const string UnitStartExperience = "unit_start_experience";

	private static readonly CFixedPoint MostExperience = CFixedPoint.FromInt(100);

	/// <summary>Under this much strength, a brigade that holds no frontage adds nothing to its unit.</summary>
	private static readonly CFixedPoint LeastStrengthToCount = new(100);

	/// <summary>
	/// What a new game does with the units its orders of battle made, country by
	/// country: each brigade takes its model's equipment and is held to what it may
	/// have, the country is given the officers its units come with, and a brigade
	/// nobody gave an experience takes the country's.
	/// </summary>
	public static void SetAtStart(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			// It is being at war when the game starts that calls the reserves up.
			country.IsMobilised = state.IsAtWar(country.Tag);

			var units = new List<CUnit>(country.AllUnits());
			foreach (CUnit unit in units)
			{
				foreach (CSubUnit subUnit in unit.SubUnits)
				{
					AfterLoad(state, country, subUnit);
				}
			}

			country.Officers = CFixedPoint.Max(CFixedPoint.Zero, country.Officers + OfficersAtStart(country, units));

			foreach (CUnit unit in units)
			{
				foreach (CSubUnit subUnit in unit.SubUnits)
				{
					if (subUnit.Experience < CFixedPoint.Zero)
					{
						subUnit.Experience = StartingExperience(country, subUnit.IsReserve);
					}
				}
			}
		}
	}

	/// <summary>
	/// What a brigade becomes once its line has been read. Only for one with a
	/// historical model: without, it keeps every level at nothing and all the strength
	/// and organisation it was made with.
	/// </summary>
	public static void AfterLoad(CGameState state, CCountry owner, CSubUnit subUnit)
	{
		int model = subUnit.HistoricalModel;
		if (model >= 0 && model < CHistoricalModels.Count(state, owner, subUnit.Type))
		{
			foreach (CTechnology technology in subUnit.Type.Technologies)
			{
				// The model's level, whatever the country itself has reached.
				if (CHistoricalModels.Level(state, owner, subUnit.Type, model, technology) is { } level)
				{
					subUnit.TechnologyLevels[technology.Name] = level;
				}
			}
			foreach (CTechnology technology in subUnit.Type.Technologies)
			{
				// A doctrine needs no new equipment, so one the model left at nothing is the country's.
				if (!technology.Change && subUnit.TechnologyLevels.GetValueOrDefault(technology.Name) == 0)
				{
					subUnit.TechnologyLevels[technology.Name] = owner.TechnologyLevels.GetValueOrDefault(technology.Name);
				}
			}
		}

		RebuildStats(subUnit);

		if (model != CSubUnit.NoModel)
		{
			// No game is running yet, so its organisation is held to its type's and nothing else.
			subUnit.Strength = CFixedPoint.Min(subUnit.Strength, MaxStrength(state, owner, subUnit, penaltyAtWar: true));
			subUnit.Organisation = CFixedPoint.Min(subUnit.Organisation, subUnit.Stats[CSubUnitType.DefaultOrganisation]);
			subUnit.Highest = subUnit.Strength;
		}
	}

	/// <summary>
	/// Puts a brigade's figures back to its type's and adds each of its technologies,
	/// as many times over as the level it holds.
	/// </summary>
	public static void RebuildStats(CSubUnit subUnit)
	{
		subUnit.Stats.CopyFrom(subUnit.Type.Stats);
		foreach (CTechnology technology in subUnit.Type.Technologies)
		{
			if (technology.UnitEffects.TryGetValue(subUnit.Type.Key, out CSubUnitStats? effect))
			{
				subUnit.Stats.Add(effect, subUnit.TechnologyLevels.GetValueOrDefault(technology.Name));
			}
		}
	}

	/// <summary>
	/// The most strength a brigade may have: its own figure, and for a reserve that less
	/// the share its country's conscription law keeps back - unless the country has
	/// mobilised.
	/// </summary>
	/// <param name="penaltyAtWar">What to do for a country at war that has not mobilised.</param>
	/// <param name="penaltyAlways">Keep the share back whatever the country is doing.</param>
	public static CFixedPoint MaxStrength(CGameState state, CCountry owner, CSubUnit subUnit,
		bool penaltyAtWar = false, bool penaltyAlways = false)
	{
		CFixedPoint most = subUnit.Stats[CSubUnitType.MaxStrength];
		if (subUnit.IsReserve && ReservePenaltyApplies(state, owner, penaltyAtWar, penaltyAlways))
		{
			most = new CFixedPoint(most.Raw * (CFixedPoint.Scale + owner.Modifier[ReservesPenaltySize].Raw) / CFixedPoint.Scale);
		}
		return most;
	}

	/// <summary>Whether a reserve of this country is kept under strength now.</summary>
	public static bool ReservePenaltyApplies(CGameState state, CCountry owner, bool penaltyAtWar, bool penaltyAlways)
	{
		if (penaltyAlways)
		{
			return true;
		}
		if (owner.IsMobilised)
		{
			return false;
		}
		return !state.IsAtWar(owner.Tag) || penaltyAtWar;
	}

	/// <summary>
	/// The experience of a brigade whose line gave none: what the country's training
	/// law says, and for a reserve only the share of it the conscription law leaves -
	/// mobilised or not.
	/// </summary>
	public static CFixedPoint StartingExperience(CCountry owner, bool isReserve)
	{
		long experience = owner.Modifier[UnitStartExperience].Raw;
		if (isReserve)
		{
			experience = experience * (CFixedPoint.Scale + owner.Modifier[ReservesPenaltySize].Raw) / CFixedPoint.Scale;
		}
		return CFixedPoint.Min(new CFixedPoint(experience), MostExperience);
	}

	/// <summary>
	/// The units a share of IC works through, in the order it reaches them: those marked
	/// to go first, the last of them first, and then the rest in the order they were made.
	/// </summary>
	public static List<CUnit> InWorkingOrder(CCountry country, System.Predicate<CUnit> wanted)
	{
		var order = new List<CUnit>();
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (!wanted(unit))
			{
				continue;
			}
			if (unit.IsPrioritised)
			{
				order.Insert(0, unit);
			}
			else
			{
				order.Add(unit);
			}
		}
		return order;
	}

	/// <summary>
	/// The officers a country's units come with: each unit's own, and where the
	/// country's history gives a ratio, each unit's times that, rounded down unit by unit.
	/// </summary>
	public static CFixedPoint OfficersAtStart(CCountry country, IEnumerable<CUnit> units)
	{
		long officers = 0;
		foreach (CUnit unit in units)
		{
			long own = OfficersOf(unit).Raw;
			if (country.OfficersRatio != CFixedPoint.Zero)
			{
				own = own * country.OfficersRatio.Raw / CFixedPoint.Scale;
			}
			officers += own;
		}
		return new CFixedPoint(officers);
	}

	/// <summary>The officers a unit's own brigades call for.</summary>
	public static CFixedPoint OfficersOf(CUnit unit)
	{
		CFixedPoint officers = CFixedPoint.Zero;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (CountsTowardsItsUnit(unit, subUnit))
			{
				officers += subUnit.Stats[CSubUnitType.Officers];
			}
		}
		return officers;
	}

	/// <summary>
	/// Whether a brigade's figures are added into its unit's. On land, one that holds no
	/// frontage - a headquarters, engineers, anti-air - stops counting once it is all
	/// but destroyed or has no organisation left. Ships and wings always count.
	/// </summary>
	public static bool CountsTowardsItsUnit(CUnit unit, CSubUnit subUnit)
	{
		bool isLand = unit.Kind != EUnitKind.Navy && unit.Kind != EUnitKind.Air;
		return !isLand
			|| subUnit.Stats[CSubUnitType.CombatWidth] > CFixedPoint.Zero
			|| (subUnit.Strength >= LeastStrengthToCount && subUnit.Organisation >= CFixedPoint.One);
	}
}
