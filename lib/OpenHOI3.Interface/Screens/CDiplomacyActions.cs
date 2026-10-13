using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The diplomacy screen's actions towards the chosen country: which rows it lists and in what order,
/// which are greyed, each row's text and tooltip, and the request dialog's description and chance.
/// Only the kinds built are listed. See <c>docs/mechanics/agreements.md</c>, section 7.
/// </summary>
public static class CDiplomacyActions
{
	private const string NewLine = "\n";

	/// <summary>
	/// The rows the player may make towards the chosen country, in the original's order: alliance, NAP,
	/// guarantee, the two kinds of transit rights, alignment, embargo, and at peace influence. Each is
	/// an offer proposed now.
	/// </summary>
	public static List<COffer> Rows(CGameState state, CCountry player, CCountry chosen)
	{
		var rows = new List<COffer>();
		COffer Row(EOfferKind kind, bool value) =>
			new(kind, player.Tag, chosen.Tag, EOfferStage.Proposed, value, state.TotalHours);
		bool atWar = CCountryLists.AtWar(state, player.Tag, chosen.Tag);

		if (CCountryLists.HasAlliance(state, player.Tag, chosen.Tag))
		{
			rows.Add(Row(EOfferKind.Alliance, false));
		}
		else if (!(COfferRules.RealFaction(player) && COfferRules.RealFaction(chosen)) && !COfferRules.IsPuppet(player)
			&& !COfferRules.InExile() && COfferRules.RecipientFree(player, chosen))
		{
			rows.Add(Row(EOfferKind.Alliance, true));
		}
		rows.Add(Row(EOfferKind.NonAggressionPact, COfferRules.Nap(state, player.Tag, chosen.Tag) is null));
		if (COfferRules.Guarantee(state, player.Tag, chosen.Tag) is not { } guarantee || guarantee.Guarantor == chosen.Tag)
		{
			rows.Add(Row(EOfferKind.Guarantee, true));
		}
		if (!COfferRules.OneRealFaction(player, chosen))
		{
			rows.Add(Row(EOfferKind.AskMilitaryAccess, !COfferRules.HasAccess(player, chosen)));
			rows.Add(Row(EOfferKind.OfferMilitaryAccess, !COfferRules.HasAccess(chosen, player)));
		}
		if (COfferRules.Align(state, player.Tag, chosen.Tag) is not null)
		{
			rows.Add(Row(EOfferKind.Align, false));
		}
		else if (!COfferRules.RealFaction(player) && !atWar && !COfferRules.IsPuppet(player))
		{
			rows.Add(Row(EOfferKind.Align, true));
		}
		if (COfferRules.Embargo(state, player.Tag, chosen.Tag) is not { } embargo)
		{
			rows.Add(Row(EOfferKind.Embargo, true));
		}
		else if (embargo.First == player.Tag)
		{
			rows.Add(Row(EOfferKind.Embargo, false));
		}
		if (!atWar)
		{
			bool running = Influencing(state, player.Tag, chosen.Tag);
			if (!COfferRules.RealFaction(chosen) && !COfferRules.IsPuppet(chosen) && (running || COfferRules.IsMajor(player)))
			{
				// Listed only where it would do something.
				COffer influence = Row(EOfferKind.Influence, !running);
				if (COfferRules.IsValid(state, influence))
				{
					rows.Add(influence);
				}
			}
		}
		return rows;
	}

	/// <summary>Whether a row's button is greyed: it may not be pressed, or a diplomat is on his way.</summary>
	public static bool IsGreyed(CGameState state, COffer offer)
	{
		if (!COfferRules.IsSelectable(state, offer))
		{
			return true;
		}
		return state.Country(offer.Actor) is { } actor && state.Country(offer.Recipient) is { } recipient
			&& COfferRules.DiplomatEnRoute(state, actor, recipient);
	}

	/// <summary>The key a row's texts are made from, by its kind and value.</summary>
	public static string Prefix(COffer offer)
	{
		return offer.Kind switch
		{
			EOfferKind.Alliance => offer.Value ? "ALLIANCE" : "CANCELALIANCE",
			EOfferKind.NonAggressionPact => offer.Value ? "NAP" : "CANCELNAP",
			EOfferKind.Guarantee => "GUARANT",
			EOfferKind.AskMilitaryAccess => offer.Value ? "MILACC" : "CANCELMILACC",
			EOfferKind.OfferMilitaryAccess => offer.Value ? "OFFERMILACC" : "REVOKEMILACC",
			EOfferKind.Embargo => offer.Value ? "EMBARGO" : "CANCELEMBARGO",
			EOfferKind.Influence => offer.Value ? "INFL" : "STOPINFL",
			_ => offer.Value ? "ALIGN" : "STOPALIGN",
		};
	}

