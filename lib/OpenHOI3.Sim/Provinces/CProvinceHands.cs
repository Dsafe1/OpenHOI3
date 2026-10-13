using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Movement;

namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// A province changing hands, by the original's two setters - who holds it, and who owns it - as far as what
/// they do is built. Every change of hands but a battle's goes through here: an effect, a peace, the day's return
/// of occupied land. See <c>docs/mechanics/war.md</c>, section 11.
/// </summary>
public static class CProvinceHands
{
	/// <summary>The defines' block the capture of buildings is read from.</summary>
	public const string Section = "military";

	/// <summary>The share of a fallen capital's money and resources its taker loots, in thousandths: a quarter.</summary>
	public const long CapitalLoot = 250;

	/// <summary>
	/// The original's <c>Province_SetController</c>, as far as it is built: nothing where the controller is the
	/// same, nor for land nobody owns. With <paramref name="consequences"/> and not <paramref name="peaceful"/>,
	/// each building an enemy takes may lose <c>COMBAT_PUSHBACK_DAMAGE</c> on a draw under
	/// <c>COMBAT_PUSHBACK_CHANCE_FOR_DAMAGE</c>, the province's production of money and the four resources is
	/// nought, and where it is the old controller's capital a quarter of the old controller's money and resources
	/// go to the new one's stockpile.
	/// </summary>
	public static void SetController(CGameState state, CProvince province, CCountryTag controller, bool consequences = false,
		bool peaceful = false)
	{
		if (province.Controller == controller || province.Owner is null)
		{
			return;
		}
		CCountryTag? old = province.Controller;
		if (consequences && !peaceful)
		{
			if (CMovementRules.IsEnemy(state, old, controller))
			{
				DamageBuildings(state, province);
			}
			for (EGoods goods = EGoods.Money; goods <= EGoods.RareMaterials; goods++)
			{
				province.CurrentProducing[goods] = CFixedPoint.Zero;
			}
			if (state.Country(old) is { } fallen && fallen.ActingCapital == province.Id && state.Country(controller) is { } taker)
			{
				Loot(state.Stockpile(fallen), state.Stockpile(taker));
			}
		}
		province.Controller = controller;
		// The old holder's government moves at once where this was its seat.
		if (consequences && state.Country(old) is { } former)
		{
			CActingCapital.Update(state, former);
		}
	}

	/// <summary>
	/// The original's <c>CMapProvince::ChangeOwner</c>: the new owner takes control with the same flags, then owns
	/// it - the rebels never do; the modifiers events gave it go, whether or not the owner changed; and the old
	/// owner, owning nothing now, leaves its faction. A new owner gets no core.
	/// </summary>
	public static void ChangeOwner(CGameState state, CProvince province, CCountryTag owner, bool consequences = false,
		bool peaceful = false)
	{
		CCountryTag? old = province.Owner;
		SetController(state, province, owner, consequences, peaceful);
		if (owner.Value != Rebels)
		{
			province.Owner = owner;
			province.Controller ??= owner;
		}
		if (state.Country(old) is { } former && RealFaction(former) && CCountryLists.OwnedCount(state, former.Tag) == 0)
		{
			CFactionMembership.Leave(former);
		}
		if (province.EventModifiers.Count > 0)
		{
			province.EventModifiers.Clear();
			CProvinceModifiers.Rebuild(state);
		}
	}

	private const string Rebels = "REB";

	private static bool RealFaction(CCountry country) => country.Faction.Length > 0;

	/// <summary>Each building there is any of, in turn, a draw each: under the chance, it loses the damage, to no less than nought.</summary>
	private static void DamageBuildings(CGameState state, CProvince province)
	{
		long chance = state.Defines[Section, "COMBAT_PUSHBACK_CHANCE_FOR_DAMAGE"].Raw / 1000;
		CFixedPoint damage = state.Defines[Section, "COMBAT_PUSHBACK_DAMAGE"];
		foreach (CBuilding building in state.Buildings)
		{
			if (province.BuildingMaxLevel(building) <= CFixedPoint.Zero)
			{
				continue;
			}
			if (state.Random.Next() % 100 < chance)
			{
				province.SetBuildingCurrentLevel(building, province.BuildingLevel(building) - damage);
			}
		}
	}

	/// <summary>The original's <c>CGoodsPool::TakeResources</c> at a quarter: out of one pool and into the other, money and the four resources.</summary>
	private static void Loot(CGoodsPool? from, CGoodsPool? into)
	{
		if (from is null || into is null)
		{
			return;
		}
		for (EGoods goods = EGoods.Money; goods <= EGoods.RareMaterials; goods++)
		{
			var taken = new CFixedPoint(from[goods].Raw * CapitalLoot / 1000);
			from[goods] -= taken;
			into[goods] += taken;
		}
	}
}
