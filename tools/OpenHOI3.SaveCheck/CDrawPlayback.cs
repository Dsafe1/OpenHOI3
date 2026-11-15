using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

/// <summary>
/// A run of the original's played back draw for draw (<c>docs/replay.md</c>, section 9): the folder the fact base's
/// <c>probes/hoidraws.lua</c> recorded - <c>draws.txt</c>, and each hour's <c>gen_&lt;tick&gt;.txt</c> and
/// <c>ai_&lt;tick&gt;.txt</c>. As each hour begins the game's generator is put where the original's stood, and every draw a
/// country's AI makes - on the AI's generator and in the Lua's <c>math.random</c> - is answered with what the original drew
/// for that country that hour, in order. Where ours draws more than the original did, the draw is its own and counted;
/// where less, what is left is counted. A measuring instrument: nothing of it is in the game.
/// </summary>
internal sealed class CDrawPlayback
{
	/// <summary>How far below a job's entry a thread's stack may reach: a draw within it is that job's.</summary>
	private const long StackReach = 0x100000;

	private const int GeneratorSize = 624;

	private readonly string _folder;
	private readonly Dictionary<long, Hour> _hours = new();
	private Hour? _current;

	private CDrawPlayback(string folder)
	{
		_folder = folder;
	}

	/// <summary>The draws of one hour of the original's, by whom they were made for.</summary>
	private sealed class Hour
	{
		public readonly Dictionary<(string Tag, string? Kind), Queue<int>> Ai = new();
		public readonly Dictionary<string, Queue<int>> Rand = new();
		public int UnfiledAi;
		public int UnfiledRand;
		public int FedAi;
		public int FedRand;
		public readonly Dictionary<string, int> MissedAi = new();
		public readonly Dictionary<string, int> MissedRand = new();
	}

	/// <summary>Reads a recording; its hours are keyed by the original's tick.</summary>
	public static CDrawPlayback Load(string folder)
	{
		var playback = new CDrawPlayback(folder);
		long tick = -1;
		var entries = new List<(long Esp, string Tag)>();
		var aiDraws = new List<(string? Tag, string? Kind, int Index)>();
		Hour? hour = null;
		void Close()
		{
			if (hour is not null)
			{
				playback.Fill(tick, hour, aiDraws);
			}
			aiDraws.Clear();
			entries.Clear();
		}
		// Shared, so a recording still being written can be read.
		using var reader = new StreamReader(new FileStream(Path.Combine(folder, "draws.txt"), FileMode.Open, FileAccess.Read,
			FileShare.ReadWrite));
		while (reader.ReadLine() is { } line)
		{
			string[] p = line.Split(' ');
			switch (p[0])
			{
				case "H":
					Close();
					tick = long.Parse(p[1], CultureInfo.InvariantCulture);
					hour = new Hour();
					playback._hours[tick] = hour;
					break;
				case "E" when hour is not null:
				{
					long esp = long.Parse(p[1], NumberStyles.HexNumber);
					// A thread runs one job at a time: an entry on the same stack ends the one before.
					entries.RemoveAll(e => Math.Abs(e.Esp - esp) < StackReach);
					entries.Add((esp, p[2]));
					break;
				}
				case "A" when hour is not null:
				{
					// `A <esp> <index> <caller>`, and `... trade <tag>` for a draw of the hourly trading, which the probe marks.
					bool trade = p.Length > 5 && p[4] == CRandom.TradeKind;
					string? tag = trade ? (p[5] == "?" ? null : p[5]) : Owner(entries, long.Parse(p[1], NumberStyles.HexNumber));
					aiDraws.Add((tag, trade ? CRandom.TradeKind : null, int.Parse(p[2], CultureInfo.InvariantCulture)));
					break;
				}
				case "R" when hour is not null:
				{
					string? tag = Owner(entries, long.Parse(p[1], NumberStyles.HexNumber));
					if (tag is null)
					{
						hour.UnfiledRand++;
					}
					else
					{
						Queue(hour.Rand, tag).Enqueue(int.Parse(p[2], CultureInfo.InvariantCulture));
					}
					break;
				}
			}
		}
		Close();
		return playback;
	}

	/// <summary>The country of the latest job whose thread's stack holds that point, or null.</summary>
	private static string? Owner(List<(long Esp, string Tag)> entries, long esp)
	{
		for (int i = entries.Count - 1; i >= 0; i--)
		{
			if (entries[i].Esp - esp is >= 0 and < StackReach)
			{
				return entries[i].Tag;
			}
		}
		return null;
	}

	private static Queue<int> Queue<TKey>(Dictionary<TKey, Queue<int>> queues, TKey key) where TKey : notnull
	{
		if (!queues.TryGetValue(key, out Queue<int>? queue))
		{
			queue = new Queue<int>();
			queues[key] = queue;
		}
		return queue;
	}

