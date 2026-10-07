using System;

namespace OpenHOI3.Sim;

/// <summary>The six things a country divides its IC between, in the original's order.</summary>
public enum EProductionCategory
{
	LendLease,
	ConsumerGoods,
	Production,
	Supplies,
	Reinforcement,
	Upgrades,
}

/// <summary>
/// The daily split of a country's IC between its six shares, and what spending IC costs.
/// See <c>docs/mechanics/production.md</c>.
///
/// Consumer goods and supplies are here. The build queue, reinforcement and upgrades
/// spend IC on units and wait for units; lend-lease has not been read. IC in those four
/// shares is wasted, and wasted IC costs nothing.
/// </summary>
public static class CProduction
{
	public const int CategoryCount = 6;

	private const string WarDemand = "war_consumer_goods_demand";
	private const string PeaceDemand = "peace_consumer_goods_demand";
	private const string DissentChange = "dissent";
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
		CFixed15 made = country.ProductionShares[(int)EProductionCategory.Supplies] * CFixed15.FromInt(country.TotalIc);
		made *= CFixed15.FromThousandths(state.Defines[CResources.EconomySection, IcToSupplies]);

		// The law's bonus and the technology's are added to each other, not multiplied.
		CFixedPoint bonus = country.Modifier[GlobalSupplies] + country.Technology[TechnologySupplies];
		made *= CFixed15.FromThousandths(bonus) + CFixed15.One;
		return made.ToThousandths();
	}

	/// <summary>
	/// The IC one of a country's shares spends in a day, in thousandths, which is what it
	/// is charged resources for. Consumer goods and supplies spend all they are given;
	/// nothing spends the other four yet.
	/// </summary>
	public static CFixedPoint Spent(CCountry country, EProductionCategory category)
	{
		CFixed15 share = country.ProductionShares[(int)category];
		return category switch
		{
			// The share is turned into thousandths before it is multiplied out, which is
			// how the original's figures come to what they do.
			EProductionCategory.ConsumerGoods => new CFixedPoint(share.ToThousandths().Raw * country.TotalIc),
			EProductionCategory.Supplies => (share * CFixed15.FromInt(country.TotalIc)).ToThousandths(),
			_ => CFixedPoint.Zero,
		};
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
			}
		}
	}

	/// <summary>
	/// One day of every country's shares, in the order the countries are declared and,
	/// within a country, in the order of the shares.
	/// </summary>
	public static void RunDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.Usage.Clear();
			if (state.Stockpile(country) is not { } stockpile || !TakesPart(state, country))
			{
				continue;
			}

			// A shortage costs IC across the board, before any of it is shared out.
			int supported = (int)Math.Min(int.MaxValue, SupportedIc(stockpile).ToInt());
			if (country.TotalIc > supported)
			{
				country.TotalIc = supported;
			}

			RunConsumerGoods(state, country, stockpile);
			RunSupplies(state, country, stockpile);
		}
	}

	private static void RunConsumerGoods(CGameState state, CCountry country, CGoodsPool stockpile)
	{
		CFixed15 share = country.ProductionShares[(int)EProductionCategory.ConsumerGoods];

		// Money comes of having IC at all, not of the share.
		if (country.TotalIc != 0)
		{
			CFixedPoint money = CResources.MoneyPerDay(state, country);
			country.HomeProduced[EGoods.Money] = money;
			stockpile[EGoods.Money] += money;
		}

		Charge(country, stockpile, Spent(country, EProductionCategory.ConsumerGoods));

		// One for one: a share a point under the demand is a point of dissent a day.
		CFixed15 change = CFixed15.FromThousandths(ConsumerGoodsDemand(state, country)) - share
			+ CFixed15.FromThousandths(country.Modifier[DissentChange]);
		CFixedPoint dissent = country.Dissent + change.ToThousandths();
		country.Dissent = dissent < CFixedPoint.Zero ? CFixedPoint.Zero : dissent;
	}

	private static void RunSupplies(CGameState state, CCountry country, CGoodsPool stockpile)
	{
		// They are made whatever the stockpile holds, and charged for afterwards.
		CFixedPoint made = SuppliesMade(state, country);
		country.HomeProduced[EGoods.Supplies] = made;
		stockpile[EGoods.Supplies] += made;

		Charge(country, stockpile, Spent(country, EProductionCategory.Supplies));
	}

	/// <summary>
	/// Takes the resources that spending this much IC costs - 1 metal, 2 energy and half
	/// a rare material each - and books them as the day's usage. No more is taken than
	/// the stockpile can pay for.
	/// </summary>
	private static void Charge(CCountry country, CGoodsPool stockpile, CFixedPoint ic)
	{
		CFixedPoint supported = SupportedIc(stockpile);
		if (ic > supported)
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
