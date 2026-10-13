using System;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Economy;

/// <summary>
/// The daily split of a country's IC between its six shares, and what spending IC costs.
/// See <c>docs/mechanics/production.md</c>.
///
/// Consumer goods and supplies are here, and the build queue, reinforcement and
/// upgrades are run from here (<see cref="CBuildQueue"/>, <see cref="CReinforcement"/>,
/// <see cref="CUpgrades"/>). Lend-lease has not been read: IC in that share is wasted,
/// and wasted IC costs nothing.
/// </summary>
public static class CProduction
{
	public const int CategoryCount = 6;

	private const string WarDemand = "war_consumer_goods_demand";
	private const string PeaceDemand = "peace_consumer_goods_demand";
	private const string DissentModifier = "dissent";
	private const string GlobalSupplies = "global_supplies";
	private const string TechnologySupplies = "ic_to_supplies";

	/// <summary>The define that turns IC into supplies. It is in the same section as the money one.</summary>
	public const string IcToSupplies = "IC_TO_SUPPLIES";

	/// <summary>The demand for consumer goods is never outside these, whatever the modifiers say.</summary>
	private static readonly CFixedPoint LeastDemand = CFixedPoint.Parse("0.010");
	private static readonly CFixedPoint MostDemand = CFixedPoint.Parse("0.990");

	/// <summary>The rebels have a tag and no economy.</summary>
	private const string Rebels = "REB";

	/// <summary>
	/// Whether the country works through its shares each day. Every country does but the
	/// rebels - holding a province is not asked for, so one that does not exist yet still
	/// earns, makes and uses, in whoever holds its capital.
	/// </summary>
	public static bool TakesPart(CGameState state, CCountry country)
	{
		return !country.Tag.Value.Equals(Rebels, StringComparison.OrdinalIgnoreCase)
			&& state.Stockpile(country) is not null;
	}

	/// <summary>
	/// The fraction of its IC the country wants on consumer goods. At peace the original
	/// adds a term for its standing army, which is nothing here until there are units.
	/// </summary>
	public static CFixedPoint ConsumerGoodsDemand(CGameState state, CCountry country)
	{
		CFixedPoint demand = country.Modifier[state.IsAtWar(country.Tag) ? WarDemand : PeaceDemand];
		if (demand < LeastDemand)
		{
			return LeastDemand;
		}
		return demand > MostDemand ? MostDemand : demand;
	}

	/// <summary>
	/// Gives every country the shares a new game starts with: consumer goods at what the
	/// country demands, and the rest split equally between the other five.
	/// </summary>
	public static void SetStartingShares(CGameState state)
	{
		CCountryModifiers.Rebuild(state);
		foreach (CCountry country in state.Countries)
		{
			CFixed15 consumerGoods = CFixed15.FromThousandths(ConsumerGoodsDemand(state, country));
			var each = new CFixed15((CFixed15.Scale - consumerGoods.Raw) / (CategoryCount - 1));
			for (int i = 0; i < CategoryCount; i++)
			{
				country.ProductionShares[i] = i == (int)EProductionCategory.ConsumerGoods ? consumerGoods : each;
			}
		}
	}

	/// <summary>
	/// Whether six numbers can be a country's shares: none below nothing, and no more
	/// than one between them. They may add up to a hair under one, as the original's do.
	/// </summary>
	public static bool AreValidShares(ReadOnlySpan<CFixed15> shares)
	{
		if (shares.Length != CategoryCount)
		{
			return false;
		}
		long sum = 0;
		foreach (CFixed15 share in shares)
		{
			if (share < CFixed15.Zero)
			{
				return false;
			}
			sum += share.Raw;
		}
		return sum <= CFixed15.Scale;
	}

	/// <summary>The supplies a country's supply share makes in a day.</summary>
	public static CFixedPoint SuppliesMade(CGameState state, CCountry country)
	{
		return SuppliesMade(state, country, country.ProductionShares[(int)EProductionCategory.Supplies]).ToThousandths();
	}

	/// <summary>The supplies a share of the country's IC would make in a day, before it is turned into thousandths.</summary>
	public static CFixed15 SuppliesMade(CGameState state, CCountry country, CFixed15 share)
	{
		CFixed15 made = share * CFixed15.FromInt(country.TotalIc);
		made *= CFixed15.FromThousandths(state.Defines[CResources.EconomySection, IcToSupplies]);

		// The law's bonus and the technology's are added to each other, not multiplied.
		CFixedPoint bonus = country.Modifier[GlobalSupplies] + country.Technology[TechnologySupplies];
		made *= CFixed15.FromThousandths(bonus) + CFixed15.One;
		return made;
	}