	/// <summary>
	/// The AI generator's draws of an hour, their values worked out from the hour's dump: each was logged with the index it
	/// drew at, so a draw the probe did not see moves no other.
	/// </summary>
	private void Fill(long tick, Hour hour, List<(string? Tag, string? Kind, int Index)> draws)
	{
		if (ReadGenerator(Path.Combine(_folder, $"ai_{tick}.txt"), indexFirst: true) is not { } dump)
		{
			return;
		}
		int most = draws.Count == 0 ? 0 : draws.Max(d => (d.Index - dump.Index + GeneratorSize) % GeneratorSize);
		var generator = new CRandom();
		generator.Restore((dump.Words, dump.Index, 0));
		var values = new int[most + 1];
		for (int i = 0; i <= most && draws.Count > 0; i++)
		{
			values[i] = generator.Next();
		}
		foreach ((string? tag, string? kind, int index) in draws)
		{
			if (tag is null)
			{
				hour.UnfiledAi++;
				continue;
			}
			Queue(hour.Ai, (tag, kind)).Enqueue(values[(index - dump.Index + GeneratorSize) % GeneratorSize]);
		}
	}

	/// <summary>A dump of an MT19937: hoidraws' <c>ai_</c> (index, then the words) or hoiDumpGenerator's (draws and index).</summary>
	private static (uint[] Words, int Index, long Draws)? ReadGenerator(string path, bool indexFirst)
	{
		if (!File.Exists(path))
		{
			return null;
		}
		string[] lines = File.ReadAllLines(path);
		string[] head = lines[0].Split(' ');
		var words = new uint[GeneratorSize];
		for (int i = 0; i < GeneratorSize; i++)
		{
			words[i] = uint.Parse(lines[i + 1], NumberStyles.HexNumber);
		}
		return indexFirst
			? (words, int.Parse(head[0], CultureInfo.InvariantCulture), 0)
			: (words, int.Parse(head[1], CultureInfo.InvariantCulture), long.Parse(head[0], CultureInfo.InvariantCulture));
	}

	/// <summary>Hands the game's AI generator and the scripts' <c>math.random</c> to this recording.</summary>
	public void Attach(CGameState state, OpenHOI3.Lua.CAiScripts scripts)
	{
		state.AiRandom.Feed = (who, kind) => Take(_current?.Ai, (who.Value, kind), _current?.MissedAi,
			kind is null ? who.Value : $"{who.Value}:{kind}", fed: true);
		scripts.RandFeed = who => Take(_current?.Rand, who.Value, _current?.MissedRand, who.Value, fed: false);
	}

	private int? Take<TKey>(Dictionary<TKey, Queue<int>>? queues, TKey key, Dictionary<string, int>? missed, string tag, bool fed)
		where TKey : notnull
	{
		if (_current is null || queues is null)
		{
			return null;
		}
		if (queues.TryGetValue(key, out Queue<int>? queue) && queue.Count > 0)
		{
			if (fed)
			{
				_current.FedAi++;
			}
			else
			{
				_current.FedRand++;
			}
			return queue.Dequeue();
		}
		missed![tag] = missed.GetValueOrDefault(tag) + 1;
		return null;
	}

	/// <summary>
	/// As the hour of that tick begins, as the original's dumps were taken: the game's generator put where the original's
	/// stood, and that hour's draws made the ones handed out. False where the recording has no such hour.
	/// </summary>
	public bool BeginHour(CGameState state, long tick)
	{
		_current = _hours.GetValueOrDefault(tick);
		if (ReadGenerator(Path.Combine(_folder, $"gen_{tick}.txt"), indexFirst: false) is { } game)
		{
			state.Random.Restore(game);
		}
		return _current is not null;
	}

	/// <summary>One line on the hour: what was handed out, what ours asked for beyond it, and what it left.</summary>
	public string Summary()
	{
		if (_current is not { } hour)
		{
			return "draws: no such hour in the recording";
		}
		int leftAi = hour.Ai.Values.Sum(q => q.Count);
		int leftRand = hour.Rand.Values.Sum(q => q.Count);
		static string Who(Dictionary<string, int> counts) => counts.Count == 0 ? "" :
			" (" + string.Join(" ", counts.OrderByDescending(c => c.Value).Take(8).Select(c => $"{c.Key} {c.Value}")) + ")";
		static string Left<TKey>(Dictionary<TKey, Queue<int>> queues, Func<TKey, string> name) where TKey : notnull =>
			string.Join(" ", queues.Where(q => q.Value.Count > 0).OrderByDescending(q => q.Value.Count).Take(8)
				.Select(q => $"{name(q.Key)} {q.Value.Count}"));
		string leftAiWho = Left(hour.Ai, k => k.Kind is null ? k.Tag : $"{k.Tag}:{k.Kind}");
		string leftRandWho = Left(hour.Rand, k => k);
		return $"draws: AI generator fed {hour.FedAi}, ours beyond {hour.MissedAi.Values.Sum()}{Who(hour.MissedAi)}, " +
			$"left {leftAi}{(leftAiWho.Length > 0 ? $" ({leftAiWho})" : "")}, nobody's {hour.UnfiledAi}; " +
			$"math.random fed {hour.FedRand}, ours beyond {hour.MissedRand.Values.Sum()}{Who(hour.MissedRand)}, " +
			$"left {leftRand}{(leftRandWho.Length > 0 ? $" ({leftRandWho})" : "")}, nobody's {hour.UnfiledRand}";
	}
}
