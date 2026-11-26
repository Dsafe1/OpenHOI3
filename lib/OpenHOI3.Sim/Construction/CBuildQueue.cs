using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Construction;

/// <summary>
/// The build queue: what a line costs and how long it takes, what the production share
/// of a country's IC does to it each day, and what finishing a unit does. See
/// <c>docs/mechanics/buildqueue.md</c>.
///
/// The arithmetic is the original's step for step. The day's work on a line is worked
/// out in the shares' kind of number and turned into thousandths at the end, which is
/// what a part-paid line's progress comes of.
/// </summary>
public static class CBuildQueue
{
	/// <summary>How much of its practical building one of a type teaches.</summary>
	public const string CompletionSize = "completion_size";

	/// <summary>The rebels' units are delivered though they hold no province.</summary>
	private const string Rebels = "REB";

	private const long MostExperience = 100000;

	/// <summary>A brigade is never taken to need less than a twentieth of its men.</summary>
	private const long LeastManpowerFactor = 50;

	/// <summary>A full day's work.</summary>
	private const long FullDay = CFixedPoint.Scale;

	/// <summary>
	/// What a reserve is kept at under a country's conscription law as it stands: 1 plus
	/// <c>reserves_penalty_size</c>, which is below nothing.
	/// </summary>
	public static CFixedPoint ReservesFactor(CCountry country)
	{
		return new CFixedPoint(CFixedPoint.Scale + country.Modifier[CUnits.ReservesPenaltySize].Raw);
	}

	/// <summary>
	/// The brigade an order would make, with the order's equipment and the figures that
	/// come of it, and nothing else decided: no strength, no name, no number.
	/// </summary>
	/// <param name="levels">The levels the order gives it, all its technologies together.</param>
	public static CSubUnit AsBuilt(CBrigadeOrder order, bool isReserve, out long levels)
	{
		var subUnit = new CSubUnit(order.Type) { IsReserve = isReserve };
		levels = 0;
		// The order's levels go to the type's technologies in turn. An order with fewer
		// leaves the last of them at nothing.
		for (int index = 0; index < order.Model.Count && index < order.Type.Technologies.Count; index++)
		{
			subUnit.TechnologyLevels[order.Type.Technologies[index].Name] = order.Model[index];
			levels += order.Model[index];
		}
		CUnits.RebuildStats(subUnit);
		return subUnit;
	}

	/// <summary>
	/// The equipment a brigade ordered today would have: the country's own level of
	/// every technology of the type.
	/// </summary>
	public static void SetModelToCountrys(CCountry country, CBrigadeOrder order)
	{
		order.Model.Clear();
		foreach (CTechnology technology in order.Type.Technologies)
		{
			order.Model.Add(country.TechnologyLevels.GetValueOrDefault(technology.Name));
		}
	}

	/// <summary>
	/// The equipment a historical model gives: for each technology of the type, in the
	/// type's order, the model's level - and nothing at all for one the model does not
	/// list, so that what follows moves up a place. That is the original's. An enhanced game's gives such a
	/// technology nought (<see cref="Rules.EFix.QueuedHistoricalModel"/>), so the list has a level for every
	/// technology of the type and each is built on its own - as the model gives a unit read onto the map.
	/// </summary>
	public static void SetModelToHistorical(CGameState state, CCountry country, CBrigadeOrder order, int model)
	{
		if (model < 0 || model >= CHistoricalModels.Count(state, country, order.Type))
		{
			return;
		}
		order.Model.Clear();
		foreach (CTechnology technology in order.Type.Technologies)
		{
			if (CHistoricalModels.Level(state, country, order.Type, model, technology) is { } level)
			{
				order.Model.Add(level);
			}
			else if (state.Rules.Has(Rules.EFix.QueuedHistoricalModel))
			{
				order.Model.Add(0);
			}
		}
	}

