using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Movement;

namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// A province changing hands, by the original's two setters - who holds it, and who owns it - as far as what
/// they do is built. Every change of hands goes through here: a battle, an effect, a peace, the day's return
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
	/// same, nor for land nobody owns. The new controller's occupation policy towards the old is set where it has none.
	/// With <paramref name="consequences"/> and not <paramref name="peaceful"/>,
	/// each building an enemy takes may lose <c>COMBAT_PUSHBACK_DAMAGE</c> on a draw under
	/// <c>COMBAT_PUSHBACK_CHANCE_FOR_DAMAGE</c>, the province's production of money and the four resources is
	/// nought, and where it is the old controller's capital a quarter of the old controller's money and resources
	/// go to the new one's stockpile. With <paramref name="consequences"/>, peaceful or not, its radar moves from the old
	/// controller's list of radar provinces to the new one's. Whatever the flags, its port's blockade is worked out again.
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
		// With consequences, peaceful or not, its radar goes from the old holder's list to the new one's (intelligence.md,
		// section 4).
		if (consequences)
		{
			Intelligence.CRadarProvinces.ControllerChanged(state, province, old, controller);
		}
		// The new controller's policy towards the old, where it has none yet (occupation.md, section 2).
		Occupation.COccupationPolicies.Capture(state, controller, old);
		province.Controller = controller;
		CProvinceHistory.Record(state, province, CProvinceHistory.Controller, controller);
		// The holders' theatres, and every neighbour's owner's, are made again in their next hour (theatres.md, section 1).
		Theatres.CTheatres.ControllerChanged(state, province, old, controller);
		// A faction drops land the rebels lose, and takes land they win (rebels.md, section 10).
		Revolts.CRebelSiege.ControllerChanged(state, province, old, controller, consequences);
		// Its underground goes where the new holder owns it or is no enemy of its owner (undergrounds.md, section 7).
		Revolts.CUndergrounds.ControllerChanged(state, province, controller);
		// Whether its port is blockaded, worked out now and at no other time, and its owner's share (modifiers.md, section 3).
		CBlockade.UpdateProvince(state, province);
		// The old holder's government moves at once where this was its seat.
		if (consequences && state.Country(old) is { } former)
		{
			CActingCapital.Update(state, former);
		}
		// The old holder's agents are told at once, and one beside a small area answers (moving.md, section 7.7). The AI's
		// decision, made inside the game's own pass: recorded and played back as a step (docs/replay.md, section 3).
		if (state.Replay is { } replay)
		{
			replay.RunCalledStep(state, "CProvinceLost.ControllerChanged #" + province.Id,
				s => Agents.CProvinceLost.ControllerChanged(s, province, old));
		}
		else
		{
			Agents.CProvinceLost.ControllerChanged(state, province, old);
		}
	}

	/// <summary>
	/// The original's <c>CMapProvince::ChangeOwner</c>: the new owner takes control with the same flags, then owns
	/// it - the rebels never do; its nationalism is set where the old owner was a country and the modifiers events
	/// gave it go, both whether or not the owner changed; and the old owner, owning nothing now, leaves its faction. A
	/// new owner gets no core.
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
			// After its controller's, at the same hour (a save of 1938: add_core, controller and owner at 17:00).
			if (old != owner)
			{
				CProvinceHistory.Record(state, province, CProvinceHistory.Owner, owner);
			}
		}
		if (state.Country(old) is not null)
		{
			CNationalism.Set(state, province);
		}
		if (state.Country(old) is { } former && RealFaction(former) && CCountryLists.OwnedCount(state, former.Tag) == 0)
		{
			CFactionMembership.Leave(state, former);
		}
		// The modifiers events gave it go, and its modifier is made again (modifiers.md, section 3).
		province.EventModifiers.Clear();
		CProvinceModifiers.RebuildOne(state, province);
	}

	private const string Rebels = "REB";

	private static bool RealFaction(CCountry country) => country.Faction.Length > 0;

	/// <summary>
	/// The original's <c>CCombat::DamageProvinceBuildings</c>, which the setter does too: each building the province
	/// was built with any of, in turn, a draw each - under <c>COMBAT_PUSHBACK_CHANCE_FOR_DAMAGE</c> per cent it loses
	/// <c>COMBAT_PUSHBACK_DAMAGE</c> of what works, to no less than nought. A battle runs it each time a defending front
	/// unit falls back (<c>docs/mechanics/landcombat.md</c>, section 7).
	/// </summary>
	public static void DamageBuildings(CGameState state, CProvince province)
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
