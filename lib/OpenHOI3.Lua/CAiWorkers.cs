using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenHOI3.Content;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua;

/// <summary>
/// The hour's trading and the ministers' work, run side by side as the original runs its AI: on worker threads, each
/// with a Lua state of its own that has run <c>autoexec.lua</c> itself (<c>reversing/findings/FINDINGS-tick.md</c>,
/// <c>FINDINGS-luahost.md</c>, <c>FINDINGS-aisched.md</c>). See <c>docs/mechanics/trade.md</c>, section 9, and
/// <c>docs/mechanics/ministers.md</c>, *Ours*.
///
/// Ours, so that a game runs the same on every machine: there are always <see cref="Count"/> states, a
/// country's runs always run on the same one - by its number in the game's order (<see cref="ByNumber"/>,
/// <see cref="ByDay"/>) - and in the countries' order there, each state with its own <c>math.random</c> (<see cref="Native.CLuaRandom"/>);
/// a script's draws on the game's generator are made on one of its own for the run, seeded from the game's
/// seed, the hour and the country. What each run posts, and what it reports, is gathered and handed on in the
/// order of the runs. The original's threads take countries as they come free.
/// </summary>
internal sealed class CAiWorkers : IDisposable
{
	/// <summary>
	/// How many states trade: as many as the original's machine had cores, here the same on every machine. On
	/// a machine of 2026 the hour's trading takes a third of its time alone with eight; sixteen took a tenth
	/// less again, for twice the states.
	/// </summary>
	public const int Count = 8;

	private readonly Worker[] _workers = new Worker[Count];

	public CAiWorkers(CDirectorySettings directories)
	{
		Parallel.For(0, Count, k => _workers[k] = new Worker(directories));
	}

	/// <summary>
	/// Runs the trading, side by side, and answers what each run posted and reported, in the order of the
	/// runs. The state is only read meanwhile.
	/// </summary>
	public (List<CCommand> Posted, List<string> Reports)[] Run(CGameState state,
		IReadOnlyList<(CCountry Country, bool Propose)> runs)
	{
		var countries = new List<CCountry>(runs.Count);
		foreach ((CCountry country, _) in runs)
		{
			countries.Add(country);
		}
		return Run(state, countries, 0, ByNumber, (scripts, i) => scripts.RunTrade(state, runs[i].Country, runs[i].Propose));
	}

	/// <summary>
	/// The state of the trading's runs: the country's number, of the count. An hour's traders are a run of numbers one
	/// after another, so they fall on every state.
	/// </summary>
	public static int ByNumber(CCountry country) => country.Index % Count;

	/// <summary>
	/// The state of a minister's runs: the country's number over the hours of a day, of the count. A minister's countries
	/// of one hour are those whose numbers are one apart in 24 (its hour is the number and an offset, of 24), so the
	/// number alone would put them all on one state; this puts each of them on the next.
	/// </summary>
	public static int ByDay(CCountry country) => country.Index / CGameState.HoursPerDay % Count;

	/// <summary>
	/// Runs <paramref name="run"/> for each country side by side, each on the state <paramref name="stateOf"/> gives it
	/// and in the order of <paramref name="countries"/> there, and answers what each run posted and reported, in that
	/// order. A run's draws on the game's generator are on one of its own, seeded from the game's seed, the hour, the
	/// country and <paramref name="salt"/> - which minister, so that two ministers' runs of a country in one hour do not
	/// draw alike; the trading's is nought.
	/// </summary>
	public (List<CCommand> Posted, List<string> Reports)[] Run(CGameState state, IReadOnlyList<CCountry> countries,
		uint salt, Func<CCountry, int> stateOf, Action<CAiScripts, int> run)
	{
		var results = new (List<CCommand> Posted, List<string> Reports)[countries.Count];
		var queues = new List<int>[Count];
		for (int i = 0; i < countries.Count; i++)
		{
			int k = stateOf(countries[i]);
			(queues[k] ??= new List<int>()).Add(i);
		}
		Parallel.For(0, Count, k =>
		{
			if (queues[k] is not { } queue)
			{
				return;
			}
			Worker worker = _workers[k];
			foreach (int i in queue)
			{
				var posted = new List<CCommand>();
				var reports = new List<string>();
				worker.Reports = reports;
				worker.Scripts.Api.Posted = posted;
				worker.Scripts.Api.WorkerRandom = new CRandom(unchecked(Seed(state, countries[i]) + salt * 0x9E3779B9u));
				run(worker.Scripts, i);
				results[i] = (posted, reports);
			}
		});
		return results;
	}

	public void Dispose()
	{
		foreach (Worker? worker in _workers)
		{
			worker?.Scripts.Dispose();
		}
	}

	/// <summary>What a run's own generator starts from: the game's seed, the hour and the country.</summary>
	private static uint Seed(CGameState state, CCountry country)
	{
		unchecked
		{
			return state.Seed * 2654435761u + (uint)state.TotalHours * 40503u + (uint)(state.TotalHours >> 32)
				+ (uint)(country.Index + 1) * 2246822519u;
		}
	}

	/// <summary>One state, and where what it reports during a run goes.</summary>
	private sealed class Worker
	{
		public Worker(CDirectorySettings directories)
		{
			Scripts = CAiScripts.Worker(directories, line => Reports.Add(line));
		}

		public CAiScripts Scripts { get; }

		public List<string> Reports { get; set; } = new();
	}
}