	/// <summary>A row's button text and the dialog's title: <c>&lt;prefix&gt;TITLE</c>.</summary>
	public static string Title(Func<string, string> localise, COffer offer) => localise(Prefix(offer) + "TITLE");

	/// <summary>A row's tooltip: the first of the kind's reasons that holds, else what the action does.</summary>
	public static string Tooltip(Func<string, string> localise, CGameState state, COffer offer)
	{
		if (state.Country(offer.Actor) is not { } actor || state.Country(offer.Recipient) is not { } recipient)
		{
			return "";
		}
		string? said = offer.Kind switch
		{
			EOfferKind.Alliance => Alliance(localise, state, offer, actor, recipient),
			EOfferKind.NonAggressionPact => Nap(localise, state, offer, actor, recipient),
			EOfferKind.Guarantee => Guarantee(localise, state, offer, actor, recipient),
			EOfferKind.AskMilitaryAccess => AskAccess(localise, state, offer, actor, recipient),
			EOfferKind.OfferMilitaryAccess => OfferAccess(localise, state, offer, actor, recipient),
			EOfferKind.Embargo => Embargo(localise, state, offer, actor, recipient),
			EOfferKind.Influence => Influence(localise, state, offer, actor, recipient),
			_ => Align(localise, state, offer, actor, recipient),
		};
		return said ?? "";
	}

	/// <summary>The request dialog's description: what the action is, and what it costs where it does.</summary>
	public static string Description(Func<string, string> localise, CGameState state, COffer offer)
	{
		if (state.Country(offer.Actor) is not { } actor || state.Country(offer.Recipient) is not { } recipient)
		{
			return "";
		}
		string Country(string key) => CTextTemplate.Fill(localise(key), ("COUNTRY", recipient.Name));
		string Recipient(string key) => CTextTemplate.Fill(localise(key), ("RECIPIENT", recipient.Name));
		string Cost() => CTextTemplate.Fill(localise("DIP_INFLUENCECOST"),
			("COST", CNumberText.Format(COfferRules.Cost(state, offer), 2)));
		switch (offer.Kind)
		{
			case EOfferKind.Alliance:
				return Country(offer.Value ? "ALLIANCENEWDESC" : "BREAKALLIANCENEWDESC") + NewLine + Cost();
			case EOfferKind.NonAggressionPact:
				if (offer.Value)
				{
					return Country("NAPNEWDESC") + NewLine + Cost();
				}
				string threat = CTextTemplate.Fill(localise("DIP_THREAT"),
					("AMOUNT", CNumberText.Format(COfferRules.Define(state, "LEAVE_NAP_THREAT_COST"), 2)),
					("PROV", state.Province(recipient.Capital).Name));
				return Country("BREAKNAPNEWDESC") + NewLine + threat + NewLine + Cost();
			case EOfferKind.Guarantee:
				// ALLIES_GUARANT follows where the actor's faction makes its members guarantee too, which
				// nothing in the original ever sets.
				return Country("GUARANTNEWDESC") + NewLine + Cost();
			case EOfferKind.AskMilitaryAccess:
				return Country(offer.Value ? "MILACCNEWDESC" : "CANCELMILACCNEWDESC");
			case EOfferKind.OfferMilitaryAccess:
				return Country(offer.Value ? "OFFERMILACCNEWDESC" : "REVOKEMILACCNEWDESC");
			case EOfferKind.Embargo:
				return offer.Value ? Recipient("EMBARGONEWDESC") + NewLine + Cost() : Recipient("CANCELEMBARGONEWDESC");
			case EOfferKind.Influence:
				return offer.Value ? Drift(localise, state, "INFLNEWDESC", recipient, actor.Faction) : Recipient("STOPINFLNEWDESC");
			default:
				return offer.Value ? Drift(localise, state, "ALIGNNEWDESC", recipient, recipient.Faction) : Recipient("STOPALIGNNEWDESC");
		}
	}

