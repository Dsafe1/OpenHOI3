using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The effects on a country's government, its parties and its ministers: <c>docs/mechanics/politics.md</c>,
/// section 8.
/// </summary>
public static class CPoliticsEffects
{
	public static bool TryRun(CGameState state, CEffect effect, CEventScope scope)
	{
		CCountry? country = state.Country(scope.Country);
		CScriptArgument value = effect.Argument;
		switch (effect.Keyword)
		{
			case "organisation":
				if (country is not null && state.Ideology(country.RulingIdeology) is { } ruling)
				{
					AddToRuling(country.Organisation, ruling.Index, value);
				}
				return true;
			case "popularity":
				if (country is not null && state.Ideology(country.RulingIdeology) is { } governing)
				{
					AddToRuling(country.Popularity, governing.Index, value);
				}
				return true;
			case "government":
				if (country is not null && GovernmentNamed(state, value, scope) is { } government)
				{
					country.GovernmentType = government.Name;
					CMinisterPosts.Fill(state, country, refillAll: true, electedOnly: false);
					Ai.CAiStrategies.AfterGovernmentChanged(state, country);
				}
				return true;
			case "coup_by":
				if (country is not null)
				{
					Coup(state, country, value, scope);
				}
				return true;
			case "do_election":
				if (state.Country(value.IsThis ? scope.Root.Country : value.IsFrom ? scope.FromCountry
					: CEventScope.Known(value.Tag)) is { } holding)
				{
					CElections.Hold(state, holding);
				}
				return true;
			case "remove_minister":
				if (country is not null && Removed(state, country, value, scope) is { } minister)
				{
					Remove(state, country, minister);
				}
				return true;
			default:
				return false;
		}
	}

	/// <summary>
	/// <c>&lt;ideology&gt; = { organisation = x popularity = y }</c>: each written figure added to that party's, a result
	/// below nought not applied, and no upper limit.
	/// </summary>
	public static void IdeologyBlock(CGameState state, CEffect effect, CEventScope scope)
	{
		if (state.Country(scope.Country) is not { } country || state.Ideology(effect.Key) is not { } ideology)
		{
			return;
		}
		AddToParty(country.Organisation, ideology.Index, CEffects.Field(effect, "organisation")?.Amount ?? CFixedPoint.Zero);
		AddToParty(country.Popularity, ideology.Index, CEffects.Field(effect, "popularity")?.Amount ?? CFixedPoint.Zero);
	}

	/// <summary>The ruling party's figure plus the value, held to 0..100; the new figure to the effects' tally.</summary>
	private static void AddToRuling(CPartyFigures figures, int ruling, CScriptArgument value)
	{
		long next = Math.Clamp(figures[ruling] + value.Amount.Raw, 0, 100000);
		figures.Set(ruling, next, CPartyFigures.EffectSlot);
	}

	private static void AddToParty(CPartyFigures figures, int ideology, CFixedPoint amount)
	{
		if (amount != CFixedPoint.Zero)
		{
			figures.Set(ideology, figures[ideology] + amount.Raw, CPartyFigures.EffectSlot);
		}
	}

	/// <summary>The government a value names: its name, or THIS's or FROM's. The rebels' is not built and is none.</summary>
	private static CGovernment? GovernmentNamed(CGameState state, CScriptArgument value, CEventScope scope)
	{
		if (value.IsThis)
		{
			return state.Country(scope.Root.Country) is { } self ? state.GovernmentOf(self) : null;
		}
		if (value.IsFrom)
		{
			return state.Country(scope.FromCountry) is { } from ? state.GovernmentOf(from) : null;
		}
		return state.Governments.TryGetValue(value.Text, out CGovernment? named)
			&& named.Name.Equals(value.Text, StringComparison.Ordinal) ? named : null;
	}

	/// <summary>
	/// A new ruling party - the one named, or THIS's or FROM's; with this or from also the instigator's government and
	/// the midpoint of the two alignments; then every post filled again, the election dated today, the seats allotted.
	/// </summary>
	private static void Coup(CGameState state, CCountry country, CScriptArgument value, CEventScope scope)
	{
		CCountry? instigator = value.IsThis ? state.Country(scope.Root.Country)
			: value.IsFrom ? state.Country(scope.FromCountry)
			: null;
		CIdeology? ideology = instigator is not null ? state.Ideology(instigator.RulingIdeology)
			: state.Ideology(value.Text) is { } named && named.Name.Equals(value.Text, StringComparison.Ordinal) ? named : null;
		if (ideology is null || (value.IsThis || value.IsFrom) && instigator is null)
		{
			return;
		}
		country.RulingIdeology = ideology.Name;
		if (instigator is not null)
		{
			country.AlignmentX = (country.AlignmentX + instigator.AlignmentX) * 1000 / 2000;
			country.AlignmentY = (country.AlignmentY + instigator.AlignmentY) * 1000 / 2000;
			country.GovernmentType = instigator.GovernmentType;
		}
		CMinisterPosts.Fill(state, country, refillAll: true, electedOnly: false);
		country.LastElection = state.Date;
		CCabinet.Allot(state, country);
		// A coup by a country changes the government, and the strategy is made again; one by an ideology alone does not.
		if (instigator is not null)
		{
			Ai.CAiStrategies.AfterGovernmentChanged(state, country);
		}
	}

	/// <summary>
	/// Which minister <c>remove_minister</c> means: by his id, any country's; by a post's name, its holder; or for
	/// <c>yes</c> one of the changeable posts that has a holder, drawn once on a copy of the scope, so the scope's own
	/// state of chance does not move.
	/// </summary>
	private static CMinister? Removed(CGameState state, CCountry country, CScriptArgument value, CEventScope scope)
	{
		if (value.IsYes)
		{
			var held = new List<CMinister>();
			foreach (CGovernmentPosition position in state.GovernmentPositions)
			{
				if (position.Changeable && country.Government.TryGetValue(position.Name, out CMinister? holder))
				{
					held.Add(holder);
				}
			}
			return held.Count == 0 ? null : held[scope.With(scope.Country, scope.Province).Draw(held.Count)];
		}
		if (CPoliticsConditions.IsChangeable(state, value.Text) || IsPost(state, value.Text))
		{
			return country.Government.GetValueOrDefault(value.Text);
		}
		long id = CConditions.WholeOf(value.Amount);
		return id > 0 ? CPoliticsConditions.FindMinister(state, country, (int)id) : null;
	}

	/// <summary>
	/// His first changeable post emptied, and him out of the pool - always: the original's test meant to keep a
	/// minister who also holds a fixed post never matches.
	/// </summary>
	private static void Remove(CGameState state, CCountry country, CMinister minister)
	{
		foreach (CGovernmentPosition position in state.GovernmentPositions)
		{
			if (position.Changeable && country.Government.TryGetValue(position.Name, out CMinister? holder) && holder == minister)
			{
				country.Government.Remove(position.Name);
				break;
			}
		}
		country.PossibleMinisters.Remove(minister);
	}

	private static bool IsPost(CGameState state, string name)
	{
		foreach (CGovernmentPosition position in state.GovernmentPositions)
		{
			if (position.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}
