using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Politics;

/// <summary>
/// A country's laws changed: what it costs, whether a law is allowed, the change and what it sets off, and the
/// laws a country at peace no longer allowed is given back. See <c>docs/mechanics/politics.md</c>, section 9.
/// </summary>
public static class CLaws
{
	public const string AppointedMessage = "LAW_APPOINTED";

	/// <summary>
	/// What changing to the law costs in money: its <c>cost</c> times thirty days of money from the country's whole
	/// IC - every division truncating - and nothing for a country no person plays.
	/// </summary>
	public static CFixedPoint ChangeCost(CGameState state, CCountry country, CLaw law)
	{
		if (!state.Played.Contains(country.Tag))
		{
			return CFixedPoint.Zero;
		}
		long money = country.MaxIc * 1000L * 30000 / 1000;
		money = money * state.Defines[CResources.EconomySection, CResources.IcToMoney].Raw / 1000;
		money = money * (1000 + country.Modifier["global_money"].Raw) / 1000;
		return new CFixedPoint(law.Cost.Raw * money / 1000);
	}

	/// <summary>Whether the law's <c>allow</c> holds for the country, asked with no FROM, no province and no chance.</summary>
	public static bool IsAllowed(CGameState state, CCountry country, CLaw law)
	{
		return law.Allow is not { } allow || CConditions.All(state, allow, CEventScope.Start(country.Tag), null);
	}

	/// <summary>
	/// The change itself, checking nothing: the law in its group's place; with <paramref name="payCost"/> its cost
	/// taken off the money, which may go below nought; the player told where <paramref name="postMessage"/> is set;
	/// and then the country's modifier made again, its build queue priced again with the work done left alone, and
	/// every brigade held to what the new modifier allows, the men of the strength taken off given back.
	/// </summary>
	public static void Change(CGameState state, CCountry country, CLaw law, string group, bool payCost, bool postMessage)
	{
		country.Laws[group] = law;
		if (payCost && state.Stockpile(country) is { } pool)
		{
			pool[EGoods.Money] = pool[EGoods.Money] - ChangeCost(state, country, law);
		}
		if (postMessage && state.Played.Contains(country.Tag))
		{
			state.RaisePactMessage(new Agreements.CPactMessage(AppointedMessage, "", "", country.Tag, country.Tag)
			{
				Told = country.Tag,
				Values = new[] { ("LAWNAME", law.Name), ("DESC", law.Name), ("EFFECT", law.Name), ("POSITION", group) },
			});
		}
		Modifiers.CCountryModifiers.RebuildOne(state, country);
		Construction.CBuildQueue.RecalculateAll(country);
		Units.COrganisation.HoldToCeilings(state, country);
	}

	/// <summary>
	/// After a change of diplomacy, for a country not at war: each group's law that its <c>allow</c> no longer permits
	/// gives way to the first other law of the group that it does, posted free with its message - the player's
	/// country too. A group where none is allowed keeps its law.
	/// </summary>
	public static void OnDiplomacyChanged(CGameState state, CCountry country)
	{
		// The original's change of diplomacy also marks the country's theatres (theatres.md, section 1).
		country.Strategy.TheatresDirty = true;
		if (state.IsAtWar(country.Tag))
		{
			return;
		}
		foreach (CLawGroup group in state.LawGroups)
		{
			if (!country.Laws.TryGetValue(group.Name, out CLaw? current) || IsAllowed(state, country, current))
			{
				continue;
			}
			if (group.Laws.Find(law => law != current && IsAllowed(state, country, law)) is { } allowed)
			{
				state.Outbox.Add(new Commands.CChangeLawCommand(country.Tag, allowed.Name, group.Name, payCost: false, postMessage: true));
			}
		}
	}
}