	/// <summary>
	/// The dialog's word for the recipient's AI's likely answer, from its score; nothing where the AI does
	/// not answer the kind.
	/// </summary>
	public static string Chance(Func<string, string> localise, CGameState state, COffer offer)
	{
		if (!COfferRules.AiAnswers(offer))
		{
			return "";
		}
		int score = CPactPasses.Score(state, offer);
		string key = score switch
		{
			< 1 => "DIPCHANCE_IMPOSSIBLE",
			< 10 => "DIPCHANCE_VUNLIKELY",
			< 40 => "DIPCHANCE_UNLIKELY",
			< 60 => "DIPCHANCE_MAYBE",
			< 80 => "DIPCHANCE_LIKELY",
			_ => "DIPCHANCE_VLIKELY",
		};
		return localise(key);
	}

	private static string? Alliance(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (Common(localise, state, actor, recipient) is { } common)
		{
			return common;
		}
		if (offer.Value)
		{
			CFixedPoint limit = COfferRules.Define(state, "ALLIANCE_NEUTRALITY_LIMIT");
			if (actor.EffectiveNeutrality > limit)
			{
				return Neutrality(localise, "DIP_TO_NEUTR_THREAT", actor, limit);
			}
			if (recipient.EffectiveNeutrality > limit)
			{
				return Neutrality(localise, "DIP_THEY_TO_NEUTR_THREAT", recipient, limit);
			}
		}
		return NoInfluence(localise, state, offer, actor, 0)
			?? localise(offer.Value ? "OFFERALLIANCEDESC" : "CANCELALLIANCEDESC");
	}

	private static string? Nap(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if ((Common(localise, state, actor, recipient) ?? NoInfluence(localise, state, offer, actor, 0)) is { } said)
		{
			return said;
		}
		if (offer.Value)
		{
			return localise("NAPEDESC");
		}
		if (COfferRules.Nap(state, actor.Tag, recipient.Tag) is { } nap)
		{
			int unbreakable = (int)(COfferRules.Define(state, "NAP_UNBREAKABLE_MONTHS").Raw / 1000);
			int window = (int)(COfferRules.Define(state, "NAP_FORCE_BALANCE_RULE_MONTHS").Raw / 1000);
			if (state.TotalHours < COfferRules.AddMonths(nap.Start, unbreakable))
			{
				return CTextTemplate.Fill(localise("CANCELNAP_TOO_SOON_DESC"),
					("MONTHS", unbreakable.ToString(System.Globalization.CultureInfo.InvariantCulture)));
			}
			for (int step = 1; step <= 3; step++)
			{
				if (state.TotalHours >= COfferRules.AddMonths(nap.Start, unbreakable + step * window))
				{
					continue;
				}
				// The balance of brigades along the border is not built, so it is nought - below any limit.
				CFixedPoint limit = COfferRules.Define(state, "NAP_BREAK_FORCE_BALANCE_" + step);
				return CTextTemplate.Fill(localise("CANCELNAP_TOO_LOW_BALANCE"),
					("LIMIT_PERC", CNumberText.Trimmed(limit * 100)),
					("CURR_PERC", CNumberText.Trimmed(CFixedPoint.Zero)));
			}
		}
		return localise("CANCELNAPDESC");
	}

	private static string? Guarantee(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if ((EnRoute(localise, state, actor, recipient) ?? NoInfluence(localise, state, offer, actor, 2)) is { } said)
		{
			return said;
		}
		if (COfferRules.Guarantee(state, actor.Tag, recipient.Tag) is { } standing)
		{
			return localise(standing.Guarantor != recipient.Tag ? "HASGUARANT" : "THEYGUARANT");
		}
		if (CCountryLists.AtWar(state, actor.Tag, recipient.Tag))
		{
			return localise("WEWAR");
		}
		CFixedPoint limit = COfferRules.Define(state, "GUARANTEE_NEUTRALITY_LIMIT");
		if (actor.EffectiveNeutrality > limit)
		{
			return CTextTemplate.Fill(localise("DIP_TO_NEUTR"),
				("NOW", CNumberText.Format(actor.EffectiveNeutrality, 2)), ("LIM", CNumberText.Trimmed(limit)));
		}
		return localise("GUARANTDESC");
	}

