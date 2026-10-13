using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// War goals: whether one can still be chosen, whether it is valid for a war, and adding it to one. See
/// <c>docs/mechanics/war.md</c>, section 3.
/// </summary>
public static class CWarGoals
{
	/// <summary>The define of the months before another goal may be added against the same country.</summary>
	public const string Cooldown = "WARGOAL_ADD_COOLDOWN";

	/// <summary>
	/// The original's <c>CWarGoal_IsAvailable</c>: whether the goal can still be chosen - its type real, its
	/// third country set where the type asks for one, and some region, province or nothing it needs passing,
	/// and the war, if any, holding no such goal.
	/// </summary>
	public static bool IsAvailable(CGameState state, CWarGoal goal, CWar? war)
	{
		CCasusBelliType type = goal.Type;
		if (!type.IsReal || (HasConditions(type.AllowedCountries) && goal.Country is null))
		{
			return false;
		}
		if (HasConditions(type.AllowedRegions) && goal.Region is null)
		{
			foreach (CRegion region in state.Regions)
			{
				if (RegionPasses(state, type, region, goal.Actor, null) && (war is null || !Holds(war, goal with { Region = region })))
				{
					return true;
				}
			}
			return false;
		}
		if (HasConditions(type.AllowedProvince))
		{
			foreach (CProvince province in state.Provinces)
			{
				if (province.Owner == goal.Recipient
					&& CConditions.All(state, type.AllowedProvince, Scope(goal.Actor, goal.Recipient, province.Id), null))
				{
					return war is null || !Holds(war, goal);
				}
			}
			return false;
		}
		return war is null || !Holds(war, goal);
	}

	/// <summary>
	/// The original's <c>CWarGoal::IsValidForWar</c>: the type real, the recipient owning a province, the third
	/// country passing <c>allowed_countries</c> where it has conditions, a region set and one of its provinces
	/// passing <c>allowed_regions</c> where that has conditions, and the war, if any, holding no such goal.
	/// </summary>
	public static bool IsValidForWar(CGameState state, CWarGoal goal, CWar? war)
	{
		CCasusBelliType type = goal.Type;
		if (!type.IsReal || CCountryFacts.Of(state).OwnedCount(goal.Recipient) == 0)
		{
			return false;
		}
		if (HasConditions(type.AllowedCountries) && !CountryPasses(state, type, goal.Actor, goal.Recipient, goal.Country))
		{
			return false;
		}
		if (HasConditions(type.AllowedRegions)
			&& (goal.Region is not { } region || !RegionPasses(state, type, region, goal.Actor, goal.Recipient)))
		{
			return false;
		}
		return war is null || !Holds(war, goal);
	}

	/// <summary>
	/// The original's <c>CWarGoal::AddToWar</c>: where the type is real and its <c>is_valid</c> holds for the
	/// recipient with THIS the actor, the goal is filed under the actor, and the actor may add none against
	/// the recipient for <c>WARGOAL_ADD_COOLDOWN</c> months. Answers whether it was added.
	/// </summary>
	public static bool AddToWar(CGameState state, CWarGoal goal, CWar war)
	{
		if (!goal.Type.IsReal || !CCasusBellis.IsValid(state, goal.Actor, goal.Recipient, goal.Type))
		{
			return false;
		}
		if (!war.Goals.TryGetValue(goal.Actor, out List<CWarGoal>? goals))
		{
			war.Goals[goal.Actor] = goals = new List<CWarGoal>();
		}
		goals.Add(goal);
		if (state.Country(goal.Actor) is { } actor && state.Country(goal.Recipient) is { } recipient)
		{
			int months = (int)(state.Defines["diplomacy", Cooldown].Raw / 1000);
			CGameState.Status(actor, recipient).NextWarGoal = Agreements.COfferRules.AddMonths(state.TotalHours, months);
		}
		return true;
	}

	/// <summary>
	/// What a declaration and an added goal do on adding it (<c>war.md</c>, section 3): added, then the type's
	/// <c>on_add</c> in the actor's scope, then one casus belli of its type the actor holds on the recipient
	/// spent.
	/// </summary>
	public static void AddAndSpend(CGameState state, CWarGoal goal, CWar war)
	{
		AddToWar(state, goal, war);
		if (goal.Type.OnAdd.Count > 0)
		{
			CEffects.Run(state, goal.Type.OnAdd, CEventScope.Start(goal.Actor));
		}
		if (state.Country(goal.Actor) is { } actor && state.Country(goal.Recipient) is { } recipient)
		{
			CCasusBellis.Spend(actor, recipient, goal.Type);
		}
	}

	/// <summary>Whether the actor may add a goal against the recipient now: strictly after the cooldown's hour.</summary>
	public static bool CooledDown(CGameState state, CCountry actor, CCountry recipient)
	{
		return CGameState.Status(actor, recipient).NextWarGoal < state.TotalHours;
	}

