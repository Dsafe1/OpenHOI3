using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Wars;

/// <summary>
/// A trial, not a check: no reference save holds an underground. On a new game the occupier is put at war
/// with the occupied and given ten of its provinces away from its capital; an underground of the occupied is finished
/// in each of the first three, as a line of the queue would finish it, to spread, to raise partisans and to build up; and the days are run,
/// telling each underground's strength, the partisans it raises, its spreading and its end:
///
///   OpenHOI3.SaveCheck underground &lt;install folder&gt; [occupier] [occupied] [days] [seed]
///
/// Germany and Poland for ninety days with nothing given. See <c>docs/mechanics/undergrounds.md</c>.
/// </summary>
internal static class CUndergroundTrial
{
	/// <summary>How many of the occupied's provinces the occupier is given.</summary>
	private const int Held = 10;

	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck underground <install folder> [occupier] [occupied] [days] [seed]");
			return 2;
		}
		string[] tags = args.Skip(2).Where(a => a.Length == 3 && !int.TryParse(a, out _)).ToArray();
		int[] numbers = args.Skip(2).Select(a => int.TryParse(a, out int n) ? n : -1).Where(n => n >= 0).ToArray();
		CCountryTag occupierTag = new(tags.Length > 0 ? tags[0] : "GER");
		CCountryTag occupiedTag = new(tags.Length > 1 ? tags[1] : "POL");
		int days = numbers.Length > 0 ? numbers[0] : 90;

		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);
		if (numbers.Length > 1)
		{
			state.Seed = (uint)numbers[1];
		}
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;
		if (state.Country(occupierTag) is not { } occupier || state.Country(occupiedTag) is not { } occupied)
		{
			Console.Error.WriteLine("no such country");
			return 2;
		}
		if (CUndergrounds.Building(state) is not { } building)
		{
			Console.Error.WriteLine("no building carries local_underground");
			return 1;
		}
		Console.WriteLine($"the underground is '{building.Key}', most level {building.MaxLevel}, {(building.IsRepaired ? "repaired" : "not repaired")}");

		CDeclarations.Apply(state, occupier, occupied, true, null);
		foreach (var unit in occupied.UnitsInOrder())
		{
			OpenHOI3.Sim.Combat.CCombatRetreat.RemoveFromGame(state, unit);
		}
		CProvince capital = state.Province(occupied.Capital);
		var kept = new HashSet<int>(capital.Edges.Select(edge => edge.To)) { capital.Id };
		var held = state.Provinces.Where(p => p.Owner == occupiedTag && !kept.Contains(p.Id)).Take(Held).ToList();
		foreach (CProvince province in held)
		{
			CProvinceHands.SetController(state, province, occupierTag, consequences: true);
		}
		OpenHOI3.Sim.Modifiers.CProvinceModifiers.Rebuild(state);
		Console.WriteLine($"{occupierTag} holds {held.Count} of {occupiedTag}'s provinces");

		var watched = new List<CProvince>();
		for (int index = 0; index < Math.Min(3, held.Count); index++)
		{
			CProvince province = held[index];
			Console.WriteLine($"   {province.Id} {province.Name}: may be placed {CUndergrounds.CanPlaceIn(state, province, occupied)}, starts at {CUndergrounds.InitialStrength(state, province, occupied)}");
			new CPlaceUndergroundCommand(occupiedTag, province.Id).Execute(state);
			new CChangeUndergroundMissionCommand(province.Id, index switch { 0 => CUndergrounds.Spread, 1 => CUndergrounds.RaisePartisans, _ => CUndergrounds.BuildUp }).Execute(state);
			watched.Add(province);
			// Where its partisans and its spread may go: each way out, with its owner and its holder.
			Console.WriteLine($"      ways out: {string.Join(", ", province.Edges.Select(e => $"{e.To} {state.Province(e.To).Owner?.Value ?? "-"}/{state.Province(e.To).Controller?.Value ?? "-"}"))}");
		}

		for (int day = 1; day <= days; day++)
		{
			CDate today = state.Date;
			while (state.Date == today)
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
			if (day == 1 || day % 10 == 0 || day == days)
			{
				var ours = CUndergrounds.Of(state, occupiedTag);
				Console.WriteLine($"{state.Date}: {string.Join("; ", ours.Select(p => $"{p.Id} {CUndergrounds.Strength(state, p)} on {p.UndergroundAction}"))}" +
					$"{(ours.Count == 0 ? "none" : "")}; rebel factions {state.RebelFactions.Count}");
			}
		}
		foreach (CRebelFaction faction in state.RebelFactions)
		{
			var armies = faction.Armies.Select(id => CRebelFactions.FindUnit(state, id)).Where(u => u is not null)
				.Select(u => $"{u!.Name} ({u.Owner}'s) at {u.Location}");
			Console.WriteLine($"   {faction.Name} ({faction.Type.Name}): {string.Join("; ", armies)}");
		}
		Console.WriteLine($"{occupiedTag}'s armies raised by its undergrounds: {occupied.Units.Count}");
		Console.WriteLine($"told: {string.Join(", ", told.GroupBy(m => m.We).Select(g => $"{g.Key} x{g.Count()}"))}");
		return 0;
	}
}