	private static string? AskAccess(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if ((EnRoute(localise, state, actor, recipient) ?? NoInfluence(localise, state, offer, actor, 2)) is { } said)
		{
			return said;
		}
		if (!offer.Value && COfferRules.HasAccess(actor, recipient))
		{
			return localise(COfferRules.HasUnitsIn(state, actor, recipient) ? "CANCELMILACCDESC_NOPES_TROOPS" : "CANCELMILACCDESC");
		}
		return localise(CCountryLists.AtWar(state, actor.Tag, recipient.Tag) ? "ISATWAR" : "MILACCDESC");
	}

	private static string? OfferAccess(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		// Revoking says its own title.
		return EnRoute(localise, state, actor, recipient) ?? NoInfluence(localise, state, offer, actor, 2)
			?? localise(offer.Value ? "OFFERMILACCDESC" : "REVOKEMILACCTITLE");
	}

	private static string? Embargo(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (Common(localise, state, actor, recipient) is { } common)
		{
			return common;
		}
		if (!offer.Value)
		{
			return localise("CANCELEMBARGODESC");
		}
		if (NoInfluence(localise, state, offer, actor, 0) is { } lacking)
		{
			return lacking;
		}
		CFixedPoint limit = COfferRules.Define(state, "EMBARGO_NETRALITY");
		return actor.EffectiveNeutrality > limit
			? Neutrality(localise, "DIP_TO_NEUTR_THREAT", actor, limit)
			: localise("EMBARGODESC");
	}

	private static string? Influence(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (EnRoute(localise, state, actor, recipient) is { } enRoute)
		{
			return enRoute;
		}
		if (!offer.Value)
		{
			return localise("STOPNFLDESC");
		}
		if (NoInfluence(localise, state, offer, actor, 2) is { } said)
		{
			return said;
		}
		if (CCountryLists.AtWar(state, actor.Tag, recipient.Tag))
		{
			return localise("WEWAR");
		}
		if (COfferRules.AtWarWithFaction(state, recipient, actor.Faction))
		{
			return localise("THEY_AT_WAR_WITH_FACTION");
		}
		if (COfferRules.HasInfluenceModifier(state, recipient, actor.Faction))
		{
			return Already(localise, state, actor, recipient);
		}
		return localise("INFLDESC");
	}

	/// <summary>
	/// "Already being influenced by": the last member of the actor's faction influencing the recipient, or
	/// aligning with it either way, with the day its influence ends; <c>ERROR</c>, which has no text, where none is.
	/// </summary>
	private static string Already(Func<string, string> localise, CGameState state, CCountry actor, CCountry recipient)
	{
		CCountry? nation = null;
		long ends = 0;
		long days = COfferRules.Define(state, "DAYS_OF_INFLUENCE_RELATION_CHANGE").Raw / 1000;
		foreach (CCountry member in CCountryLists.FactionMembers(state, actor.Faction))
		{
			CAlign? align = COfferRules.Align(state, recipient.Tag, member.Tag) ?? COfferRules.Align(state, member.Tag, recipient.Tag);
			if (state.Influences.Find(i => i.Influencer == member.Tag && i.Influenced == recipient.Tag) is { } influence)
			{
				nation = member;
				ends = influence.Start + days * CGameState.HoursPerDay;
			}
			else if (align is not null)
			{
				nation = member;
				ends = AlignEnds(state, align);
			}
		}
		if (nation is null)
		{
			return localise("ERROR");
		}
		return CTextTemplate.Fill(localise("DIP_ALLREADY_INF"), ("RECIPIENT", recipient.Name), ("NATION", nation.Name),
			("DATE", HourDate(localise, ends)));
	}

	private static string? Align(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if ((EnRoute(localise, state, actor, recipient) ?? NoInfluence(localise, state, offer, actor, 2)) is { } said)
		{
			return said;
		}
		if (CCountryLists.AtWar(state, actor.Tag, recipient.Tag))
		{
			return localise("WEWAR");
		}
		if (!COfferRules.RealFaction(recipient))
		{
			return localise("ALIGN_NO_FACTION");
		}
		string faction = localise(recipient.Faction);
		if (offer.Value && COfferRules.Leader(state, recipient.Faction) is { } leader && leader != recipient.Tag)
		{
			return CTextTemplate.Fill(localise("ALIGN_NOT_LEADER"), ("FACTION", faction),
				("LEADER", state.Country(leader)?.Name ?? leader.Value));
		}
		if (offer.Value && COfferRules.Align(state, actor.Tag, recipient.Tag) is { } running)
		{
			return CTextTemplate.Fill(localise("ALIGN_ALLREADY_ALL"), ("FACTION", faction),
				("DATE", HourDate(localise, AlignEnds(state, running))));
		}
		// Stopping says what starting does.
		return localise(COfferRules.AtWarWithFaction(state, actor, recipient.Faction) ? "AT_WAR_WITH_FACTION" : "ALIGNDESC");
	}

