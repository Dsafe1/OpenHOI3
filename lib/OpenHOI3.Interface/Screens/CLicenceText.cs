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
	/// not better built by the buyer. Ours: the original compares the two versions' combat value, which is
	/// not read; here the seller's level of none of the type's technologies may be below the buyer's.
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

	private static bool SellerNoWorse(CCountry buyer, CCountry seller, CSubUnitType type)
	{
		foreach (Sim.Research.CTechnology technology in type.Technologies)
		{
			if (seller.TechnologyLevels.GetValueOrDefault(technology.Name) < buyer.TechnologyLevels.GetValueOrDefault(technology.Name))
			{
				return false;
			}
		}
		return true;
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
		buyer.Manpower.Raw >= ManpowerOfOne(buyer, type).Raw * serial * parallel;

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
