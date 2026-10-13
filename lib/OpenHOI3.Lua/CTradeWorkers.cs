using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenHOI3.Content;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua;

/// <summary>
/// The hour's trading, run side by side as the original runs it: on worker threads, each with a Lua
/// state of its own that has run <c>autoexec.lua</c> itself (<c>reversing/findings/FINDINGS-tick.md</c>,
/// <c>FINDINGS-luahost.md</c>). See <c>docs/mechanics/trade.md</c>, section 9.
///
/// Ours, so that a game runs the same on every machine: there are always <see cref="Count"/> states, a
/// country's trading always runs on the same one - its number in the game's order, modulo the count - and
/// in the countries' order there, each state with its own <c>math.random</c> (<see cref="Native.CLuaRandom"/>);
/// a script's draws on the game's generator are made on one of its own for the run, seeded from the game's
/// seed, the hour and the country. What each run posts, and what it reports, is gathered and handed on in the
/// order of the runs. The original's threads take countries as they come free.
/// </summary>
internal sealed class CTradeWorkers : IDisposable
{
	/// <summary>
	/// How many states trade: as many as the original's machine had cores, here the same on every machine. On
	/// a machine of 2026 the hour's trading takes a third of its time alone with eight; sixteen took a tenth
	/// less again, for twice the states.
	/// </summary>
	public const int Count = 8;

	private readonly Worker[] _workers = new Worker[Count];

	public CTradeWorkers(CDirectorySettings directories)
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
		var results = new (List<CCommand> Posted, List<string> Reports)[runs.Count];
		var queues = new List<int>[Count];
		for (int i = 0; i < runs.Count; i++)
		{
			int k = runs[i].Country.Index % Count;
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
				(CCountry country, bool propose) = runs[i];
				var posted = new List<CCommand>();
				var reports = new List<string>();
				worker.Reports = reports;
				worker.Scripts.Api.Posted = posted;
				worker.Scripts.Api.WorkerRandom = new CRandom(Seed(state, country));
				worker.Scripts.RunTrade(state, country, propose);
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
