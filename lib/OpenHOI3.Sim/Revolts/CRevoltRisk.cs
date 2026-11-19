using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// A province's revolt risk: the accumulator the day moves while its holder is at war with its owner, and the figure
/// everything else reads, made of it, the holder's and the province's modifiers and the suppression. See
/// <c>docs/mechanics/revolt.md</c>, sections 3 and 4.
/// </summary>
public static class CRevoltRisk
{
	public const string PartisanSupport = "local_partisan_support";
	public const string LocalRevoltRisk = "local_revolt_risk";
	public const string GlobalRevoltRisk = "global_revolt_risk";
	public const string MinimumRevoltRisk = "minimum_revolt_risk";

	/// <summary>The day's step, up or down, in thousandths of a point: the executable's own figure.</summary>
	public const long Step = 100;

	/// <summary>The most the suppression takes off a day's step, in thousandths of a point: the executable's own.</summary>
	public const long MostCut = 200;

	private const string Rebels = "REB";

	/// <summary>
	/// The day's step, in the province's own day after its suppression is worked out: where its holder is at war with
	/// its owner, or either is the rebels, a tenth of a point up towards its <c>local_partisan_support</c> - down
	/// where it is above it - less a tenth of its suppression, at most 0.2, and held to the support; and anywhere,
	/// never below nought.
	/// </summary>
	public static void StepDaily(CGameState state, CProvince province)
	{
		long risk = province.RevoltRisk.Raw;
		if (province.Owner is { } owner && state.Country(province.Controller) is { } holder && owner != holder.Tag
			&& (CCountryLists.AtWar(state, holder.Tag, owner) || owner.Value == Rebels || holder.Tag.Value == Rebels))
		{
			long ceiling = province.Modifier[PartisanSupport].Raw;
			long step = risk > ceiling ? -Step : Step;
			long cut = Math.Min(province.Suppression.Raw * Step / CFixedPoint.Scale, MostCut);
			risk += step - cut;
			if (risk > ceiling)
			{
				risk = ceiling;
			}
		}
		province.RevoltRisk = new CFixedPoint(Math.Max(risk, 0));
	}

	/// <summary>
	/// What everything reads, in thousandths of a point, and it may be below nought: the accumulator where the holder is
	/// an enemy of the owner; the <c>global_revolt_risk</c> of the holder - of the owner where the rebels hold it; the
	/// province's <c>local_revolt_risk</c>; less its suppression. The original's <c>pure_revolt_risk</c>.
	/// </summary>
	/// <param name="countriesMade">
	/// False for the province modifiers a new game is entered with, made before any country's modifier is: the holder's
	/// <c>global_revolt_risk</c> is not in them (<c>revolt.md</c>, section 3; <c>manpower.md</c>, section 4).
	/// </param>
	public static long Effective(CGameState state, CProvince province, bool countriesMade = true)
	{
		long risk = 0;
		if (state.Country(province.Controller) is { } holder && province.Owner is { } owner
			&& CCountryLists.IsEnemy(state, holder, owner))
		{
			risk = province.RevoltRisk.Raw;
		}
		if (countriesMade && province.Controller is { } controller)
		{
			CCountryTag whose = controller.Value == Rebels && province.Owner is { } owned ? owned : controller;
			if (state.Country(whose) is { } country)
			{
				risk += country.Modifier[GlobalRevoltRisk].Raw;
			}
		}
		risk += province.Modifier[LocalRevoltRisk].Raw;
		return risk - province.Suppression.Raw;
	}

	/// <summary>The figure shown and asked by <c>revolt_risk</c>: the effective one, never below the province's <c>minimum_revolt_risk</c> nor nought.</summary>
	public static long Shown(CGameState state, CProvince province, bool countriesMade = true)
	{
		return Math.Max(Effective(state, province, countriesMade), Math.Max(province.Modifier[MinimumRevoltRisk].Raw, 0));
	}

	/// <summary>The effect <c>revolt_risk = N</c>: N added to the accumulator, never below nought. A country's scope reaches province 0.</summary>
	public static void Add(CProvince province, CFixedPoint amount)
	{
		province.RevoltRisk = new CFixedPoint(Math.Max(province.RevoltRisk.Raw + amount.Raw, 0));
	}

	/// <summary>A new game, as it is entered: every province with no revolt risk is given its <c>local_partisan_support</c>.</summary>
	public static void SetAtNewGame(CGameState state)
	{
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id > 0 && province.RevoltRisk == CFixedPoint.Zero)
			{
				province.RevoltRisk = province.Modifier[PartisanSupport];
			}
		}
	}
}