	/// <summary>
	/// Works out what a line costs a day and how many days one of its unit takes, for
	/// the country as it is now. A unit of one brigade costs what the brigade costs and
	/// takes what it takes. One of several takes as long as its slowest brigade, and
	/// costs a day what would pay for all of them over that time.
	/// </summary>
	public static void Recalculate(CCountry country, CConstruction construction)
	{
		// A convoy line is priced once, when it is ordered, and passed over here (convoylines.md, section 2).
		if (construction.IsConvoy)
		{
			return;
		}
		// A building line has a price of its own (buildinglines.md, section 3).
		if (construction.IsBuilding)
		{
			CBuildingLines.Price(country, construction);
			return;
		}
		if (construction.Brigades.Count != 0 && construction.Brigades[0].Upgrade is { } upgrade)
		{
			PriceUpgrade(country, construction, upgrade);
			return;
		}
		long cost = 0;
		long duration = 0;
		bool several = construction.Brigades.Count > 1;
		foreach (CBrigadeOrder order in construction.Brigades)
		{
			CSubUnit subUnit = AsBuilt(order, construction.IsReserve, out long levels);
			long days = CBuildCosts.BuildTime(country, subUnit, levels) * CFixedPoint.Scale;
			long daily = CBuildCosts.BuildCost(country, subUnit, levels);
			cost += several ? daily * days / CFixedPoint.Scale : daily;
			duration = Math.Max(duration, days);
		}
		if (several && duration > 0)
		{
			cost = cost * CFixedPoint.Scale / duration;
		}
		construction.Duration = new CFixedPoint(duration);
		construction.Cost = new CFixedPoint(cost);
		construction.PracticalFactor = CFixedPoint.One;
	}

	/// <summary>
	/// A change of type: the new brigade's cost less a share of the old type's at the new levels, by how much of its strength it
	/// had, and its days less a share of the old type's; what it teaches goes down with what it costs. See
	/// <c>docs/mechanics/upgrades.md</c>, section 3.
	/// </summary>
	private static void PriceUpgrade(CCountry country, CConstruction construction, CBrigadeUpgradeOrder upgrade)
	{
		CSubUnit built = AsBuilt(construction.Brigades[0], construction.IsReserve, out long levels);
		CSubUnit old = AsBuilt(new CBrigadeOrder(upgrade.From), construction.IsReserve, out long _);
		long newCost = CBuildCosts.BuildCost(country, built, levels);
		long oldCost = CBuildCosts.BuildCost(country, old, levels);
		long credit = (upgrade.CostFactor.Raw * upgrade.Strength / CFixedPoint.Scale) * oldCost / CFixedPoint.Scale;
		long cost = newCost - credit;
		if (cost < 1)
		{
			cost = 0;
		}
		long practical = CFixedPoint.Scale;
		if (newCost != 0)
		{
			practical = (cost * CFixedPoint.Scale / newCost) * upgrade.PracticalFactor.Raw / CFixedPoint.Scale;
		}
		long days = CBuildCosts.BuildTime(country, built, levels) * CFixedPoint.Scale
			- CBuildCosts.BuildTime(country, old, levels) * CFixedPoint.Scale * upgrade.TimeFactor.Raw / CFixedPoint.Scale;
		if (days < 1)
		{
			days = 0;
		}
		construction.Cost = new CFixedPoint(cost);
		construction.Duration = new CFixedPoint(days);
		construction.PracticalFactor = new CFixedPoint(practical);
	}

	/// <summary>
	/// What ordering a unit would come to, with nothing ordered: a line of those types
	/// priced for the country as it is now, with the men one of it takes, and not in
	/// any queue. It is what a window shows before the order is given.
	/// </summary>
	/// <param name="models">
	/// For each type, the level of each of its technologies to build it with, or
	/// nothing for the country's own.
	/// </param>
	public static CConstruction Quote(CCountry country, IReadOnlyList<CSubUnitType> types, bool isReserve,
		IReadOnlyList<IReadOnlyList<int>?>? models = null)
	{
		var construction = new CConstruction(country.Tag) { IsReserve = isReserve };
		if (isReserve)
		{
			construction.ReservesFactor = ReservesFactor(country);
		}
		for (int index = 0; index < types.Count; index++)
		{
			var order = new CBrigadeOrder(types[index]);
			if (models is not null && index < models.Count && models[index] is { } model)
			{
				order.Model.AddRange(model);
			}
			else
			{
				SetModelToCountrys(country, order);
			}
			construction.Brigades.Add(order);
		}
		Recalculate(country, construction);
		construction.Manpower = ManpowerFor(country, construction);
		return construction;
	}