	/// <summary>Whether the war holds the same goal - type, actor, recipient, third country and region - under its actor.</summary>
	public static bool Holds(CWar war, CWarGoal goal)
	{
		foreach (CWarGoal held in war.GoalsOf(goal.Actor))
		{
			if (held == goal)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Whether some province of the region passes <c>allowed_regions</c>, THIS the actor and FROM the recipient where given.</summary>
	public static bool RegionPasses(CGameState state, CCasusBelliType type, CRegion region, CCountryTag actor, CCountryTag? from)
	{
		foreach (int id in region.Provinces)
		{
			if (CConditions.All(state, type.AllowedRegions, Scope(actor, from, id), null))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Whether the candidate passes <c>allowed_countries</c>: asked in the recipient's scope with THIS the
	/// actor and FROM the candidate.
	/// </summary>
	public static bool CountryPasses(CGameState state, CCasusBelliType type, CCountryTag actor, CCountryTag recipient,
		CCountryTag? candidate)
	{
		CEventScope scope = CEventScope.Start(actor, fromCountry: candidate).With(recipient, 0);
		return CConditions.All(state, type.AllowedCountries, scope, null);
	}

	/// <summary>
	/// The goals the declare-war dialog lists against the recipient (<c>war.md</c>, section 10): every real
	/// type, in the file's order, whose <c>is_valid</c> holds with THIS the player, that the player holds or
	/// that is <c>always</c>, whose goal is available, and - where <c>allowed_regions</c> has conditions - for
	/// which some province of a region kept for peace passes them with THIS the player and FROM the recipient.
	/// </summary>
	public static List<CCasusBelliType> Offered(CGameState state, CCountry player, CCountry recipient)
	{
		CWar? war = CWarSides.Between(state, player.Tag, recipient.Tag);
		var offered = new List<CCasusBelliType>();
		foreach (CCasusBelliType type in state.CasusBelliTypes)
		{
			if (!type.IsReal || !CCasusBellis.IsValid(state, player.Tag, recipient.Tag, type))
			{
				continue;
			}
			bool held = CGameState.Status(player, recipient).CasusBelli.Exists(c => c.Type == type.Index);
			if ((!held && !type.Always) || !IsAvailable(state, new CWarGoal(type, player.Tag, recipient.Tag), war))
			{
				continue;
			}
			if (HasConditions(type.AllowedRegions) && !PeaceRegions(state, type, player.Tag, recipient.Tag).GetEnumerator().MoveNext())
			{
				continue;
			}
			offered.Add(type);
		}
		return offered;
	}

	/// <summary>
	/// For trials: the goal the dialog would make with its first choices - the first type it offers that asks
	/// for no country or region, else its first, with the first country and region its lists hold. None where it
	/// offers nothing.
	/// </summary>
	public static CWarGoal? FirstOffered(CGameState state, CCountry actor, CCountry recipient)
	{
		List<CCasusBelliType> offered = Offered(state, actor, recipient);
		CCasusBelliType? type = offered.Find(t => !HasConditions(t.AllowedCountries) && !HasConditions(t.AllowedRegions))
			?? (offered.Count > 0 ? offered[0] : null);
		if (type is null)
		{
			return null;
		}
		var goal = new CWarGoal(type, actor.Tag, recipient.Tag);
		if (HasConditions(type.AllowedCountries))
		{
			List<CCountry> countries = CountriesFor(state, type, actor, recipient);
			goal = goal with { Country = countries.Count > 0 ? countries[0].Tag : null };
		}
		if (HasConditions(type.AllowedRegions))
		{
			List<CRegion> regions = RegionsFor(state, goal);
			goal = goal with { Region = regions.Count > 0 ? regions[0] : null };
		}
		return goal;
	}

	/// <summary>The countries the dialog lists for a type that asks for one: every one passing <c>allowed_countries</c>.</summary>
	public static List<CCountry> CountriesFor(CGameState state, CCasusBelliType type, CCountry player, CCountry recipient)
	{
		var countries = new List<CCountry>();
		foreach (CCountry candidate in state.Countries)
		{
			if (CountryPasses(state, type, player.Tag, recipient.Tag, candidate.Tag))
			{
				countries.Add(candidate);
			}
		}
		return countries;
	}

	/// <summary>
	/// The regions the dialog lists for a goal that asks for one: those kept for peace, in the map's order,
	/// with a province passing <c>allowed_regions</c> with THIS the player and FROM the recipient, whose goal
	/// with the region is available.
	/// </summary>
	public static List<CRegion> RegionsFor(CGameState state, CWarGoal goal)
	{
		CWar? war = CWarSides.Between(state, goal.Actor, goal.Recipient);
		var regions = new List<CRegion>();
		foreach (CRegion region in PeaceRegions(state, goal.Type, goal.Actor, goal.Recipient))
		{
			if (IsAvailable(state, goal with { Region = region }, war))
			{
				regions.Add(region);
			}
		}
		return regions;
	}

	private static IEnumerable<CRegion> PeaceRegions(CGameState state, CCasusBelliType type, CCountryTag player, CCountryTag recipient)
	{
		foreach (CRegion region in state.Regions)
		{
			if (region.UseForPeace && RegionPasses(state, type, region, player, recipient))
			{
				yield return region;
			}
		}
	}

	/// <summary>Whether a block has a condition in it.</summary>
	public static bool HasConditions(CTrigger block) => block.Children.Count > 0;

	/// <summary>A province's scope with THIS the actor and FROM the country given.</summary>
	internal static CEventScope Scope(CCountryTag actor, CCountryTag? from, int province)
	{
		return CEventScope.Start(actor, fromCountry: from).With(null, province);
	}
}
