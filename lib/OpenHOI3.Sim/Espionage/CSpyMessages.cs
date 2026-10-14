using System.Globalization;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Sim.Espionage;

/// <summary>
/// What spies tell a player: a catch, to the catcher or else to the caught spies' country, and a technology stolen, to
/// the thief or else to its victim where it has spies enough on counter-espionage at home. Nobody else is told. Each
/// variable that is a country or a technology is written as its tag or its name, for the messages to put into words.
/// See <c>docs/mechanics/espionage.md</c>, section 12.
/// </summary>
public static class CSpyMessages
{
	public const string WeCaught = "SPIESFOUNDUS";
	public const string TheyCaught = "SPIESFOUNDTHEM";
	public const string WeStoleOnce = "WE_STEAL_TECH_ONCE";
	public const string WeStoleRepeat = "WE_STEAL_TECH_REPEAT";
	public const string Stolen = "TECH_STOLEN";
	public const string StolenByUnknown = "TECH_STOLEN_BY_UNKNOWN";

	/// <summary>The victim hears of a steal above two spies on counter-espionage at home, and learns who above five.</summary>
	private const long Heard = 2000;
	private const long Named = 5000;

	/// <summary><paramref name="owner"/>'s spy in <paramref name="target"/> caught by <paramref name="catcher"/>.</summary>
	public static void Caught(CGameState state, CCountry catcher, CCountry owner, CCountry target)
	{
		if (state.Played.Contains(catcher.Tag))
		{
			Raise(state, WeCaught, catcher.Tag, owner.Tag, catcher.Tag, ("WHERE", owner.Tag.Value));
		}
		else if (state.Played.Contains(owner.Tag))
		{
			Raise(state, TheyCaught, owner.Tag, catcher.Tag, owner.Tag, ("WHERE", target.Tag.Value));
		}
	}

	/// <summary>The actor has stolen the target's <paramref name="level"/> in <paramref name="technology"/>.</summary>
	public static void TechnologyStolen(CGameState state, CCountry actor, CCountry target, CTechnology technology, int level)
	{
		string levelText = level.ToString(CultureInfo.InvariantCulture);
		if (state.Played.Contains(actor.Tag))
		{
			Raise(state, technology.AdditionalOffset == 0 ? WeStoleOnce : WeStoleRepeat, actor.Tag, target.Tag, actor.Tag,
				("LEVEL", levelText), ("TYPE", technology.Name), ("TARGET_ADJ", target.Tag.Value), ("EFFECT", technology.Name));
			return;
		}
		if (!state.Played.Contains(target.Tag))
		{
			return;
		}
		CSpyPresence home = CGameState.Presence(target, target);
		long watching = home.Level * home.Share(ESpyMission.CounterEspionage) / 1000;
		if (watching <= Heard)
		{
			return;
		}
		if (watching > Named)
		{
			Raise(state, Stolen, target.Tag, actor.Tag, target.Tag,
				("LEVEL", levelText), ("TYPE", technology.Name), ("RESPONSIBLE", actor.Tag.Value));
		}
		else
		{
			Raise(state, StolenByUnknown, target.Tag, target.Tag, target.Tag, ("LEVEL", levelText), ("TYPE", technology.Name));
		}
	}

	private static void Raise(CGameState state, string type, CCountryTag first, CCountryTag second, CCountryTag told,
		params (string Name, string Value)[] values)
	{
		state.RaisePactMessage(new CPactMessage(type, "", "", first, second) { Told = told, Values = values });
	}
}
