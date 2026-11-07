using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A province's two colours in the diplomatic map mode, by its owner's standing towards the player - its own, at war beside
/// it, a puppet bond, its faction, an alliance, at war with it, anyone else - the first that holds; and as stripes, the
/// player's cores green and its own land that is no core of its a dark grey. No intelligence dims it and the controller is
/// not asked. See <c>docs/mechanics/mapmodes.md</c>, section 8, and <c>reversing/findings/FINDINGS-mapmodescountries.md</c>,
/// section 2.
/// </summary>
public static class CDiplomaticMapColours
{
	public static readonly CColor Own = new(0f, 0.9f, 0f);
	public static readonly CColor WarAlly = new(0f, 0.8f, 0.9f);
	public static readonly CColor Puppet = new(0f, 0.7f, 0f);
	public static readonly CColor Faction = new(0f, 0.9f, 0.7f);
	public static readonly CColor Ally = new(0f, 0.7f, 0.7f);
	public static readonly CColor Enemy = new(0.7f, 0f, 0f);
	public static readonly CColor Neutral = new(0.5f, 0.5f, 0.5f);

	/// <summary>The stripes of the player's own land that is no core of its: 0x66 in every byte, drawn as 0.4 grey.</summary>
	public static readonly CColor NotCore = new(0.4f, 0.4f, 0.4f, 0.4f);

	public static (CColor Ground, CColor Stripes) Of(CGameState state, CProvince province, CCountryTag? player)
	{
		CCountry? owner = province.Owner is { } tag ? state.Country(tag) : null;
		CCountry? us = player is { } p ? state.Country(p) : null;
		CColor first = Ground(state, owner, us);
		CColor second = first;
		bool core = player is { } ours && province.Cores.Contains(ours);
		if (owner is not null && us is not null && owner == us && !core)
		{
			second = NotCore;
		}
		if (core)
		{
			second = Own;
		}
		return (first, second);
	}

	private static CColor Ground(CGameState state, CCountry? owner, CCountry? us)
	{
		if (owner is null || us is null)
		{
			return Neutral;
		}
		if (owner == us)
		{
			return Own;
		}
		if (CCountryLists.FightingTogether(state, owner.Tag, us.Tag))
		{
			return WarAlly;
		}
		if (owner.Overlord == us.Tag || us.Overlord == owner.Tag)
		{
			return Puppet;
		}
		if (owner.Faction.Length > 0 && owner.Faction == us.Faction)
		{
			return Faction;
		}
		if (CCountryLists.HasAlliance(state, owner.Tag, us.Tag))
		{
			return Ally;
		}
		return CCountryLists.AtWar(state, owner.Tag, us.Tag) ? Enemy : Neutral;
	}
}
