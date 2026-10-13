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
/// A look at factions on a new game, with the install's Lua answering: each faction and its members, how many
/// countries each leader's join and invite rows are open to, and then the actor sending the recipient the faction
/// row that applies - Join Faction where the recipient leads one, Invite to Faction where the actor does - and the
/// hours run to the answer. See <c>docs/mechanics/factions.md</c>.
///
///   OpenHOI3.SaveCheck faction &lt;install folder&gt; [actor] [recipient] [neutral]
///
/// <c>neutral</c> sets the joiner's effective neutrality to nought first, which nobody's is on 1 January 1936.
/// </summary>
internal static class CFactionTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck faction <install folder> [actor] [recipient] [neutral]");
			return 2;
		}
		string[] tags = args.Skip(2).Where(a => a.Length == 3).ToArray();
		CCountryTag actorTag = new(tags.Length > 0 ? tags[0] : "HUN");
		CCountryTag recipientTag = new(tags.Length > 1 ? tags[1] : "GER");

		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		var reports = new List<string>();
		using var scripts = new CAiScripts(directories, reports.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add, aiScripts: scripts);
		state.Played.Clear();
		var facts = CCountryFacts.Of(state);

		foreach (var group in state.IdeologyGroups.Where(g => g.Faction.Length > 0))
		{
			List<CCountry> members = CCountryLists.FactionMembers(state, group.Faction);
			Console.WriteLine($"{group.Faction}: {string.Join(" ", members.Select(m => m.Tag.Value))}; rules {string.Join(", ", group.FactionRules)}");
			Console.WriteLine($"   progress {CFactionProgress.Progress(state, group.Faction) / 1000} of {CFactionProgress.Total(state) / 1000}, " +
				$"share {CFactionProgress.Share(state, group.Faction) / 1000.0:0.000}%; victory conditions met " +
				$"{CVictoryConditions.Met(state, group.Faction)} of {CVictoryConditions.Chosen(state, group.Faction)} chosen" +
				$"{string.Concat(state.VictoryConditions.Where(v => v.Faction == group.Faction).SelectMany(v => v.Conditions).Where(c => c.Met).Select(c => ", " + c.Name))}");
			if (members.Count == 0)
			{
				continue;
			}
			CCountry leader = members[0];
			int join = 0, invite = 0;
			foreach (CCountry other in state.Countries.Where(c => facts.OwnedCount(c.Tag) > 0 && c != leader))
			{
				if (COfferRules.IsValid(state, Row(state, other, leader, true)) && COfferRules.IsSelectable(state, Row(state, other, leader, true)))
				{
					join++;
				}
				if (COfferRules.IsValid(state, Row(state, leader, other, false)) && COfferRules.IsSelectable(state, Row(state, leader, other, false)))
				{
					invite++;
				}
			}
			Console.WriteLine($"   rows that may be pressed on a new game: {join} countries may ask {leader.Tag} to join, {leader.Tag} may invite {invite}");
		}

		if (state.Country(actorTag) is not { } actor || state.Country(recipientTag) is not { } recipient)
		{
			Console.Error.WriteLine("no such country");
			return 2;
		}
		bool joining = CFactionOffers.Leads(state, recipient);
		COffer offer = Row(state, actor, recipient, joining);
		CCountry joiner = CFactionOffers.Joiner(offer, actor, recipient);
		if (args.Contains("neutral"))
		{
			joiner.EffectiveNeutrality = CFixedPoint.Zero;
		}
		Console.WriteLine();
		Console.WriteLine($"{actor.Tag} {(joining ? "asks to join" : "invites")} {recipient.Tag}: valid {COfferRules.IsValid(state, offer)}, " +
			$"refused by {CFactionOffers.Refusal(state, offer, actor, recipient)}; {joiner.Tag}'s effective neutrality {joiner.EffectiveNeutrality}, " +
			$"place ({joiner.AlignmentX / 1000.0}, {joiner.AlignmentY / 1000.0})");
		Console.WriteLine($"   DiploScore_InviteToFaction answers {CPactPasses.Score(state, offer)}");
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;
		new COfferCommand(offer).Execute(state);
		for (int hour = 0; hour < 3; hour++)
		{
			CTick.RunHour(state);
			var posted = new List<CCommand>(state.Outbox);
			state.Outbox.Clear();
			foreach (CCommand command in posted)
			{
				if (command is COfferCommand { Offer.Kind: EOfferKind.Faction } answer)
				{
					Console.WriteLine($"   hour {hour + 1}: {recipient.Tag} {(answer.Offer.Stage == EOfferStage.Accepted ? "accepts" : "declines")}");
				}
				if (command.MayExecute(state))
				{
					command.Execute(state);
				}
			}
		}
		Console.WriteLine($"   {joiner.Tag}'s faction now: {(joiner.Faction.Length > 0 ? joiner.Faction : "none")}; " +
			$"members {string.Join(" ", CCountryLists.FactionMembers(state, recipient.Faction.Length > 0 ? recipient.Faction : actor.Faction).Select(m => m.Tag.Value))}");
		foreach (string report in reports.Distinct().Take(10))
		{
			Console.WriteLine($"   lua: {report}");
		}
		return 0;
	}

	private static COffer Row(CGameState state, CCountry actor, CCountry recipient, bool join) =>
		new(EOfferKind.Faction, actor.Tag, recipient.Tag, EOfferStage.Proposed, join, state.TotalHours);
}
