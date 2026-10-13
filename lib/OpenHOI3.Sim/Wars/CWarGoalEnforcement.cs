using System.Collections.Generic;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A war goal enforced on a capitulating country, the original's <c>CWarGoal::Enforce</c>: its type's peace options
/// in a fixed order, its threat, its <c>on_completion</c> and the message. See <c>docs/mechanics/war.md</c>, section 13.
/// </summary>
public static class CWarGoalEnforcement
{
	public const string Annex = "po_annex";
	public const string DemandRegion = "po_demand_region";
	public const string DemandProvinces = "po_demand_provinces";
	public const string MakePuppet = "po_puppet";

	/// <summary>The per cent below which the draw lets a country go into exile: 80.</summary>
	private const int ExileChance = 80;

	/// <summary>Base IC over which a member may host an exile whatever its file says.</summary>
	private const int HostIc = 100;

	/// <summary>The two countries the original sends into exile whatever the draw: Belgium and the Netherlands.</summary>
	private static readonly string[] AlwaysExiled = { "BEL", "HOL" };

	private const string Rebels = "REB";

	/// <summary>
	/// The goal enforced, in the original's order whatever the file's: annexation or a government in exile; a region
	/// demanded; provinces demanded; a puppet; then the type's threat spread from the actor's capital, its
	/// <c>on_completion</c> run - the recipient's scope, THIS and FROM the actor - and the message. Answers whether
	/// the actor made peace: not where the type handles peace itself, nor where the recipient went into exile.
	/// <c>po_install_government</c> and <c>po_change_government</c> do nothing, as in the original.
	/// </summary>
	public static bool Enforce(CGameState state, CWarGoal goal)
	{
		CCasusBelliType type = goal.Type;
		bool madePeace = !type.HandlesPeace;
		if (state.Country(goal.Actor) is not { } actor || state.Country(goal.Recipient) is not { } recipient)
		{
			return madePeace;
		}
		int threatOrigin = actor.Capital;
		if (type.PeaceOptions.Contains(Annex) && !AnnexOrExile(state, actor, recipient))
		{
			madePeace = false;
		}
		if (type.PeaceOptions.Contains(DemandRegion) && goal.Region is { } region)
		{
			foreach (int id in region.Provinces)
			{
				CProvince province = state.Province(id);
				if (province.Owner == recipient.Tag)
				{
					CProvinceHands.ChangeOwner(state, province, actor.Tag, consequences: true);
				}
			}
		}
		if (type.PeaceOptions.Contains(DemandProvinces))
		{
			var demanded = new List<CProvince>();
			foreach (CProvince province in state.Provinces)
			{
				if (province.Owner == recipient.Tag
					&& CConditions.All(state, type.AllowedProvince, CWarGoals.Scope(actor.Tag, recipient.Tag, province.Id), null))
				{
					demanded.Add(province);
				}
			}
			foreach (CProvince province in demanded)
			{
				CProvinceHands.ChangeOwner(state, province, actor.Tag, consequences: true);
			}
		}
		if (type.PeaceOptions.Contains(MakePuppet))
		{
			CPuppets.Make(state, actor, recipient);
		}
		if (type.Threat > CFixedPoint.Zero)
		{
			CThreat.SpreadOverMap(state, actor, type.Threat.Raw, 1000, state.Province(threatOrigin));
		}
		if (type.OnCompletion.Count > 0)
		{
			CEffects.Run(state, type.OnCompletion, CEventScope.Start(actor.Tag, fromCountry: actor.Tag).With(recipient.Tag, 0));
		}
		state.RaisePactMessage(new CPactMessage("THEYSURRENDERUS", "WESURRENDER", "THEYSURRENDER", actor.Tag, recipient.Tag)
		{
			Values = new[] { ("AGRESSOR", actor.Name), ("TARGET", recipient.Name), ("WARGOAL", type.Name) },
		});
		return madePeace;
	}

	/// <summary>
	/// <c>po_annex</c>: the draw of 80 in a hundred taken every time; where the recipient's faction has a host and the
	/// draw holds - or the recipient is a puppet whose overlord owns land, or Belgium or the Netherlands - the actor
	/// takes control of the recipient's land on its capital's continent or beside its own, and the recipient goes into
	/// exile, answering false; otherwise it is annexed, and where it had no host its faction's governments in exile
	/// with it. Answers whether the actor made peace.
	/// </summary>
	private static bool AnnexOrExile(CGameState state, CCountry actor, CCountry recipient)
	{
		CCountry? host = Host(state, recipient);
		bool drawn = state.Random.Next() % 100 < ExileChance;
		bool qualifies = drawn || (state.Country(recipient.Overlord) is { } overlord && CCountryLists.OwnedCount(state, overlord.Tag) > 0)
			|| System.Array.IndexOf(AlwaysExiled, recipient.Tag.Value) >= 0;
		if (host is not null && qualifies)
		{
			if (actor.Tag.Value != Rebels)
			{
				TakeExilesLand(state, actor, recipient);
			}
			CExile.Enter(state, recipient);
			return false;
		}
		string faction = recipient.Faction;
		CAnnexation.Annex(state, actor, recipient);
		if (host is null && faction.Length > 0)
		{
			foreach (CCountry member in CCountryLists.FactionMembers(state, faction))
			{
				if (member.InExile)
				{
					CAnnexation.Annex(state, actor, member);
				}
			}
		}
		return true;
	}

	/// <summary>
	/// Who would host an exile: the first member of its real faction but itself, not the rebels, major or with base IC
	/// over 100, owning a province and not in exile itself.
	/// </summary>
	public static CCountry? Host(CGameState state, CCountry country)
	{
		if (!COfferRules.RealFaction(country))
		{
			return null;
		}
		foreach (CCountry member in CCountryLists.FactionMembers(state, country.Faction))
		{
			if (member != country && member.Tag.Value != Rebels && (member.IsMajor || member.MaxIc > HostIc)
				&& CCountryLists.OwnedCount(state, member.Tag) > 0 && !member.InExile)
			{
				return member;
			}
		}
		return null;
	}

	/// <summary>
	/// The actor takes control - not ownership - of every province the exile holds on the continent of the actor's
	/// capital, or whose area borders ground the actor holds.
	/// </summary>
	private static void TakeExilesLand(CGameState state, CCountry actor, CCountry exile)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		string continent = state.Province(actor.Capital).Continent;
		var taken = new List<CProvince>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller != exile.Tag)
			{
				continue;
			}
			bool sameContinent = continent.Length > 0 && province.Continent == continent;
			bool beside = network.AreaOf(province.Id) is { } area && area.Neighbours.Exists(next => next.Controller == actor.Tag);
			if (sameContinent || beside)
			{
				taken.Add(province);
			}
		}
		foreach (CProvince province in taken)
		{
			CProvinceHands.SetController(state, province, actor.Tag);
		}
	}
}