	/// <summary>
	/// The IC one of a country's shares spends in a day, in thousandths, which is what it
	/// is charged resources for. Consumer goods and supplies spend all they are given;
	/// production spends what its queue takes, and reinforcement and upgrades what
	/// their units take; nothing spends lend-lease yet.
	/// </summary>
	public static CFixedPoint Spent(CCountry country, EProductionCategory category)
	{
		CFixed15 share = country.ProductionShares[(int)category];
		return category switch
		{
			// The share is turned into thousandths before it is multiplied out, which is
			// how the original's figures come to what they do.
			EProductionCategory.ConsumerGoods => new CFixedPoint(share.ToThousandths().Raw * country.TotalIc),
			EProductionCategory.Supplies => Given(country, category),
			// These three spend what their work took, which is what the last day's was.
			EProductionCategory.Production => country.ProductionSpent,
			EProductionCategory.Reinforcement => country.ReinforcementSpent,
			EProductionCategory.Upgrades => country.UpgradeSpent,
			_ => CFixedPoint.Zero,
		};
	}

	/// <summary>The IC a share is given to spend today, in thousandths.</summary>
	public static CFixedPoint Given(CCountry country, EProductionCategory category)
	{
		return (country.ProductionShares[(int)category] * CFixed15.FromInt(country.TotalIc)).ToThousandths();
	}

	/// <summary>
	/// What a share wants, in IC: what its slider shows as its need. Consumer goods want
	/// what keeps dissent still; supplies, what makes as much as the units asked for
	/// today; production, a full day of every line in the queue; reinforcement and
	/// upgrades, what a day of their work costs. Lend-lease has no need.
	/// </summary>
	public static CFixedPoint Needed(CGameState state, CCountry country, EProductionCategory category)
	{
		switch (category)
		{
			case EProductionCategory.ConsumerGoods:
				return ConsumerGoodsNeeded(state, country) * country.TotalIc;
			case EProductionCategory.Supplies:
				return SuppliesNeeded(state, country);
			case EProductionCategory.Production:
				return country.ProductionCost;
			case EProductionCategory.Reinforcement:
				return country.ReinforcementCost;
			case EProductionCategory.Upgrades:
				return country.UpgradeCost;
			default:
				return CFixedPoint.Zero;
		}
	}

	/// <summary>The IC that would make the supplies the country's units asked for today.</summary>
	public static CFixedPoint SuppliesNeeded(CGameState state, CCountry country)
	{
		// What it pays a friend for its units on the friend's depots it has to make too,
		// and what it is paid it need not.
		CFixed15 asked = CFixed15.FromThousandths(country.UnitDemand[EGoods.Supplies]
			+ country.TradedAway[EGoods.Supplies] - country.TradedFor[EGoods.Supplies]);
		if (asked <= CFixed15.Zero)
		{
			return CFixedPoint.Zero;
		}
		CFixedPoint bonus = country.Modifier[GlobalSupplies] + country.Technology[TechnologySupplies];
		CFixed15 perIc = CFixed15.FromThousandths(state.Defines[CResources.EconomySection, IcToSupplies])
			* (CFixed15.FromThousandths(bonus) + CFixed15.One);
		if (perIc <= CFixed15.Zero)
		{
			return CFixedPoint.Zero;
		}
		return new CFixed15((asked.Raw << CFixed15.Places) / perIc.Raw).ToThousandths();
	}

	/// <summary>
	/// The IC a stockpile can pay for, in thousandths: the least of its metal, half its
	/// energy and twice its rare materials, and never less than nothing.
	/// </summary>
	public static CFixedPoint SupportedIc(CGoodsPool stockpile)
	{
		long supported = Math.Min(stockpile[EGoods.Metal].Raw,
			Math.Min(stockpile[EGoods.Energy].Raw / 2, stockpile[EGoods.RareMaterials].Raw * 2));
		return new CFixedPoint(Math.Max(0, supported));
	}

