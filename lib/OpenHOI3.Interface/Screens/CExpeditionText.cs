using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>What the expeditionary force window lists and says. See <c>docs/mechanics/expeditions.md</c>, section 1.</summary>
public static class CExpeditionText
{
	/// <summary>A row's indent a level deeper.</summary>
	public const float Indent = 10f;

	/// <summary>Whether the country holds a unit lent by the other with no recall under way, the test for a withdrawal's row.</summary>
	public static bool HoldsLentBy(CCountry holder, CCountry lender)
	{
		return holder.AllUnits().Any(unit => unit.Lender == lender.Tag && unit.RecallHour is null);
	}

	/// <summary>The window's rows, the unit and its depth: land as a tree the given set opens, air and fleets flat.</summary>
	public static List<(CUnit Unit, int Depth)> Rows(CGameState state, CCountry player, COffer offer, EExpeditionKind kind, ISet<int> opened)
	{
		var rows = new List<(CUnit, int)>();
		CCountryTag? lender = offer.Value ? null : offer.Recall ? offer.Actor : offer.Recipient;
		CCountry? walked = !offer.Value && offer.Recall ? state.Country(offer.Recipient) : player;
		foreach (CUnit unit in walked?.AllUnits() ?? Enumerable.Empty<CUnit>())
		{
			Add(rows, unit, 0, lender, kind, opened);
		}
		return rows;
	}

	private static void Add(List<(CUnit, int)> rows, CUnit unit, int depth, CCountryTag? lender, EExpeditionKind kind, ISet<int> opened)
	{
		if (lender is { } owner)
		{
			if (unit.Lender != owner || unit.RecallHour is not null || (depth == 0 && unit.Parent?.Lender == owner))
			{
				return;
			}
		}
		else if (unit.Lender is not null)
		{
			return;
		}
		bool land = CMovementRules.IsLandUnit(unit);
		if (kind == EExpeditionKind.Army ? !land : kind == EExpeditionKind.Air ? unit.Kind != EUnitKind.Air : unit.Kind != EUnitKind.Navy)
		{
			return;
		}
		if (land && depth == 0 && lender is null && unit.Parent is not null)
		{
			return;
		}
		rows.Add((unit, depth));
		if (land && opened.Contains(unit.Id))
		{
			foreach (CUnit child in unit.Children)
			{
				Add(rows, child, depth + 1, lender, kind, opened);
			}
		}
	}

	public static int CountUnder(CUnit unit) => 1 + unit.Children.Sum(CountUnder);

	/// <summary>The window's description: who it goes to, or when it comes back, and the unit ticked by its kind's words.</summary>
	public static string Description(Func<string, string> localise, CGameState state, COffer offer, CUnit? chosen, Func<long, string> date)
	{
		CCountry? other = state.Country(offer.Value || !offer.Recall ? offer.Recipient : offer.Actor);
		string head;
		if (offer.Value)
		{
			head = CTextTemplate.Fill(localise("SENDEXPEDNEWDESC"), ("RECIPIENT", other?.Name ?? ""));
		}
		else
		{
			string define = offer.Recall ? CExpeditions.ReclaimDefine : CExpeditions.ReturnDefine;
			long hour = state.TotalHours + COfferRules.Define(state, define).Raw / CFixedPoint.Scale * CGameState.HoursPerDay;
			head = CTextTemplate.Fill(localise("RETRIEVEEXPEDNEWDESC"), ("RECIPIENT", other?.Name ?? ""), ("DATE", date(hour)));
		}
		if (chosen is null)
		{
			return head;
		}
		string key = chosen.Kind == EUnitKind.Navy ? "EXPEDNEWDESC_NAVY" : chosen.Kind == EUnitKind.Air ? "EXPEDNEWDESC_AIR" : "EXPEDNEWDESC_ARMY";
		return head + " " + CTextTemplate.Fill(localise(key), ("UNIT", chosen.Name), ("NUM", CountUnder(chosen).ToString(CultureInfo.InvariantCulture)),
			("PROV", state.Province(chosen.Location).Name));
	}
}
