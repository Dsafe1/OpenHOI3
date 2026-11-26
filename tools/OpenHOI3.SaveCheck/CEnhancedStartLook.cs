using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Rules;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

/// <summary>
/// A look at what the fixes and defines taken after BiceLib's make of a new game (<c>docs/enhanced.md</c>, <i>What is
/// built</i>): the game's rules and the two defines' answers; the land units whose leader has not the rank of their
/// level, and the leaders a file of units put in service ahead of their history's day; the leaders still to come and
/// the rank each comes at; the queue's brigades with a historical model and whether each has a level for every
/// technology of its type; and every country's candidate events written to a save and read back.
///
///   OpenHOI3.SaveCheck enhancedstart &lt;install folder&gt; [mod=NAME]
///
/// Run it with and without a mod's enhanced file to see both arms; the base game is the faithful one.
/// </summary>
internal static class CEnhancedStartLook
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("usage: OpenHOI3.SaveCheck enhancedstart <install folder> [mod=NAME]");
			return 2;
		}
		var warnings = new List<string>();
		CDirectorySettings directories = CToolDirectories.At(args[1]);
		CLocalisation localisation = CLocalisation.Load(directories, warn: warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, warnings.Add);
		CMap map = CMap.Load(directories, warnings.Add);
		CGameState state = CGameStateLoader.LoadStart(directories, map, localisation, countries, warnings.Add);

		Console.WriteLine($"rules: {(state.Rules.Enhanced ? state.Rules.Text : "the faithful game's")}");
		foreach (EFix fix in new[]
		{
			EFix.HistoricalModelChoice, EFix.QueuedHistoricalModel, EFix.PlaceUnresearchedBuilding, EFix.OobLeaderRanks,
			EFix.HistoryLeaderRanks, EFix.SavedEventCandidates,
		})
		{
			Console.WriteLine($"  {CFixes.Name(fix)}: {(state.Rules.Has(fix) ? "on" : "off")}");
		}
		Console.WriteLine($"  a month of peace pays war exhaustion to neutrality: {(CWarExhaustion.PaysNeutrality(state) ? "yes" : "no")}");
		Console.WriteLine($"  the AI lends its units: {(CAiExpeditions.MayLend(state) ? "yes" : "no")}");

		Leaders(state);
		Queue(state);
		return Candidates(state, directories, map, localisation, warnings) ? 0 : 1;
	}

	private static void Leaders(CGameState state)
	{
		int led = 0;
		int otherRank = 0;
		int foreign = 0;
		int early = 0;
		var examples = new List<string>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.Leader is not { } leader || RankOf(unit.Kind) is not { } rank)
				{
					continue;
				}
				led++;
				if (leader.Country != unit.Owner)
				{
					foreign++;
				}
				else if (leader.Rank != rank)
				{
					otherRank++;
					if (examples.Count < 6)
					{
						examples.Add($"{country.Tag} {unit.Name}: {leader.Name}, rank {leader.Rank} where the unit asks {rank}");
					}
				}
			}
			early += country.ActiveLeaders.Count(leader => leader.FirstRankDate is { } first && first.CompareTo(state.Date) > 0);
		}
		Console.WriteLine();
		Console.WriteLine($"land units with a leader: {led}");
		Console.WriteLine($"  their own country's leader at another rank than the unit's level: {otherRank}");
		examples.ForEach(line => Console.WriteLine($"    {line}"));
		Console.WriteLine($"  another country's leader, left as the file has him: {foreign}");
		Console.WriteLine($"  leaders in service whose history's first rank is still to come: {early}");

		var coming = new SortedDictionary<int, int>();
		foreach (CLeader leader in state.Leaders.Values)
		{
			if (leader.Rank == 0 && leader.FirstRankDate is { } first && first.CompareTo(state.Date) > 0)
			{
				int rank = state.Rules.Has(EFix.HistoryLeaderRanks) ? Math.Clamp(leader.FirstRank, 1, 4) : 1;
				coming[rank] = coming.GetValueOrDefault(rank) + 1;
			}
		}
		Console.WriteLine($"leaders still to come, by the rank each comes at: " +
			(coming.Count == 0 ? "none" : string.Join(", ", coming.Select(entry => $"rank {entry.Key} {entry.Value}"))));
	}

	private static int? RankOf(EUnitKind kind)
	{
		return kind switch
		{
			EUnitKind.Division => 1,
			EUnitKind.Corps => 2,
			EUnitKind.Army => 3,
			EUnitKind.ArmyGroup or EUnitKind.Theatre => 4,
			_ => null,
		};
	}

	private static void Queue(CGameState state)
	{
		int modelled = 0;
		int whole = 0;
		foreach (CCountry country in state.Countries)
		{
			foreach (CConstruction line in country.Constructions)
			{
				foreach (CBrigadeOrder order in line.Brigades)
				{
					if (order.HistoricalModel == CSubUnit.NoModel)
					{
						continue;
					}
					modelled++;
					if (order.Model.Count == order.Type.Technologies.Count)
					{
						whole++;
					}
				}
			}
		}
		Console.WriteLine();
		Console.WriteLine($"queued brigades with a historical model: {modelled}; with a level for every technology of their type: {whole}");
	}

	/// <summary>Every country's candidates as a save holds them: written, loaded as a second game and set beside the first.</summary>
	private static bool Candidates(CGameState state, CDirectorySettings directories, CMap map, CLocalisation localisation,
		List<string> warnings)
	{
		int total = state.Countries.Sum(country => country.EventCandidates.Count);
		int having = state.Countries.Count(country => country.EventCandidates.Count != 0);
		Console.WriteLine();
		Console.WriteLine($"candidate events: {total} in {having} countries");
		string path = Path.Combine(Path.GetTempPath(), $"openhoi3_enhancedstart_{Environment.ProcessId}.hoi3");
		try
		{
			CSaveFiles.Write(state, localisation.Localise, path);
			long size = new FileInfo(path).Length;
			(CGameState loaded, CSaveReport report) = CSaveLoader.Load(directories, map, localisation,
				CCountries.Load(directories, localisation, warnings.Add), path, null, null, _ => { });
			int same = 0;
			int kept = 0;
			foreach (CCountry country in state.Countries)
			{
				CCountry other = loaded.Country(country.Tag)!;
				kept += other.EventCandidates.Count;
				if (country.EventCandidates.Select(candidate => candidate.Id)
					.SequenceEqual(other.EventCandidates.Select(candidate => candidate.Id)))
				{
					same++;
				}
			}
			Console.WriteLine($"  the save, {size / 1024} KB, loaded: {kept} candidates, {same} of {state.Countries.Count} countries' lists the same");
			foreach (KeyValuePair<(string Block, string Key), int> unread in report.UnreadKeys
				.Where(entry => entry.Key.Block == CSaveEventCandidates.Key))
			{
				Console.WriteLine($"  not read: {unread.Key.Key}, {unread.Value} times");
			}
			return state.Rules.Has(EFix.SavedEventCandidates) ? same == state.Countries.Count : kept == 0;
		}
		finally
		{
			File.Delete(path);
		}
	}
}