	/// <summary>
	/// What every agreement but the guarantee, the transit rights, influence and alignment tests first:
	/// itself, a diplomat on his way, war between them, the actor a puppet.
	/// </summary>
	private static string? Common(Func<string, string> localise, CGameState state, CCountry actor, CCountry recipient)
	{
		if (actor == recipient)
		{
			return localise("ACT_NO_SELF");
		}
		if (EnRoute(localise, state, actor, recipient) is { } enRoute)
		{
			return enRoute;
		}
		if (CCountryLists.AtWar(state, actor.Tag, recipient.Tag))
		{
			return localise("WEWAR");
		}
		return COfferRules.IsPuppet(actor) ? localise("SUBJECT") : null;
	}

	/// <summary>A diplomat on his way, with the hour he arrives.</summary>
	private static string? EnRoute(Func<string, string> localise, CGameState state, CCountry actor, CCountry recipient)
	{
		if (!COfferRules.DiplomatEnRoute(state, actor, recipient))
		{
			return null;
		}
		return CTextTemplate.Fill(localise("ENROUTE"),
			("DATE", HourDate(localise, CGameState.Status(actor, recipient).DiplomatUntil)));
	}

	/// <summary>Too little influence for the offer, its cost to as many places as the kind writes it.</summary>
	private static string? NoInfluence(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, int decimals)
	{
		CFixedPoint cost = COfferRules.Cost(state, offer);
		if (actor.DiplomaticInfluence >= cost)
		{
			return null;
		}
		return CTextTemplate.Fill(localise("DIP_NO_INFL"), ("INF", CNumberText.Format(cost, decimals)));
	}

	private static string Neutrality(Func<string, string> localise, string key, CCountry country, CFixedPoint limit)
	{
		return CTextTemplate.Fill(localise(key), ("VAL", CNumberText.Format(country.EffectiveNeutrality, 2)),
			("LIM", CNumberText.Trimmed(limit)));
	}

	/// <summary>
	/// An influence's or an alignment's dialog: how far the faction's influence modifier moves the recipient
	/// each day, and for how many days - the influence's define for both.
	/// </summary>
	private static string Drift(Func<string, string> localise, CGameState state, string key, CCountry recipient, string faction)
	{
		long days = COfferRules.Define(state, "DAYS_OF_INFLUENCE_RELATION_CHANGE").Raw / 1000;
		string time = days < 0 ? localise("UNLIMITED") : days.ToString(System.Globalization.CultureInfo.InvariantCulture);
		CFixedPoint value = state.FindStaticModifier(COfferRules.InfluenceModifier(state, faction)) is { } modifier
			? modifier[CAlignment.AlignTowards]
			: CFixedPoint.Zero;
		return CTextTemplate.Fill(localise(key), ("RECIPIENT", recipient.Name), ("TIME", time),
			("VAL", CNumberText.Format(value, 2)), ("FACTION", localise(faction)));
	}

	/// <summary>The hour an alignment's days run out.</summary>
	private static long AlignEnds(CGameState state, CAlign align)
	{
		long days = COfferRules.Define(state, "DAYS_OF_ALIGN_RELATION_CHANGE").Raw / 1000;
		return align.Start + days * CGameState.HoursPerDay;
	}

	private static bool Influencing(CGameState state, CCountryTag influencer, CCountryTag influenced) =>
		state.Influences.Exists(i => i.Influencer == influencer && i.Influenced == influenced);

	/// <summary>An hour as the original writes it: " 13:00, 1 January, 1936", a space first.</summary>
	private static string HourDate(Func<string, string> localise, long hour)
	{
		long day = Math.Max(hour, 0) / CGameState.HoursPerDay;
		return CMessageText.Date(localise, CDate.FromDayNumber((int)day), (int)(Math.Max(hour, 0) % CGameState.HoursPerDay));
	}
}
