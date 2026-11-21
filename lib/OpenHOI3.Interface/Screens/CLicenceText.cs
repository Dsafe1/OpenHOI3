using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Aid;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the licence dialog says and allows: which unit types it lists and in what order, a row's model
/// name, the runs and the money a unit, the description, the men an order takes, and when Accept may be
/// pressed. See <c>docs/mechanics/aid.md</c>, section 6.
/// </summary>
public static class CLicenceText
{
	public const int LeastRuns = 1;
	public const int MostRuns = 99;

	/// <summary>
	/// The types the buyer may licence from the seller, in the types' order: ones the buyer may use, the
	/// seller has unlocked and may still build, not capital ships, and - where the buyer can build one too -
	/// not better built by the buyer: each country's version of the type, at its own level of each of the type's
	/// technologies, is given its combat value, and only a buyer's strictly above the seller's leaves the type out. A
	/// type the buyer cannot build is listed with nothing compared.
	/// </summary>
	public static List<CSubUnitType> Types(CGameState state, CCountry buyer, CCountry seller)
	{
		var types = new List<CSubUnitType>();
		foreach (CSubUnitType type in state.SubUnitTypes.Values)
		{
			if (!CUnitAvailability.IsBuildableType(type) || !CUnitAvailability.IsUsableBy(buyer, type)
				|| CUnitAvailability.StillBuildable(state, seller, type) < 1 || type.Flag("capital"))
			{
				continue;
			}
			if (CUnitAvailability.MayUse(state, buyer, type) && !SellerNoWorse(buyer, seller, type))
			{
				continue;
			}
			types.Add(type);
		}
		return types;
	}

	private static bool SellerNoWorse(CCountry buyer, CCountry seller, CSubUnitType type) =>
		CombatValue(buyer, type) <= CombatValue(seller, type);

	/// <summary>
	/// The combat value of a country's version of a type, in thousandths: the type at the country's own level of each
	/// of its technologies, valued as the AI values a brigade, ship or wing (<see cref="Sim.Agents.CUnitPower.Value"/>).
	/// </summary>
	public static long CombatValue(CCountry country, CSubUnitType type)
	{
		var order = new CBrigadeOrder(type);
		CBuildQueue.SetModelToCountrys(country, order);
		return Sim.Agents.CUnitPower.Value(CBuildQueue.AsBuilt(order, isReserve: false, out long _));
	}

	/// <summary>
	/// A row's name: the seller's current model in brackets, "+*" after it where the seller's levels are not
	/// that model's exactly - "(§YPanzer IV+*§W)" - or the type's name where it has no model.
	/// </summary>
	public static string RowName(Func<string, string> localise, CGameState state, CCountry seller, CSubUnitType type)
	{
		string? model = ModelName(localise, state, seller, type);
		return model is null ? localise(type.Key) : "(§Y" + model + "§W)";
	}

	/// <summary>The seller's current model's name, with "+*" where its levels are not the model's exactly; null with no model.</summary>
	public static string? ModelName(Func<string, string> localise, CGameState state, CCountry seller, CSubUnitType type)
	{
		int model = CHistoricalModels.PickCurrent(state, seller, type);
		if (model < 0)
		{
			return null;
		}
		string? Find(string key) => localise(key) is var text && text != key ? text : null;
		string name = Tooltips.CBrigadeTooltips.ModelName(localise, Find, state, seller, type, model);
		return CHistoricalModels.FindExact(state, seller, type) == model ? name : name + "+*";
	}

	/// <summary>The money a unit at a place of the slider, nought to a thousand: that share of the buyer's money.</summary>
	public static CFixedPoint MoneyAt(CGameState state, CCountry buyer, long place)
	{
		long money = state.Stockpile(buyer)?[EGoods.Money].Raw ?? 0;
		return new CFixedPoint(Math.Max(0, money) * place / CFixedPoint.Scale);
	}

	/// <summary>The men one unit of the type takes the buyer, ordered as a licence orders it - a land type as reserves.</summary>
	public static CFixedPoint ManpowerOfOne(CCountry buyer, CSubUnitType type)
	{
		CConstruction quote = CBuildQueue.Quote(buyer, new[] { type }, type.Branch == EUnitBranch.Land);
		return quote.Manpower;
	}

	/// <summary>Whether the buyer has the men for every unit of the order.</summary>
	public static bool HasMen(CCountry buyer, CSubUnitType type, int serial, int parallel) =>
		buyer.Manpower.Raw >= MenNeeded(buyer, type, serial, parallel);

	/// <summary>The men the whole order takes, in thousandths: one unit's, cut to thousandths first, times the units.</summary>
	public static long MenNeeded(CCountry buyer, CSubUnitType type, int serial, int parallel) =>
		ManpowerOfOne(buyer, type).Raw * serial * parallel;

	/// <summary>
	/// Whether a row's box - and Accept - is greyed after a look at the order, given whether it was before: greyed where
	/// the order takes more men than the buyer has, freed where it takes fewer, and **left as it was where it takes
	/// exactly what he has** - the original's two strict tests.
	/// </summary>
	public static bool GreyedAfter(bool greyed, CCountry buyer, CSubUnitType type, int serial, int parallel)
	{
		long need = MenNeeded(buyer, type, serial, parallel);
		if (need > buyer.Manpower.Raw)
		{
			return true;
		}
		return need < buyer.Manpower.Raw ? false : greyed;
	}

	/// <summary><c>BUYLICENCENONE</c> with no type chosen; else <c>BUYLICENCENEWDESC</c> - runs, units a run, type, and the fee to one place.</summary>
	public static string Description(Func<string, string> localise, CLicenceTerms? terms)
	{
		if (terms is null)
		{
			return localise("BUYLICENCENONE");
		}
		return CTextTemplate.Fill(localise("BUYLICENCENEWDESC"),
			("RUNS", terms.Parallel.ToString(CultureInfo.InvariantCulture)),
			("NUM", terms.Serial.ToString(CultureInfo.InvariantCulture)),
			("TYPE", localise(terms.UnitType)),
			("PAY", CNumberText.Format(new CFixedPoint(terms.Fee), 1)));
	}

	/// <summary>
	/// The delayed half of a row's tooltip: the figures of the seller's version of the type - the type at the seller's
	/// own level of each of its technologies - a line a figure, with no plus signs.
	/// </summary>
	public static string RowFigures(Func<string, string> localise, CGameState state, CCountry seller, CSubUnitType type)
	{
		var order = new CBrigadeOrder(type);
		CBuildQueue.SetModelToCountrys(seller, order);
		CSubUnit version = CBuildQueue.AsBuilt(order, isReserve: false, out long _);
		return CTechnologyEffectsText.UnitFigures(localise, state, version.Stats, 1, plus: false).TrimEnd('\n', ' ');
	}

	/// <summary>
	/// A row's tooltip: where the buyer lacks the men, <c>BUYLICENCE_MPCOST</c> with one unit's men to one
	/// place, else the type's name.
	/// </summary>
	public static string RowTooltip(Func<string, string> localise, CCountry buyer, CSubUnitType type, bool hasMen)
	{
		return hasMen
			? localise(type.Key)
			: CTextTemplate.Fill(localise("BUYLICENCE_MPCOST"), ("COST", CNumberText.Format(ManpowerOfOne(buyer, type), 1)));
	}
}
