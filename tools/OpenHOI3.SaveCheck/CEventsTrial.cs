using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Script;

/// <summary>
/// Plays a new game for some days with nobody playing but the country given, and says which events
/// fired, for whom, and which decisions the country is offered at the end:
///
///   OpenHOI3.SaveCheck events &lt;install folder&gt; [tag] [days]
///
/// A look and not a check: no save keeps what fired, and the original's AI draws its options on the
/// clock. Germany for 180 days by default. See <c>docs/mechanics/events.md</c>.
/// </summary>
internal static class CEventsTrial
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck events <install folder> [tag] [days]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CDirectorySettings.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);
		var tag = new CCountryTag(args.Length > 2 ? args[2].ToUpperInvariant() : "GER");
		int days = args.Length > 3 ? int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 180;
		if (state.Country(tag) is null)
		{
			Console.Error.WriteLine($"no country is {tag}");
			return 2;
		}
		state.Played.Add(tag);

		var fired = new List<(CDate Date, CEvent Event, string Who)>();
		state.EventFired += (shown, scope) =>
		{
			CCountryTag? who = scope.Country ?? state.Province(scope.Province).Owner;
			fired.Add((state.Date, shown, who?.Value ?? "---"));
		};
		var simulation = new CSimulation(state);
		int candidates = state.Countries.Sum(c => c.EventCandidates.Count);
		var clock = System.Diagnostics.Stopwatch.StartNew();
		simulation.RunHours(days * 24);
		simulation.Pump();

		Console.WriteLine($"{days} days from {state.StartDate} to {state.Date} in {clock.ElapsedMilliseconds} ms, {tag} played");
		Console.WriteLine($"candidates when entered: {candidates}; now: {state.Countries.Sum(c => c.EventCandidates.Count)}");
		Console.WriteLine($"fired: {fired.Count}, of {fired.Select(f => f.Event).Distinct().Count()} events; "
			+ $"{state.FiredOnce.Count} fire-once events used up");
		Console.WriteLine();
		foreach ((CDate date, CEvent shown, string who) in fired)
		{
			string title = localisation.Localise(shown.Title);
			Console.WriteLine($"   {date}  {who}  {shown.Id,6}  {title}");
		}
		Console.WriteLine();
		Console.WriteLine($"open for {tag}: {string.Join(", ", state.OpenEvents.Select(o => o.Event.Id))}");
		Console.WriteLine($"decisions offered to {tag}:");
		foreach ((CDecision decision, bool mayTake) in CDecisions.Offered(state, tag))
		{
			Console.WriteLine($"   {(mayTake ? "may take " : "may not  ")} {decision.Name}");
		}
		return 0;
	}
}
