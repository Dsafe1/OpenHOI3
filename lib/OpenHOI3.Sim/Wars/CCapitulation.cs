using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A country's capitulation, the original's <c>CCountry_Capitulate</c>: every enemy's war goals on it enforced, in
/// <c>peace_order</c>, annexing goals last; and the country leaving the war, or staying in it. See
/// <c>docs/mechanics/war.md</c>, section 13.
/// </summary>
public static class CCapitulation
{
	/// <summary>The type of the goal made up where no goal can be enforced.</summary>
	public const string Conquer = "conquer";

	/// <summary>The type of the goal a capitulating country's vassals are puppeted to the victor by.</summary>
	public const string Puppet = "puppet";

	/// <summary>
	/// In order: the war the victor and the country share; the goals the other side holds, each of its countries'
	/// in turn, sorted by the types' ranks with the victor's first among equals - by the original runtime's own
	/// sort; those against the country that do not annex enforced, then those that do, each where it passes the
	/// gate, its actor making peace or fighting on by what it answers; with none passing, a <c>conquer</c> goal of
	/// the victor's enforced unasked. Where nobody fights on, the country leaves the war and each of its vassals in
	/// it is freed where a goal names it, else made the victor's puppet; otherwise it stays and every goal by or
	/// against it, and its vassals, is deleted. Last, what it holds of countries no longer at war with it goes back.
	/// </summary>
	public static void Capitulate(CGameState state, CCountry country, CCountry victor)
	{
		if (CWarSides.Between(state, victor.Tag, country.Tag) is not { } war)
		{
			return;
		}
		bool defends = war.Defenders.Contains(country.Tag);
		var goals = new List<CWarGoal>();
		foreach (CCountryTag holder in defends ? war.Attackers : war.Defenders)
		{
			goals.AddRange(war.GoalsOf(holder));
		}
		CStdSort.Sort(goals, (a, b) => a.Type.Rank < b.Type.Rank
			|| (a.Type.Rank == b.Type.Rank && a.Actor == victor.Tag && b.Actor != victor.Tag));

		var peace = new List<CCountryTag>();
		var fightOn = new List<CCountryTag>();
		int passed = 0;
		foreach (bool annexing in new[] { false, true })
		{
			foreach (CWarGoal goal in goals)
			{
				if (goal.Recipient != country.Tag || goal.Type.Annexes != annexing || !CWarGoals.IsValidForWar(state, goal, null))
				{
					continue;
				}
				passed++;
				Count(CWarGoalEnforcement.Enforce(state, goal), goal.Actor, peace, fightOn);
			}
		}
		if (passed == 0)
		{
			CCasusBelliType type = Type(state, Conquer) ?? CCasusBelliType.Null;
			Count(CWarGoalEnforcement.Enforce(state, new CWarGoal(type, victor.Tag, country.Tag)), victor.Tag, peace, fightOn);
		}

		List<CCountry> vassals = Vassals(state, country);
		if (fightOn.Count == 0)
		{
			CWarSides.Leave(state, war, country.Tag);
			foreach (CCountry vassal in vassals)
			{
				if (!war.Involves(vassal.Tag))
				{
					continue;
				}
				if (Named(war, vassal.Tag))
				{
					CPuppets.Release(state, vassal);
					continue;
				}
				if (Type(state, Puppet) is { IsReal: true } puppet)
				{
					CWarGoalEnforcement.Enforce(state, new CWarGoal(puppet, victor.Tag, vassal.Tag));
				}
				CWarSides.Leave(state, war, vassal.Tag);
			}
		}
		else
		{
			CWarSides.RemoveGoalsOf(war, country.Tag);
			foreach (CCountry vassal in vassals)
			{
				CWarSides.RemoveGoalsOf(war, vassal.Tag);
			}
		}

		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller == country.Tag && state.Country(province.Owner) is { } owner && owner != country
				&& (!CCountryLists.AtWar(state, owner.Tag, country.Tag) || (owner.InExile && CWarSides.Between(state, owner.Tag, country.Tag) == war)))
			{
				CProvinceHands.SetController(state, province, owner.Tag, consequences: true);
			}
		}
	}

	/// <summary>An actor that made peace, unless it fights on already; one that fights on is taken off the peacemakers.</summary>
	private static void Count(bool madePeace, CCountryTag actor, List<CCountryTag> peace, List<CCountryTag> fightOn)
	{
		if (madePeace)
		{
			if (!peace.Contains(actor) && !fightOn.Contains(actor))
			{
				peace.Add(actor);
			}
			return;
		}
		if (!fightOn.Contains(actor))
		{
			fightOn.Add(actor);
		}
		peace.Remove(actor);
	}

	/// <summary>Whether any goal of the war names the country as its recipient.</summary>
	private static bool Named(CWar war, CCountryTag tag)
	{
		foreach (List<CWarGoal> goals in war.Goals.Values)
		{
			if (goals.Exists(goal => goal.Recipient == tag))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The country's vassals, as it holds them now.</summary>
	private static List<CCountry> Vassals(CGameState state, CCountry country)
	{
		var vassals = new List<CCountry>();
		foreach (CCountry other in state.Countries)
		{
			if (other.Overlord == country.Tag)
			{
				vassals.Add(other);
			}
		}
		return vassals;
	}

	/// <summary>A casus belli type by its name; type 0, the null type, where there is none.</summary>
	private static CCasusBelliType? Type(CGameState state, string name)
	{
		foreach (CCasusBelliType type in state.CasusBelliTypes)
		{
			if (type.Name == name)
			{
				return type;
			}
		}
		return null;
	}
}
