using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

/// <summary>
/// A trial, not a check: makes a new game with the install's Lua and has one country declare war on another
/// as the player would from the declare-war dialog, then runs some hours with the AI answering the calls to
/// arms, and says who is where:
///
///   OpenHOI3.SaveCheck declare &lt;install folder&gt; [actor] [recipient] [limited] [hours] [peace] [occupy [lend=TAG]]
///
/// Germany on Poland for 48 hours with nothing given. First it says what a new game holds of war: the casus
/// belli types read, the history's wars with their goals, who is mobilised. No save keeps a declaration. See
/// <c>docs/mechanics/war.md</c>.
/// </summary>
internal static class CDeclareTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck declare <install folder> [actor] [recipient] [limited] [hours] [peace] [occupy [lend=TAG]]");
			return 2;
		}
		string[] tags = args.Skip(2).Where(a => a.Length == 3 && !int.TryParse(a, out _)).ToArray();
		CCountryTag actorTag = new(tags.Length > 0 ? tags[0] : "GER");
		CCountryTag recipientTag = new(tags.Length > 1 ? tags[1] : "POL");
		bool limited = args.Contains("limited");
		int hours = args.Skip(2).Select(a => int.TryParse(a, out int h) ? h : -1).FirstOrDefault(h => h >= 0, 48);

		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var reports = new List<string>();
		using var scripts = new CAiScripts(directories, reports.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts);
		state.Played.Clear();
		string Name(CCountryTag tag) => state.Country(tag)?.Name ?? tag.Value;
		string WarName(CWar war) => war.MadeName?.Text(localisation.Localise) ?? war.Name;

		Console.WriteLine($"casus belli types: {string.Join(", ", state.CasusBelliTypes.Where(t => t.IsReal).Select(t => $"{t.Name} (rank {t.Rank}{(t.Always ? ", always" : "")})"))}");
		foreach (CWar war in state.Wars)
		{
			Console.WriteLine($"at war: {WarName(war)}{(war.IsLimited ? " (limited)" : "")}, {string.Join(" ", war.Attackers)} against {string.Join(" ", war.Defenders)}");
			foreach ((CCountryTag holder, List<CWarGoal> goals) in war.Goals)
			{
				Console.WriteLine($"   {holder}'s goals: {string.Join(", ", goals)}");
			}
		}
		Console.WriteLine($"mobilised: {string.Join(" ", state.Countries.Where(c => c.Mobilised).Select(c => $"{c.Tag} (to {CDate.FromDayNumber((int)(c.EventModifiers.First(m => m.Modifier.Name == CWarSides.Mobilisation).UntilHour / 24))})"))}");

		if (args.Contains("who"))
		{
			// Every pair whose row may be pressed on the new game, and nothing more.
			var facts = CCountryFacts.Of(state);
			foreach (CCountry one in state.Countries.Where(c => facts.OwnedCount(c.Tag) > 0))
			{
				var targets = state.Countries.Where(other => facts.OwnedCount(other.Tag) > 0 && CDeclarations.IsSelectable(state, one, other))
					.Select(other => other.Tag.Value).ToList();
				if (targets.Count > 0)
				{
					Console.WriteLine($"{one.Tag} may declare on {string.Join(" ", targets)}");
				}
			}
			return 0;
		}
		if (state.Country(actorTag) is not { } actor || state.Country(recipientTag) is not { } recipient)
		{
			Console.Error.WriteLine("no such country");
			return 2;
		}
		Console.WriteLine();
		Console.WriteLine($"{actor.Tag} on {recipient.Tag}: the row {(CDeclarations.IsSelectable(state, actor, recipient) ? "may" : "may not")} be pressed; " +
			$"ceiling for war {OpenHOI3.Sim.Diplomacy.CNeutrality.WarCeiling(state, actor, recipient)}, effective neutrality {actor.EffectiveNeutrality}");
		List<CCasusBelliType> offered = CWarGoals.Offered(state, actor, recipient);
		Console.WriteLine($"the dialog offers: {(offered.Count == 0 ? "nothing" : string.Join(", ", offered.Select(t => t.Name)))}");
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;
		// Two already at war go on with that war.
		CWar? made = CWarSides.Between(state, actor.Tag, recipient.Tag);
		if (made is not null)
		{
			Console.WriteLine("at war already: that war goes on");
		}
		else if (CWarGoals.FirstOffered(state, actor, recipient) is not { } goal)
		{
			Console.WriteLine("no goal to declare with: a declaration without one does nothing");
			return 0;
		}
		else
		{
			Console.WriteLine($"declaring with {goal}{(limited ? ", limited" : "")}");
			var offer = new COffer(EOfferKind.DeclareWar, actor.Tag, recipient.Tag, EOfferStage.Accepted, !limited, state.TotalHours)
			{
				WarGoal = goal,
			};
			new COfferCommand(offer).Execute(state);
			made = CWarSides.Between(state, actor.Tag, recipient.Tag);
		}
		if (made is null)
		{
			Console.WriteLine("no war came of it");
			return 0;
		}
		void Sides(string when)
		{
			Console.WriteLine($"{when}: {WarName(made)}{(made.IsLimited ? " (limited)" : "")}");
			Console.WriteLine($"   attackers: {string.Join(", ", made.Attackers.Select(t => $"{t} {Name(t)}"))}");
			Console.WriteLine($"   defenders: {string.Join(", ", made.Defenders.Select(t => $"{t} {Name(t)}"))}");
			Console.WriteLine($"   calls waiting: {(made.CallsWaiting.Count == 0 ? "none" : string.Join(" ", made.CallsWaiting))}");
		}
		Sides("at once");
		Console.WriteLine($"   relation {CGameState.Status(actor, recipient).Relation} and {CGameState.Status(recipient, actor).Relation}; " +
			$"threat {recipient.Tag} feels {CGameState.Status(recipient, actor).Threat}");
		Console.WriteLine($"   told: {string.Join(", ", told.Select(m => m.Told is { } t ? $"{m.We} to {t}" : m.We))}");

		int answered = 0;
		for (int hour = 0; hour < hours; hour++)
		{
			CTick.RunHour(state);
			var posted = new List<CCommand>(state.Outbox);
			state.Outbox.Clear();
			foreach (CCommand command in posted)
			{
				if (command is COfferCommand { Offer.Kind: EOfferKind.CallAlly } call)
				{
					answered++;
					Console.WriteLine($"   hour {hour + 1}: {call.Offer.Recipient} {(call.Offer.Stage == EOfferStage.Accepted ? "accepts" : "declines")} {call.Offer.Actor}'s call against {call.Offer.Versus}");
				}
				if (command.MayExecute(state))
				{
					command.Execute(state);
				}
			}
		}
		Sides($"after {hours} hours, {answered} calls answered");
		if (args.Contains("peace"))
		{
			OfferPeace(state, actor, recipient, made, told);
			Sides("after the peace");
		}
		if (args.Contains("occupy"))
		{
			CCountryTag? lender = args.FirstOrDefault(a => a.StartsWith("lend=", StringComparison.Ordinal)) is { } lend ? new CCountryTag(lend[5..]) : null;
			Occupy(state, actor, recipient, told, Name, lender);
			Sides("after the surrender");
		}
		Console.WriteLine($"{reports.Count} Lua errors{(reports.Count > 0 ? ": " + string.Join("; ", reports.Take(3)) : "")}");
		return 0;
	}

	/// <summary>
	/// The recipient's units are taken away, the actor is given every province it owns to hold, and hours run to the end of the next day: the
	/// recipient's surrender progress before and after, and what its surrender made of it and its land. With
	/// <c>lend=TAG</c>, that country first lends the recipient two divisions, one standing on ground held by a country
	/// neither at war nor the recipient's friend and one in the lender's seat, and both are told before and after.
	/// </summary>
	private static void Occupy(CGameState state, CCountry actor, CCountry recipient, List<CPactMessage> told, Func<CCountryTag, string> name,
		CCountryTag? lenderTag)
	{
		Console.WriteLine();
		// Its units go first, or they would take their ground back within the hour.
		foreach (var unit in recipient.UnitsInOrder())
		{
			OpenHOI3.Sim.Combat.CCombatRetreat.RemoveFromGame(state, unit);
		}
		CCountry? lender = lenderTag is { } tag ? state.Country(tag) : null;
		List<OpenHOI3.Sim.Units.CUnit> lent = lender is null ? new() : Lend(state, lender, recipient);
		void Lent(string when)
		{
			foreach (var unit in lent)
			{
				Console.WriteLine($"   {when}: {unit.Name} is {unit.Owner}'s{(unit.Lender is { } l ? $", lent by {l}" : "")}, at {state.Province(unit.Location).Name}" +
					$"{(unit.IsRemoved ? ", out of the game" : "")}, on its lender's list {lender!.LentUnits.Contains(unit)}, under {unit.Parent?.Name ?? "nobody"}");
			}
		}
		Lent("lent");
		int owned = state.Provinces.Count(province => province.Owner == recipient.Tag);
		// Through the setter with no consequences, so the capture's occupation policy is set (occupation.md, section 2).
		new CHoldLandCommand(actor.Tag, recipient.Tag).Execute(state);
		Console.WriteLine($"{actor.Tag} given all {owned} provinces {recipient.Tag} owns; {recipient.Tag}'s national unity {recipient.NationalUnity}, capital {recipient.Capital}, seat {recipient.ActingCapital}, original {recipient.OriginalCapital}");
		Console.WriteLine($"   occupation policy {actor.Tag} -> {recipient.Tag}: {OpenHOI3.Sim.Occupation.COccupationPolicies.Of(state, actor, recipient).Name}");
		int heard = told.Count;
		CDate day = state.Date;
		while (state.Date == day)
		{
			CTick.RunHour(state);
			foreach (CCommand command in state.Outbox.ToArray())
			{
				if (command.MayExecute(state))
				{
					command.Execute(state);
				}
			}
			state.Outbox.Clear();
		}
		Console.WriteLine($"   surrender progress {CSurrender.Progress(state, recipient)}; last surrendered {(recipient.LastSurrender == state.TotalHours ? "now" : "not")}");
		var owners = state.Provinces.Where(p => p.Cores.Contains(recipient.Tag)).GroupBy(p => p.Owner?.Value ?? "nobody")
			.Select(g => $"{g.Key} {g.Count()}");
		Console.WriteLine($"   its cores now owned by: {string.Join(", ", owners)}");
		Console.WriteLine($"   overlord {(recipient.Overlord is { } o ? name(o) : "none")}; in exile {recipient.InExile}; acting capital {state.Province(recipient.ActingCapital).Name}; " +
			$"at war {state.IsAtWar(recipient.Tag)}; faction '{recipient.Faction}'");
		Console.WriteLine($"   told: {string.Join(", ", told.Skip(heard).Select(m => m.Told is { } t ? $"{m.We} to {t}" : $"{m.We}/{m.They}/{m.Other}"))}");
		Lent("after");
	}

	/// <summary>The lender's first two divisions, put where <see cref="Occupy"/> says and handed over as an accepted offer hands them.</summary>
	private static List<OpenHOI3.Sim.Units.CUnit> Lend(CGameState state, CCountry lender, CCountry host)
	{
		var divisions = lender.UnitsInOrder().Where(unit => unit.Kind == OpenHOI3.Sim.Units.EUnitKind.Division && unit.Lender is null).Take(2).ToList();
		OpenHOI3.Sim.Provinces.CProvince? away = state.Provinces.FirstOrDefault(province => !province.IsSea && province.Controller is { } holder
			&& holder != host.Tag && holder != lender.Tag && !CExile.IsFriend(state, holder, host) && !state.IsAtWar(holder));
		int[] places = { away?.Id ?? lender.ActingCapital, lender.ActingCapital };
		for (int i = 0; i < divisions.Count; i++)
		{
			divisions[i].Location = places[i];
			divisions[i].Path.Clear();
			CExpeditions.HandOver(state, divisions[i], lender, host);
		}
		state.ForgetPositions();
		return divisions;
	}

	/// <summary>
	/// The actor offers the recipient peace as the row would, and the hours run until the recipient's AI has
	/// answered: whether the row may be pressed, the AI's score, what would change hands, and the answer.
	/// </summary>
	private static void OfferPeace(CGameState state, CCountry actor, CCountry recipient, CWar war, List<CPactMessage> told)
	{
		Console.WriteLine();
		Console.WriteLine($"{actor.Tag} offers {recipient.Tag} peace: the row {(CPeace.IsSelectable(state, actor, recipient) ? "may" : "may not")} be pressed" +
			$"{(CPeace.Negotiators(state, actor, recipient) ? "" : ", a negotiator may not speak for its side")}");
		var offer = new COffer(EOfferKind.Peace, actor.Tag, recipient.Tag, EOfferStage.Proposed, true, state.TotalHours);
		Console.WriteLine($"   DiploScore_PeaceAction answers {CPactPasses.Score(state, offer)}; surrender progress {actor.Tag} " +
			$"{CSurrender.Progress(state, actor)}, {recipient.Tag} {CSurrender.Progress(state, recipient)}");
		(CCountryTag attacking, CCountryTag defending) = CPeace.Parties(war, actor.Tag, recipient.Tag);
		var transfers = CPeace.DefendersLose(state, war, attacking);
		transfers.AddRange(CPeace.AttackersLose(state, war, defending));
		Console.WriteLine($"   would change hands: {(transfers.Count == 0 ? "nothing" : string.Join(", ", transfers.Select(t => $"{t.Province} {t.Loser}>{t.Gainer}")))}");
		new COfferCommand(offer).Execute(state);
		int heard = told.Count;
		for (int hour = 0; hour < 3 && CCountryLists.AtWar(state, actor.Tag, recipient.Tag); hour++)
		{
			CTick.RunHour(state);
			var posted = new List<CCommand>(state.Outbox);
			state.Outbox.Clear();
			foreach (CCommand command in posted)
			{
				if (command is COfferCommand { Offer.Kind: EOfferKind.Peace } answer)
				{
					Console.WriteLine($"   hour {hour + 1}: {recipient.Tag} {(answer.Offer.Stage == EOfferStage.Accepted ? "accepts" : "declines")}");
				}
				if (command.MayExecute(state))
				{
					command.Execute(state);
				}
			}
		}
		Console.WriteLine($"   at war: {CCountryLists.AtWar(state, actor.Tag, recipient.Tag)}; truce: {CTruces.Has(state, actor, recipient)}; " +
			$"told: {string.Join(", ", told.Skip(heard).Select(m => m.Told is { } t ? $"{m.We} to {t}" : m.We))}");
	}
}