	/// <summary>The IC a day every line of a country's queue costs, added up.</summary>
	public static CFixedPoint QueueCost(CCountry country)
	{
		long cost = 0;
		foreach (CConstruction construction in country.Constructions)
		{
			cost += construction.Cost.Raw;
		}
		return new CFixedPoint(cost);
	}

	/// <summary>
	/// Every line of a country's queue priced again, as it is after a change of law. The
	/// work done on each stays as it was, and so does the production share's need: the
	/// original's change of law counts nothing again, so the need is the queue at its old
	/// prices until the day's share, or a line ordered or cancelled, counts it
	/// (<c>docs/mechanics/buildqueue.md</c>, section 4; <see cref="UpdateCost"/>).
	/// </summary>
	public static void RecalculateAll(CCountry country)
	{
		foreach (CConstruction construction in country.Constructions)
		{
			Recalculate(country, construction);
		}
	}

	/// <summary>
	/// What entering a loaded game does with a country's queue, the production share being run with nothing to spend:
	/// each building line asked whether it may still be built and taken out where not, silently, as at the head of the
	/// day's share, and the need counted again. Nothing is paid and nothing moves on (<c>buildqueue.md</c>, section 4).
	/// </summary>
	public static void RunAtEntry(CGameState state, CCountry country)
	{
		country.Constructions.RemoveAll(line => line.IsBuilding && !CBuildingLines.MayStillBeBuilt(state, country, line));
		UpdateCost(country);
	}

	/// <summary>
	/// The men one of a line's unit takes: each brigade's own figure, and for reserves
	/// only the share of it the conscription law leaves.
	/// </summary>
	public static CFixedPoint ManpowerFor(CCountry country, CConstruction construction)
	{
		long men = 0;
		foreach (CBrigadeOrder order in construction.Brigades)
		{
			CSubUnit subUnit = AsBuilt(order, construction.IsReserve, out long _);
			men += ManpowerOf(country, subUnit, construction.IsReserve).Raw;
		}
		return new CFixedPoint(men);
	}

	/// <summary>
	/// The men one brigade, ship or wing takes its country to raise: its own figure,
	/// and for a reserve the share of it the conscription law leaves - never less than a
	/// twentieth.
	/// </summary>
	public static CFixedPoint ManpowerOf(CCountry country, CSubUnit subUnit, bool isReserve)
	{
		long factor = CFixedPoint.Scale;
		if (isReserve)
		{
			factor += country.Modifier[CUnits.ReservesPenaltySize].Raw;
		}
		factor = Math.Max(factor, LeastManpowerFactor);
		return new CFixedPoint(subUnit.Stats[CSubUnitStats.BuildCostManpower].Raw * factor / CFixedPoint.Scale);
	}

