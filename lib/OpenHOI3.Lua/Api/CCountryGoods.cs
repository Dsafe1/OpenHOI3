using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// A country's day of one good as the original's AI reads it: what came in less what went out, what
/// went out, and what the AI reckons it needs. Money counts the day's debt repaid and repaid to it - the
/// repaid **taken off** the spending, as the original writes it. The original's tribute columns are not
/// built. See <c>docs/mechanics/lua.md</c>, section 4.2.
/// </summary>
public static class CCountryGoods
{
	private const string TributeUnbuilt = "tribute: the day's goods count none";
	private const string TechnologyIc = "ic_modifier";
	private const string GlobalIc = "global_ic";

	/// <summary><c>GetDailyBalance</c>, thousandths.</summary>
	public static long Balance(CGameApi api, CCountry country, EGoods goods)
	{
		return Income(api, country, goods) - Spending(country, goods);
	}

	/// <summary><c>GetDailyExpense</c>, thousandths: traded away, less repaid, convoyed out, conversion used, sent to and used.</summary>
	public static long Expense(CGameApi api, CCountry country, EGoods goods)
	{
		api.ReportUnbuilt(TributeUnbuilt);
		return Spending(country, goods);
	}

	/// <summary>
	/// The income side of the balance: home production - and convoyed in, but not for supplies or fuel - with
	/// what came back, was repaid to it, was made and was traded for.
	/// </summary>
	public static long Income(CGameApi api, CCountry country, EGoods goods)
	{
		api.ReportUnbuilt(TributeUnbuilt);
		long home = country.HomeProduced[goods].Raw;
		if (goods is not (EGoods.Fuel or EGoods.Supplies))
		{
			home += country.ConvoyedIn[goods].Raw;
		}
		return home + country.SentBack[goods].Raw + Repaid(country.IncomeFromDebt, goods) + country.ConversionMade[goods].Raw
			+ country.TradedFor[goods].Raw;
	}

	private static long Spending(CCountry country, EGoods goods)
	{
		return country.SentTo[goods].Raw - Repaid(country.RepaidAway, goods) + country.Usage[goods].Raw
			+ country.ConversionUsed[goods].Raw + country.ConvoyedOut[goods].Raw + country.TradedAway[goods].Raw;
	}

	/// <summary>A debt's money, which is the only good it moves.</summary>
	private static long Repaid(CFixedPoint money, EGoods goods) => goods == EGoods.Money ? money.Raw : 0;

	/// <summary>
	/// <c>AICalculateExpense</c>: for metal, energy and rare materials, the base IC raised by the
	/// technology's and the modifiers' IC bonus - twice it for energy, half for rare materials - every
	/// step a float; never less than the day's spending.
	/// </summary>
	public static float AiExpense(CGameApi api, CCountry country, EGoods goods)
	{
		float need = 0;
		if (goods is EGoods.Energy or EGoods.Metal or EGoods.RareMaterials)
		{
			float technology = CLuaFloats.SingleThousandths((int)country.Technology[TechnologyIc].Raw);
			float modifier = CLuaFloats.SingleThousandths((int)country.Modifier[GlobalIc].Raw);
			float ic = country.BaseIc;
			need = (float)(((double)technology + modifier) * ic + ic);
			if (goods == EGoods.Energy)
			{
				need = (float)(need * 2.0);
			}
			else if (goods == EGoods.RareMaterials)
			{
				need = (float)(need * 0.5);
			}
		}
		api.ReportUnbuilt(TributeUnbuilt);
		float spent = CLuaFloats.SingleThousandths((int)Spending(country, goods));
		return need < spent ? spent : need;
	}
}
