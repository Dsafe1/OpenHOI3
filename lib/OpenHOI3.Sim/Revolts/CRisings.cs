using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// A rising: rebels of a type put down in a province, in the faction they join or a new one. See
/// <c>docs/mechanics/rebels.md</c>, sections 5 and 6.
/// </summary>
public static class CRisings
{
	private const long GovernmentBase = 1000;
	private const long GovernmentIdeology = 31000;
	private const long GovernmentPresent = 50000;

	/// <summary>
	/// The original's <c>CreateRebelBrigadesInProvince</c>: nothing in a province in no real area; at least one
	/// brigade; the faction - the underground's own for an underground's rising - or a new one; and the province's
	/// revolt risk set to nought before anything is made, so also where nothing is.
	/// </summary>
	public static void Rise(CGameState state, CProvince province, CRebelType? type, int count, bool forActor = false,
		CCountryTag? actor = null)
	{
		if (CRebelFactions.AreaOf(state, province.Id) is null)
		{
			return;
		}
		if (count < 1)
		{
			count = 1;
		}
		CRebelFaction? faction = forActor ? Underground(state, type, actor) : FactionFor(state, province, type);
		province.RevoltRisk = CFixedPoint.Zero;
		if (faction is not null)
		{
			if (province.Controller is { } holder && !CRebelFactions.IsRebels(holder) && !faction.Targets.Contains(holder))
			{
				faction.Targets.Add(holder);
			}
			CRebelArmies.Make(state, faction, province, count, forActor, actor);
		}
		else
		{
			Make(state, type, province, count, forActor, actor);
		}
	}

	/// <summary>
	/// The original's <c>FindRebelFactionForProvince</c>: of the factions of the type, an independence type's first
	/// freeing the country the province would free; a nation-wide type's last whose country owns the province; any
	/// other type's none.
	/// </summary>
	public static CRebelFaction? FactionFor(CGameState state, CProvince province, CRebelType? type)
	{
		if (type is null)
		{
			return null;
		}
		CRebelFaction? found = null;
		foreach (CRebelFaction faction in state.RebelFactions)
		{
			if (faction.Type != type)
			{
				continue;
			}
			if (type.Independence)
			{
				if (faction.Independence == CRebelChoice.IndependenceOf(state, type, province))
				{
					return faction;
				}
			}
			else if (!type.AnyArea && faction.Country == province.Owner)
			{
				found = faction;
			}
		}
		return found;
	}

	/// <summary>
	/// The original's <c>CreateRebelFaction</c> and the faction's constructor: the type chosen again, and not tested,
	/// where it would free nobody; then its country, its one target - the province's holder - the country it would free,
	/// its government and its name; and its first army.
	/// </summary>
	public static CRebelFaction? Make(CGameState state, CRebelType? type, CProvince province, int count,
		bool forActor = false, CCountryTag? actor = null)
	{
		if (type is { Independence: true } && !forActor && CRebelChoice.IndependenceOf(state, type, province) is null)
		{
			type = CRebelChoice.TypeFor(state, province);
		}
		if (type is null)
		{
			return null;
		}
		var faction = new CRebelFaction(++state.LastRebelId, type, province.Id, forActor ? actor : province.Owner);
		faction.Government = state.Country(faction.Country) is { } country ? state.GovernmentOf(country) : null;
		if (province.Controller is { } holder)
		{
			faction.Targets.Add(holder);
		}
		faction.Independence = CRebelChoice.IndependenceOf(state, type, province);
		faction.Government = ChooseGovernment(state, faction, province);
		faction.Name = CRebelFactions.Render(state, faction, type.NameText);
		state.RebelFactions.Add(faction);
		CRebelArmies.Make(state, faction, province, count, forActor, actor);
		return faction;
	}

	/// <summary>
	/// The original's <c>RebelFaction_ChooseGovernment</c>: of the governments valid for a new country, in their file's
	/// order, 1000 each, 31000 for one whose ideologies hold the type's, 50000 more for the faction's country's own; the
	/// first strictly highest. Where none, the province's owner's.
	/// </summary>
	public static CGovernment? ChooseGovernment(CGameState state, CRebelFaction faction, CProvince province)
	{
		CGovernment? chosen = faction.Government;
		CGovernment? present = state.Country(faction.Country) is { } country ? state.GovernmentOf(country) : null;
		long best = 0;
		foreach (CGovernment government in state.Governments.Values)
		{
			if (!government.ValidForNewCountry)
			{
				continue;
			}
			long score = faction.Type.Ideology is { } ideology && government.Accepted.Contains(ideology)
				? GovernmentIdeology : GovernmentBase;
			if (government == present)
			{
				score += GovernmentPresent;
			}
			if (score > best)
			{
				best = score;
				chosen = government;
			}
		}
		if (chosen is null && state.Country(province.Owner) is { } owner)
		{
			chosen = state.GovernmentOf(owner);
		}
		return chosen;
	}

	/// <summary>
	/// <c>&lt;rebel type&gt; = N</c>: a rising of that type, of the scope's province's manpower times N brigades. It names
	/// its type and tests nothing first; in a country's scope it is the province with no number's (section 8).
	/// </summary>
	public static void FromEffect(CGameState state, Script.CEffect effect, Script.CEventScope scope)
	{
		CProvince province = state.Province(scope.Province);
		CRebelType? type = null;
		foreach (CRebelType each in state.RebelTypes)
		{
			if (each.Name == effect.Key)
			{
				type = each;
				break;
			}
		}
		Rise(state, province, type, Brigades(province, effect.Argument.Amount));
	}

	/// <summary>The province's manpower times N, truncated: the brigades both effects ask for.</summary>
	public static int Brigades(CProvince province, CFixedPoint times)
	{
		return (int)(province.Manpower.Raw * times.Raw / 1000 / 1000);
	}

	/// <summary>
	/// The original's <c>SpawnFullRevoltInProvince</c>, as a new game is entered: each province whose history gives it
	/// a rebel type rises, the size times its manpower and at least the size, in the faction it would join or a new
	/// one; and where the rebels then hold it, the faction takes it. The revolt risk a new game gives is
	/// <see cref="CRevoltRisk.SetAtNewGame"/>'s.
	/// </summary>
	public static void AtNewGame(CGameState state)
	{
		foreach (CProvince province in state.Provinces)
		{
			if (province.RevoltType is not { } name)
			{
				continue;
			}
			CRebelType? type = null;
			foreach (CRebelType each in state.RebelTypes)
			{
				if (each.Name == name)
				{
					type = each;
					break;
				}
			}
			if (type is null)
			{
				continue;
			}
			int brigades = province.RevoltSize * (int)Math.Max(1, province.Manpower.Raw / 1000);
			CRebelFaction? faction = FactionFor(state, province, type);
			if (faction is not null)
			{
				CRebelArmies.Make(state, faction, province, brigades, forActor: false, actor: null);
			}
			else
			{
				faction = Make(state, type, province, brigades);
			}
			if (faction is not null && CRebelFactions.IsRebels(province.Controller))
			{
				CRebelSiege.Take(state, faction, province);
			}
		}
	}

	/// <summary>The first faction of the type whose country is the underground's.</summary>
	private static CRebelFaction? Underground(CGameState state, CRebelType? type, CCountryTag? actor)
	{
		foreach (CRebelFaction faction in state.RebelFactions)
		{
			if (faction.Type == type && faction.Country == actor)
			{
				return faction;
			}
		}
		return null;
	}
}
