using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Economy;

/// <summary>
/// The AI's production, for the countries nobody plays: a production minister that works once a day in an hour of its
/// country's own, making its desperation again three days a month and running the install's Lua
/// <c>ProductionMinister_Tick</c>, which orders convoys, units and buildings; and at the end of every such country's
/// day the Lua's <c>BalanceProductionSliders</c>, which sets its six shares. See <c>docs/mechanics/ministers.md</c>,
/// sections 4 and 5.
/// </summary>
public static class CProductionAi
{
	/// <summary>The production minister's place in the day: its country's number plus five.</summary>
	private const int HourOffset = 5;

	/// <summary>The desperation is made again on every fourteenth day of the month, counting the 1st as nought.</summary>
	private const int DesperationEvery = 14;

	/// <summary>A line whose day is paid this far is never moved.</summary>
	private const long WellPaid = 980;

	/// <summary>Over this desperation, a land line due within this many days goes to the front.</summary>
	private const long Desperate = 600;
	private const long SoonDays = 120;

	/// <summary>
	/// The hour's work of every production minister, in the countries' order, as the tech minister's (<see
	/// cref="CResearchAi.RunHourly"/>), in its hour <c>(number + 5) mod 24</c>.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		if (state.IsBeingMade)
		{
			return;
		}
		BalanceDue(state);
		int hour = (int)(state.TotalHours % CGameState.HoursPerDay);
		var due = new List<CCountry>();
		foreach (CCountry country in state.Countries)
		{
			if (!CResearchAi.Works(state, country, country.ProductionCountdown, Settings.EAutomation.Production))
			{
				continue;
			}
			if (--country.ProductionCountdown > 0)
			{
				continue;
			}
			country.ProductionCountdown = 1;
			if (CResearchAi.Hour(country, HourOffset) == hour)
			{
				Prepare(state, country);
				due.Add(country);
			}
		}
		// The hour's countries' scripts side by side, as the original's AI pass runs them (ministers.md, *Ours*).
		state.AiScripts?.RunSideBySide(state, due, Salt, (scripts, country) => scripts.RunProduction(state, country));
	}

	/// <summary>Which minister a run of the production minister is, for the seed of its own generator on a worker.</summary>
	private const uint Salt = 4;

	/// <summary>The minister's day: its desperation where due, then the script.</summary>
	public static void Work(CGameState state, CCountry country)
	{
		Prepare(state, country);
		state.AiScripts?.RunProduction(state, country);
	}

	/// <summary>Its desperation, where due, before the script.</summary>
	private static void Prepare(CGameState state, CCountry country)
	{
		if (country.MinisterDesperation < 0 || (state.Date.Day - 1) % DesperationEvery == 0)
		{
			country.MinisterDesperation = (int)CDesperation.Of(state, country);
		}
	}

	/// <summary>
	/// At the end of every country's day, for each that owns land and is not the rebels - the two days a new game runs
	/// first among them: the AI's sliders where the AI runs its production, and then, where it gives lend-lease to anyone,
	/// the share of it, a failure of the first leaving the second; and a played country's own sliders at the player's
	/// slider automation level, where production is his and the level is above nought (ministers.md, section 5).
	/// </summary>
	public static void BalanceDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			BalanceCountry(state, country);
		}
	}

	/// <summary>
	/// One country's sliders, at the end of its own day: what the later countries' days trade with it is not counted yet,
	/// as the original's call stands last in each country's daily pass (<c>reversing/findings/FINDINGS-liveread.md</c>,
	/// section 141).
	/// </summary>
	public static void BalanceCountry(CGameState state, CCountry country)
	{
		if (state.AiScripts is not { } scripts
			|| country.AiPaused || country.Tag.Value == "REB" || COwnedCounts.Of(state).Count(country.Tag) == 0)
		{
			return;
		}
		if (!Settings.CAutomation.AiRuns(state, country, Settings.EAutomation.Production))
		{
			BalancePlayer(state, country);
		}
		else if (scripts.BalanceProductionSliders(state, country) && country.LendLeaseGiving.Count > 0)
		{
			scripts.BalanceLendLeaseSliders(state, country);
		}
	}

	/// <summary>
	/// A played country's sliders at the player's slider automation level, where production is his and the level is above
	/// nought: the level, and his lend-lease share as it stands, never below nought, handed to the script with the needs.
	/// </summary>
	private static void BalancePlayer(CGameState state, CCountry country)
	{
		int level = state.Automation.Sliders;
		if (level <= 0 || state.AiScripts is not { } scripts
			|| Settings.CAutomation.AiRuns(state, country, Settings.EAutomation.Production))
		{
			return;
		}
		float lendLease = country.ProductionShares[(int)EProductionCategory.LendLease].Raw / (float)CFixed15.Scale;
		scripts.BalanceProductionSliders(state, country, level, lendLease > 0 ? lendLease : 0f);
	}

	/// <summary>
	/// The sliders of each played country set at once by a slider automation just raised above nought, as the production
	/// screen's arrow sets them - here at the next hour, its command having run between hours.
	/// </summary>
	private static void BalanceDue(CGameState state)
	{
		if (!state.Automation.SlidersDue)
		{
			return;
		}
		state.Automation.SlidersDue = false;
		foreach (CCountry country in state.Countries)
		{
			if (state.Played.Contains(country.Tag))
			{
				BalancePlayer(state, country);
			}
		}
	}

	/// <summary>
	/// <c>PrioritizeBuildQueue</c>: over the queue head to tail, a line paid less than 0.980 of its day goes to the front
	/// where it is transports and the country has fewer than half the transports it needs, or where it is land, the
	/// minister's desperation is over 0.6 and it is due within 120 days - a line paid nothing counting as due. Each move
	/// is posted, so those moved at once end at the front in the reverse of their order.
	/// </summary>
	public static void PrioritizeBuildQueue(CGameState state, CCountry country)
	{
		foreach (CConstruction line in country.Constructions)
		{
			if (line.Status.Raw >= WellPaid)
			{
				continue;
			}
			bool move;
			if (line.IsConvoy)
			{
				move = !line.IsEscorts && country.Transports * 2 < country.TransportsNeeded;
			}
			else if (line.IsUnit && country.MinisterDesperation > Desperate && line.Brigades.Count > 0
				&& line.Brigades[0].Type.Branch == EUnitBranch.Land)
			{
				long days = line.Status.Raw == 0 ? -1
					: (line.Duration.Raw - line.Progress.Raw) * CFixedPoint.Scale / line.Status.Raw;
				move = days / CFixedPoint.Scale < SoonDays;
			}
			else
			{
				move = false;
			}
			if (move)
			{
				state.Outbox.Add(new CMoveConstructionCommand(country.Tag, line.Id, EQueueMove.First));
			}
		}
	}

	/// <summary>The ships of transports, or of escorts, the queue's convoy lines are still to deliver: ten an item, times what is left of each run.</summary>
	public static int ShipsUnderConstruction(CGameState state, CCountry country, bool escorts)
	{
		long each = state.Defines[CConvoyLines.Section, CConvoyLines.SizeDefine].Raw / CFixedPoint.Scale;
		long ships = 0;
		foreach (CConstruction line in country.Constructions)
		{
			if (line.IsConvoy && line.IsEscorts == escorts)
			{
				ships += each * line.Size;
			}
		}
		return (int)ships;
	}

	/// <summary>
	/// The brigades of each type the queue's military lines are still to deliver: each line's brigades times its size.
	/// </summary>
	public static Dictionary<CSubUnitType, int> InProduction(CCountry country)
	{
		var counts = new Dictionary<CSubUnitType, int>();
		foreach (CConstruction line in country.Constructions)
		{
			if (!line.IsUnit)
			{
				continue;
			}
			foreach (CBrigadeOrder order in line.Brigades)
			{
				counts[order.Type] = counts.GetValueOrDefault(order.Type) + line.Size;
			}
		}
		return counts;
	}

	/// <summary>The brigades of each type the country has: in every unit it has, and in every unit waiting to be placed.</summary>
	public static Dictionary<CSubUnitType, int> Deployed(CCountry country)
	{
		var counts = new Dictionary<CSubUnitType, int>();
		foreach (CUnit unit in country.Deployments)
		{
			Count(counts, unit);
		}
		foreach (CUnit unit in country.AllUnits())
		{
			Count(counts, unit);
		}
		return counts;
	}

	private static void Count(Dictionary<CSubUnitType, int> counts, CUnit unit)
	{
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			counts[brigade.Type] = counts.GetValueOrDefault(brigade.Type) + 1;
		}
	}
}
