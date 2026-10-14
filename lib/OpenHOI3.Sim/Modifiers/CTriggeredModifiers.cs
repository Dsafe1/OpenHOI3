using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Modifiers;

/// <summary>
/// Which triggered modifiers a country has, tested once a day straight before its modifier is rebuilt and on entering a
/// game: an entry whose two blocks hold joins the end of the country's list, and one on the list whose blocks no longer
/// both hold leaves it, the player told of each. See <c>docs/mechanics/modifiers.md</c>, section 5.
/// </summary>
public static class CTriggeredModifiers
{
	public const string Gained = "TRIGMOD";
	public const string Lost = "TRIGMODLOST";
	public const string Messenger = "MESSAGE_HEAD_GOV";

	/// <summary>The country's list made right for today: the new ones in file order at its end, then the lapsed taken out.</summary>
	public static void Update(CGameState state, CCountry country, CScriptReport? report = null)
	{
		foreach (CTriggeredModifier entry in state.TriggeredModifiers)
		{
			if (Applies(state, entry, country, report) && !country.TriggeredModifiers.Contains(entry))
			{
				country.TriggeredModifiers.Add(entry);
				Tell(state, country, Gained, entry);
			}
		}
		for (int i = 0; i < country.TriggeredModifiers.Count; i++)
		{
			CTriggeredModifier entry = country.TriggeredModifiers[i];
			if (!Applies(state, entry, country, report))
			{
				Tell(state, country, Lost, entry);
				country.TriggeredModifiers.RemoveAt(i--);
			}
		}
	}

	/// <summary>Whether both its blocks hold for the country: its <c>potential</c>, and then its <c>trigger</c>.</summary>
	public static bool Applies(CGameState state, CTriggeredModifier entry, CCountry country, CScriptReport? report = null)
	{
		CEventScope scope = CEventScope.Start(country.Tag);
		return CConditions.All(state, entry.Potential, scope, report) && CConditions.All(state, entry.Trigger, scope, report);
	}

	/// <summary>Whether its <c>potential</c> holds for the country, its <c>trigger</c> not asked.</summary>
	public static bool PotentialHolds(CGameState state, CTriggeredModifier entry, CCountry country)
	{
		return CConditions.All(state, entry.Potential, CEventScope.Start(country.Tag), null);
	}

	/// <summary>
	/// A played country told of a modifier come or gone: its NAME and EFFECT, and on coming its EXPLANATION, each written
	/// out from the entry's key where the messages are put into words.
	/// </summary>
	private static void Tell(CGameState state, CCountry country, string type, CTriggeredModifier entry)
	{
		if (!state.Played.Contains(country.Tag))
		{
			return;
		}
		(string, string)[] values = type == Gained
			? new[] { ("NAME", entry.Name), ("EFFECT", entry.Name), ("EXPLANATION", entry.Name) }
			: new[] { ("NAME", entry.Name), ("EFFECT", entry.Name) };
		state.RaisePactMessage(new CPactMessage(type, "", "", country.Tag, country.Tag)
		{
			Told = country.Tag,
			Messenger = Messenger,
			Values = values,
		});
	}
}