	/// <summary>
	/// What the day would bring, without moving anything: the money and the supplies each
	/// country makes, put beside the rest of its home production so that there are
	/// figures to show before the first midnight.
	/// </summary>
	public static void Estimate(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (TakesPart(state, country))
			{
				country.HomeProduced[EGoods.Money] = CResources.MoneyPerDay(state, country);
				country.HomeProduced[EGoods.Supplies] = SuppliesMade(state, country);

				// What the units would ask for and what seeing to them would cost, so
				// that three of the sliders have a need to show.
				(CFixedPoint supplies, CFixedPoint _) = CUnitSupply.DailyUse(state, country);
				country.UnitDemand.Clear();
				country.UnitDemand[EGoods.Supplies] = supplies;
				country.UnitDrawn.Clear();
				country.UnitDrawn[EGoods.Supplies] = supplies;
				CReinforcement.Run(state, country, CFixedPoint.Zero, apply: false);
				CUpgrades.Run(state, country, CFixedPoint.Zero, apply: false);
			}
		}
	}

	/// <summary>
	/// One day of every country's economy, in the order the countries are declared. Within
	/// a country: energy into crude oil, the cut a shortage makes to its IC, crude oil into
	/// fuel, and then its shares in their own order.
	/// </summary>
	public static void RunDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.ConversionUsed.Clear();
			country.ConversionMade.Clear();
			country.ReinforcementSpent = CFixedPoint.Zero;
			country.UpgradeSpent = CFixedPoint.Zero;
			country.ProductionSpent = CFixedPoint.Zero;
			if (state.Stockpile(country) is not { } stockpile || !TakesPart(state, country))
			{
				country.Usage.Clear();
				continue;
			}

			// The conversions are part of a country's own day, which only a country that
			// exists has - one owning a province, or a government in exile. The first still
			// has yesterday's usage to go by.
			bool exists = Countries.CCountryLists.OwnedCount(state, country.Tag) > 0 || country.InExile;
			if (exists)
			{
				CConversions.EnergyIntoCrudeOil(country, stockpile);
			}

			// A shortage costs IC across the board, before any of it is shared out - for
			// a country that exists. One that does not has no day of its own in which its
			// IC is worked out again, and keeps what it has.
			int supported = (int)Math.Min(int.MaxValue, SupportedIc(stockpile).ToInt());
			if (exists && country.TotalIc > supported)
			{
				country.TotalIc = supported;
			}
			// Then the whole IC of the lend-lease it holds, after the cap and needing no resources.
			if (exists)
			{
				Aid.CLendLeaseIc.Receive(country);
			}

			if (exists)
			{
				CConversions.CrudeOilIntoFuel(state, country, stockpile);
			}

			country.Usage.Clear();
			// Lend-lease is the first of the six.
			Aid.CLendLeaseIc.Give(state, country, stockpile, ic => Charge(country, stockpile, ic, whateverItHolds: true));
			RunConsumerGoods(state, country, stockpile);
			RunBuildQueue(state, country, stockpile);
			RunSupplies(state, country, stockpile);
			RunReinforcement(state, country, stockpile);
			RunUpgrades(state, country, stockpile);
		}
	}

	/// <summary>
	/// The share that builds pays line by line, and a line the stockpile cannot pay for
	/// that day gets nothing.
	/// </summary>
	private static void RunBuildQueue(CGameState state, CCountry country, CGoodsPool stockpile)
	{
		CFixed15 share = country.ProductionShares[(int)EProductionCategory.Production];
		CFixed15 allocation = share * CFixed15.FromInt(country.TotalIc);
		country.ProductionSpent = CBuildQueue.RunDaily(state, country, allocation, ic =>
		{
			if (SupportedIc(stockpile) < ic)
			{
				return false;
			}
			Charge(country, stockpile, ic);
			return true;
		}, share * CFixed15.FromInt(country.LendLeaseIc));
	}

	/// <summary>
	/// A share's part of the lend-lease IC the country received, in thousandths - what it may spend without
	/// resources (<c>docs/mechanics/aid.md</c>, section 3).
	/// </summary>
	public static CFixedPoint FreePart(CCountry country, EProductionCategory category) =>
		(country.ProductionShares[(int)category] * CFixed15.FromInt(country.LendLeaseIc)).ToThousandths();

	/// <summary>
	/// The share that brings units back to strength pays as it goes, brigade by brigade, its free part covering
	/// the first brigades; whatever the stockpile holds.
	/// </summary>
	private static void RunReinforcement(CGameState state, CCountry country, CGoodsPool stockpile)
	{
		long free = FreePart(country, EProductionCategory.Reinforcement).Raw;
		country.ReinforcementSpent = CReinforcement.Run(state, country,
			Given(country, EProductionCategory.Reinforcement), apply: true, ic =>
			{
				long charge = ic.Raw;
				if (free > 0)
				{
					charge = ic.Raw - free;
					free -= ic.Raw;
				}
				if (charge > 0)
				{
					Charge(country, stockpile, new CFixedPoint(charge), whateverItHolds: true);
				}
			});
	}

	/// <summary>The share that re-equips them pays for the whole day at the end of it, less its free part, whatever the stockpile holds.</summary>
	private static void RunUpgrades(CGameState state, CCountry country, CGoodsPool stockpile)
	{
		country.UpgradeSpent = CUpgrades.Run(state, country, Given(country, EProductionCategory.Upgrades), apply: true);
		Charge(country, stockpile, country.UpgradeSpent - FreePart(country, EProductionCategory.Upgrades), whateverItHolds: true);
	}

	private static void RunConsumerGoods(CGameState state, CCountry country, CGoodsPool stockpile)
	{
		// Money comes of having IC at all, not of the share.
		if (country.TotalIc != 0)
		{
			CFixedPoint money = CResources.MoneyPerDay(state, country);
			country.HomeProduced[EGoods.Money] = money;
			stockpile[EGoods.Money] += money;
		}

		Charge(country, stockpile, Spent(country, EProductionCategory.ConsumerGoods) - FreePart(country, EProductionCategory.ConsumerGoods),
			whateverItHolds: true);

		CFixedPoint dissent = country.Dissent + DissentChange(state, country).ToThousandths();
		country.Dissent = dissent < CFixedPoint.Zero ? CFixedPoint.Zero : dissent;
	}

	/// <summary>
	/// What a day does to the country's dissent with its consumer goods share where it
	/// is. One for one: a share a point under the demand is a point of dissent a day.
	/// </summary>
	public static CFixed15 DissentChange(CGameState state, CCountry country)
	{
		return DissentChange(state, country, country.ProductionShares[(int)EProductionCategory.ConsumerGoods]);
	}

	/// <summary>What a day would do to the country's dissent with its consumer goods share at that.</summary>
	public static CFixed15 DissentChange(CGameState state, CCountry country, CFixed15 share)
	{
		return CFixed15.FromThousandths(ConsumerGoodsDemand(state, country)) - share
			+ CFixed15.FromThousandths(country.Modifier[DissentModifier]);
	}

	/// <summary>
	/// The fraction of its IC on consumer goods at which the country's dissent stands
	/// still: what it demands, and what its dissent modifier adds. What the consumer
	/// goods slider shows as its need.
	/// </summary>
	public static CFixedPoint ConsumerGoodsNeeded(CGameState state, CCountry country)
	{
		return ConsumerGoodsDemand(state, country) + country.Modifier[DissentModifier];
	}

	/// <summary>
	/// The IC the country has put where nothing uses it, in whole IC: what each share
	/// but lend-lease was given beyond what it spent, added up and the fraction dropped.
	/// </summary>
	public static int Wasted(CCountry country)
	{
		long spare = 0;
		for (int index = (int)EProductionCategory.ConsumerGoods; index < CategoryCount; index++)
		{
			var category = (EProductionCategory)index;
			long given = (country.ProductionShares[index] * CFixed15.FromInt(country.TotalIc)).ToThousandths().Raw;
			long over = given - Spent(country, category).Raw;
			if (over > 0)
			{
				spare += over;
			}
		}
		return (int)(spare / CFixedPoint.Scale);
	}

	private static void RunSupplies(CGameState state, CCountry country, CGoodsPool stockpile)
	{
		// They are made whatever the stockpile holds, and charged for afterwards - and
		// made where the factories are, so only part of them is in the stockpile at once.
		CFixedPoint made = SuppliesMade(state, country);
		country.HomeProduced[EGoods.Supplies] = made;
		Supply.CSupplyMaking.ShareOut(state, country, stockpile, EGoods.Supplies, made);

		Charge(country, stockpile, Spent(country, EProductionCategory.Supplies) - FreePart(country, EProductionCategory.Supplies),
			whateverItHolds: true);
	}

	/// <summary>
	/// Takes the resources that spending this much IC costs - 1 metal, 2 energy and half
	/// a rare material each - and books them as the day's usage.
	///
	/// Production and lend-lease ask first what the stockpile can pay for. The other
	/// four do not: they are charged whatever the stockpile holds, which may leave it
	/// below nothing until the day's end (<see cref="CStockpile.HoldWithinBounds"/>) -
	/// reinforcement and upgrades too, which the lend-lease reading corrected
	/// (<c>reversing/findings/FINDINGS-tradeui.md</c>, E.1).
	/// </summary>
	private static void Charge(CCountry country, CGoodsPool stockpile, CFixedPoint ic, bool whateverItHolds = false)
	{
		CFixedPoint supported = SupportedIc(stockpile);
		if (!whateverItHolds && ic > supported)
		{
			ic = supported;
		}
		if (ic <= CFixedPoint.Zero)
		{
			return;
		}

		foreach ((EGoods goods, CFixedPoint perIc) in CResources.NeededPerIc)
		{
			CFixedPoint cost = ic * perIc;
			stockpile[goods] -= cost;
			country.Usage[goods] += cost;
		}
	}
}
