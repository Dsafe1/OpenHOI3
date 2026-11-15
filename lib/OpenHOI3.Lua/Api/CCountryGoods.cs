using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// A country's day of one good as the original's AI reads it: what came in less what went out, what
/// went out, and what the AI reckons it needs. Money counts the day's debt repaid and repaid to it - the
/// repaid **taken off** the spending, as the original writes it. See <c>docs/mechanics/lua.md</c>, section 4.2.
/// </summary>
public static class CCountryGoods
{
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
		return Spending(country, goods);
	}

	/// <summary>
	/// The income side of the balance: home production - and convoyed in, but not for supplies or fuel - with
	/// what came back, was handed over by governments in exile, was repaid to it, was made and was traded for.
	/// </summary>
	public static long Income(CGameApi api, CCountry country, EGoods goods) => CDailyLedger.Income(country, goods);

	private static long Spending(CCountry country, EGoods goods) => CDailyLedger.Expense(country, goods);

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
		float spent = CLuaFloats.SingleThousandths((int)Spending(country, goods));
		return need < spent ? spent : need;
	}
}