	/// <summary>
	/// What a new game does with the lines its orders of battle gave: each is priced by
	/// the country as it starts, and what its file called its duration is taken as the
	/// days it still has to go.
	/// </summary>
	public static void SetUpAtStart(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CConstruction construction in country.Constructions)
			{
				foreach (CBrigadeOrder order in construction.Brigades)
				{
					if (order.HistoricalModel != CSubUnit.NoModel)
					{
						SetModelToHistorical(state, country, order, order.HistoricalModel);
					}
				}

				long left = construction.Duration.Raw;
				Recalculate(country, construction);
				construction.Progress = new CFixedPoint(Math.Max(0, construction.Duration.Raw - left));
			}
			UpdateCost(country);
		}
	}

	/// <summary>
	/// The production share's need: a full day of every line, at the price each has now. Counted only where the
	/// original's production share counts it - at the end of each day's share and of entering's run with nothing to
	/// spend, and when a line is ordered or cancelled - so a line priced again in between, by a change of law, leaves it
	/// as last counted (<c>docs/mechanics/buildqueue.md</c>, section 4).
	/// </summary>
	public static void UpdateCost(CCountry country)
	{
		// Each line's cost is turned into the shares' kind of number before it is added.
		long cost = 0;
		foreach (CConstruction construction in country.Constructions)
		{
			cost += CFixed15.FromThousandths(construction.Cost).Raw;
		}
		country.ProductionCost = new CFixed15(cost).ToThousandths();
	}

	/// <summary>
	/// One day of the share. Each line in turn is given a day of its cost, or as much of
	/// one as is left, and a line that is finished delivers its unit. Answers the IC
	/// spent.
	/// </summary>
	/// <param name="allocation">The IC the share has to spend today.</param>
	/// <param name="pay">
	/// Charges the resources for so much IC, or answers false and charges nothing when
	/// the stockpile cannot pay for it. A line refused gets nothing that day and leaves
	/// the share's IC for the lines after it, which may need less.
	/// </param>
	public static CFixedPoint RunDaily(CGameState state, CCountry country, CFixed15 allocation, Func<CFixedPoint, bool>? pay = null,
		CFixed15 freePool = default)
	{
		long pool = allocation.Raw;
		long free = freePool.Raw;
		bool withFree = free > 0;
		long spent = 0;
		bool delivered = false;

		// First, each building line asked whether it may still be built, and taken out where not, silently
		// (buildinglines.md, section 4).
		country.Constructions.RemoveAll(line => line.IsBuilding && !CBuildingLines.MayStillBeBuilt(state, country, line));

		foreach (CConstruction construction in new List<CConstruction>(country.Constructions))
		{
			long need = CFixed15.FromThousandths(construction.Cost).Raw;
			if (pool <= 0)
			{
				AddProgress(country, construction, 0);
				continue;
			}

			if (need <= 0)
			{
				AddProgress(country, construction, FullDay);
			}
			else if (withFree)
			{
				// Lend-lease's free IC first, unasked; then the rest of the day's cost, as the share allows and
				// the stockpile pays (docs/mechanics/aid.md, section 3).
				long first = Math.Min((Math.Max(free, 0) << CFixed15.Places) / need, CFixed15.Scale);
				long freeSpent = (first * need) >> CFixed15.Places;
				pool -= freeSpent;
				free -= freeSpent;
				long amount = new CFixed15(first).ToThousandths().Raw;
				if (first < CFixed15.Scale && pool > 0)
				{
					long rest = Math.Min((pool << CFixed15.Places) / (need - freeSpent), CFixed15.Scale);
					long extra = (rest * (CFixed15.Scale - first)) >> CFixed15.Places;
					var wanted = new CFixed15((extra * need) >> CFixed15.Places).ToThousandths();
					if (pay is null || pay(wanted))
					{
						spent += wanted.Raw;
						pool -= need - freeSpent;
						amount += new CFixed15(extra).ToThousandths().Raw;
						if (amount >= FullDay - 1)
						{
							amount = FullDay;
						}
					}
				}
				AddProgress(country, construction, amount);
			}
			else
			{
				long share = Math.Min((pool << CFixed15.Places) / need, CFixed15.Scale);
				var wanted = new CFixed15((share * need) >> CFixed15.Places).ToThousandths();
				long amount = 0;
				if (pay is null || pay(wanted))
				{
					spent += wanted.Raw;
					// The whole of a day's cost comes off, paid in full or not: a line
					// that takes the last of the share leaves nothing for the next.
					pool -= need;
					amount = new CFixed15(share).ToThousandths().Raw;
				}
				AddProgress(country, construction, amount);
			}

			if ((construction.Progress >= construction.Duration || state.InstantConstruction) && Deliver(state, country, construction))
			{
				country.Constructions.Remove(construction);
				CBrigadeUpgrades.LineEnded(state, construction);
				delivered = true;
			}
		}

		if (delivered)
		{
			// A line finished has taught its practical, which makes the rest cheaper: the country's modifier is made
			// again and every line priced again (buildinglines.md, section 5).
			Modifiers.CCountryModifiers.RebuildOne(state, country);
			foreach (CConstruction construction in country.Constructions)
			{
				Recalculate(country, construction);
			}
		}
		UpdateCost(country);
		return new CFixedPoint(spent);
	}

	/// <summary>
	/// Moves a line on by so much of a day's work, in thousandths, and gives the unit
	/// being built that share of its country's starting experience.
	/// </summary>
	public static void AddProgress(CCountry country, CConstruction construction, long amount)
	{
		long fraction = 0;
		if (amount != 0)
		{
			fraction = construction.Duration.Raw == 0 ? -1 : amount * CFixedPoint.Scale / construction.Duration.Raw;
		}
		// A building or a convoy gathers no experience, and counts none of its building towards it: the two figures are a
		// unit line's alone, as the original keeps them on its military line and a save writes them for no other
		// (reversing/findings/FINDINGS-savegame.md, C.8).
		if (construction.IsUnit)
		{
			construction.AccumulatedExperience += new CFixedPoint(ExperienceRate(country, construction) * fraction / CFixedPoint.Scale);
			construction.AccumulatedProgress += new CFixedPoint(fraction);
		}
		construction.Progress += new CFixedPoint(amount);

		if (amount >= FullDay)
		{
			construction.Status = CFixedPoint.One;
		}
		else
		{
			construction.Status = amount > 0 && construction.Cost > CFixedPoint.Zero ? new CFixedPoint(amount) : CFixedPoint.Zero;
		}
	}

	/// <summary>
	/// The experience a whole building is worth: what the country's training law gives
	/// a new unit, and for reserves the share of it the line was queued with.
	/// </summary>
	private static long ExperienceRate(CCountry country, CConstruction construction)
	{
		long rate = country.Modifier[CUnits.UnitStartExperience].Raw;
		if (construction.IsReserve)
		{
			rate = construction.ReservesFactor.Raw * rate / CFixedPoint.Scale;
		}
		return rate;
	}

	/// <summary>
	/// Makes the unit a line has finished and puts it with the units waiting to be
	/// placed. Answers whether the line is done with: a series goes on to its next.
	/// </summary>
	public static bool Deliver(CGameState state, CCountry country, CConstruction construction)
	{
		// A convoy line's item asks nothing of its country (convoylines.md, section 3).
		if (construction.IsConvoy)
		{
			return CConvoyLines.Finish(state, country, construction);
		}
		// A country that holds nothing gets nothing, and the line is dropped.
		if (!country.Tag.Value.Equals(Rebels, StringComparison.OrdinalIgnoreCase) && !OwnsAProvince(state, country))
		{
			return true;
		}
		if (construction.IsBuilding)
		{
			return CBuildingLines.Finish(state, country, construction);
		}
		if (construction.Brigades.Count == 0)
		{
			return true;
		}

		// Whatever of the building was never counted is counted now, at today's rate.
		if (construction.AccumulatedProgress.Raw < CFixedPoint.Scale)
		{
			long missing = CFixedPoint.Scale - construction.AccumulatedProgress.Raw;
			construction.AccumulatedExperience += new CFixedPoint(missing * ExperienceRate(country, construction) / CFixedPoint.Scale);
		}

		EUnitKind kind = construction.Brigades[0].Type.Branch switch
		{
			EUnitBranch.Naval => EUnitKind.Navy,
			EUnitBranch.Air => EUnitKind.Air,
			_ => EUnitKind.Division,
		};
		var unit = new CUnit(kind, country.Tag) { Id = construction.UnitId };
		string name = kind == EUnitKind.Division ? construction.Name : "";

		foreach (CBrigadeOrder order in construction.Brigades)
		{
			if (name.Length < 2)
			{
				name = order.Name;
			}

			CSubUnit subUnit = AsBuilt(order, construction.IsReserve, out long _);
			subUnit.Id = order.Id;
			subUnit.Name = order.Name;
			subUnit.Experience = new CFixedPoint(Math.Min(MostExperience,
				Math.Max(order.Experience.Raw, construction.AccumulatedExperience.Raw)));
			if (construction.Builder is { } builder && builder != country.Tag)
			{
				subUnit.Builder = builder;
			}

			long gain = subUnit.Stats[CompletionSize].Raw;
			if (construction.IsReserve)
			{
				gain = construction.ReservesFactor.Raw * gain / CFixedPoint.Scale;
			}
			gain = construction.PracticalFactor.Raw * gain / CFixedPoint.Scale;
			CResearch.GainAbility(state, country, order.Type.OnCompletion, new CFixedPoint(gain));

			long strength = subUnit.Stats[CSubUnitType.MaxStrength].Raw * construction.ReservesFactor.Raw / CFixedPoint.Scale;
			strength = Math.Max(0, Math.Min(strength, CUnits.MaxStrength(state, country, subUnit).Raw));
			subUnit.Strength = new CFixedPoint(strength);
			subUnit.Highest = subUnit.Strength;

			// It is given its organisation before it is put in its unit, and so with no
			// regard to the officers its country has: the first hours on the map see to that.
			subUnit.Organisation = CFixedPoint.Max(CFixedPoint.Zero,
				COrganisation.Ceiling(state, country, subUnit, penaltyAtWar: true, penaltyAlways: true));
			subUnit.Unit = unit;
			unit.AddSubUnit(subUnit);
		}
		unit.Name = name;
		// Its deployment takes the number after the unit's, which the line passed over for it (buildqueue.md, section 4).
		unit.DeploymentId = construction.UnitId + 1;
		country.Deployments.Add(unit);

		// A line ordered for a unit joins it where it may, and otherwise waits to be placed like any other (upgrades.md, section 4).
		if (construction.Target != 0 && CUnitFinder.Find(state, country.Tag, construction.Target) is { } target)
		{
			CDeployment.JoinOrdered(state, country, unit, target);
		}

		// The line is named for its next unit as this one is delivered, before the series is counted down
		// (buildqueue.md, section 6). A division of several brigades with more to come is named afresh, as a new order
		// would be, and its brigades keep theirs. Every other line - a ship's, a wing's, one brigade's, and the last unit
		// of any line - has its first brigade named afresh and takes that name itself: so a line that ends draws one
		// name nothing will bear, as the original's does.
		if (kind == EUnitKind.Division && construction.Size > 1 && construction.Brigades.Count > 1)
		{
			construction.Name = CUnitNames.ForDivision(state, country, construction.Brigades[0].Type);
			CUnitNames.Register(country, construction.Name);
		}
		else
		{
			string next = CUnitNames.ForSubUnit(state, country, construction.Brigades[0].Type);
			construction.Brigades[0].Name = next;
			CUnitNames.Register(country, next);
			construction.Name = next;
			CUnitNames.Register(country, next);
		}

		construction.AccumulatedExperience = CFixedPoint.Zero;
		construction.AccumulatedProgress = CFixedPoint.Zero;
		construction.Progress -= CFixedPoint.Min(construction.Duration, construction.Progress);
		construction.Size--;
		if (construction.Size <= 0)
		{
			return true;
		}

		// The next of the series is a new unit with new numbers, priced by the country as it is now.
		construction.UnitId = ++state.LastUnitId;
		state.LastUnitId += 2;
		foreach (CBrigadeOrder order in construction.Brigades)
		{
			order.Id = ++state.LastUnitId;
		}
		Recalculate(country, construction);
		if (construction.IsReserve)
		{
			construction.ReservesFactor = ReservesFactor(country);
		}
		return false;
	}

	/// <summary>
	/// The days of work a line still has to go at the pace it was last paid for, rounded
	/// up to whole days, or null for a line that was given nothing and so will never be
	/// done. A line no day has touched is taken to be paid in full.
	/// </summary>
	public static long? DaysLeft(CConstruction construction)
	{
		long pace = construction.Progress == CFixedPoint.Zero ? CFixedPoint.Scale : construction.Status.Raw;
		if (pace == 0)
		{
			return null;
		}
		long left = (construction.Duration.Raw - construction.Progress.Raw) * CFixedPoint.Scale / pace;
		long days = left / CFixedPoint.Scale;
		return left - days * CFixedPoint.Scale > 0 ? days + 1 : days;
	}

	private static bool OwnsAProvince(CGameState state, CCountry country)
	{
		foreach (CProvince province in state.Provinces)
		{
			if (province.Owner is { } owner && owner.Equals(country.Tag))
			{
				return true;
			}
		}
		return false;
	}
}
